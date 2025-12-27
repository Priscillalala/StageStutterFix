using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.Networking;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.SceneManagement;

namespace StageStutterFix;

public static class BetterPreloadPatch
{
    private class NewScenePreloadData()
    {
        public string sceneName;
        public bool preserveInMenuScenes;
        public AsyncOperationHandle<IList<IResourceLocation>> locationsHandle;
        public List<AsyncOperationHandle<Object>> assetHandles;
        public Coroutine preloadAssetsCoroutine;

        public void ReleaseLocations()
        {
            if (locationsHandle.IsValid())
            {
                Addressables.Release(locationsHandle);
            }
        }

        public void ReleaseAssets()
        {
            if (assetHandles == null)
            {
                return;
            }
            foreach (var assetHandle in assetHandles)
            {
                if (assetHandle.IsValid())
                {
                    Addressables.Release(assetHandle);
                }
            }
            assetHandles = null;
        }
    }

    private static readonly List<NewScenePreloadData> allScenePreloadData = [];
    private static bool unloadingScenePreloads;

    private static MonoBehaviour CoroutineManager => StageStutterFixPlugin.Instance;

    public static void Init()
    {
        StageStutterFixPlugin.Logger.LogMessage($"Using better scene preloading with a frame budget of {StageStutterFixPlugin.PreloadBudgetPerFrame}ms");

        var StartNewScenePreload = typeof(NetworkPreloadManager)
            .GetMethod(nameof(NetworkPreloadManager.StartNewScenePreload), [typeof(string), typeof(bool)]);
        new Hook(StartNewScenePreload, StartBetterScenePreload);

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private static void StartBetterScenePreload(string sceneCachedName, bool preserveSceneInMenus)
    {
        if (allScenePreloadData.Exists(x => x.sceneName == sceneCachedName))
        {
            return;
        }
        NewScenePreloadData scenePreloadData = new()
        {
            sceneName = sceneCachedName,
            preserveInMenuScenes = preserveSceneInMenus,
            locationsHandle = Addressables.LoadResourceLocationsAsync(sceneCachedName),
            assetHandles = []
        };
        allScenePreloadData.Add(scenePreloadData);
        scenePreloadData.preloadAssetsCoroutine = CoroutineManager.StartCoroutine(DoScenePreloadCoroutine(scenePreloadData));
    }

    private static IEnumerator DoScenePreloadCoroutine(NewScenePreloadData scenePreloadData)
    {
        var preloadLocations = scenePreloadData.locationsHandle.WaitForCompletion();
        Stopwatch frameStopwatch = new();
        frameStopwatch.Start();
        for (int i = 0; i < preloadLocations.Count; i++)
        {
            if (frameStopwatch.ElapsedMilliseconds >= StageStutterFixPlugin.PreloadBudgetPerFrame)
            {
                yield return null;
                frameStopwatch.Restart();
            }
            scenePreloadData.assetHandles.Add(Addressables.LoadAssetAsync<Object>(preloadLocations[i]));
        }

        scenePreloadData.ReleaseLocations();
    }

    private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if (!unloadingScenePreloads && allScenePreloadData.Count > 0)
        {
            CoroutineManager.StartCoroutine(WaitBeforeUnloadingScenePreloads());
        }
    }

    private static IEnumerator WaitBeforeUnloadingScenePreloads()
    {
        unloadingScenePreloads = true;
        yield return new WaitForSeconds(NetworkPreloadManager.delayBeforeReleasingScenePreloads);
        SceneDef sceneDefForCurrentScene = SceneCatalog.GetSceneDefForCurrentScene();
        bool isMenuScene = sceneDefForCurrentScene == null || sceneDefForCurrentScene.sceneType <= SceneType.Menu;
        for (int i = allScenePreloadData.Count - 1; i >= 0; i--)
        {
            NewScenePreloadData scenePreloadData = allScenePreloadData[i];
            if (!isMenuScene || !scenePreloadData.preserveInMenuScenes)
            {
                scenePreloadData.ReleaseAssets();
                scenePreloadData.ReleaseLocations();
                if (scenePreloadData.preloadAssetsCoroutine != null)
                {
                    CoroutineManager.StopCoroutine(scenePreloadData.preloadAssetsCoroutine);
                }
                allScenePreloadData.RemoveAt(i);
            }
        }
        unloadingScenePreloads = false;
    }
}