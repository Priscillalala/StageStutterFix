using BepInEx.Logging;
using EntityStates;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Networking;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.SceneManagement;

namespace StageStutterFix;

public static class PreloadFixer
{
    private class NewScenePreloadData()
    {
        public string sceneName;
        public bool preserveInMenuScenes;
        public AsyncOperationHandle<IList<IResourceLocation>> locationsHandle;
        public List<AsyncOperationHandle<Object>> assetHandles;
        public Coroutine coroutine;

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

        public void ReleaseLocations()
        {
            if (locationsHandle.IsValid())
            {
                Addressables.Release(locationsHandle);
            }
        }
    }

    private static ManualLogSource Logger;

    //private static readonly int maxPreloadsPerFrame = 1;
    private static readonly long preloadBudgetPerFrame = 1;

    private static readonly List<NewScenePreloadData> allScenePreloadData = [];
    private static bool unloadingScenePreloads;

    public static void Init()
    {
        Logger = StageStutterFixPlugin.Logger;
        On.RoR2.Networking.NetworkPreloadManager.StartNewScenePreload_string_bool += NetworkPreloadManager_StartNewScenePreload_string_bool;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if (!unloadingScenePreloads && allScenePreloadData.Count > 0)
        {
            NetworkPreloadManager.Instance.StartCoroutine(WaitBeforeUnloadingScenePreloads());
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
                Logger.LogMessage($"Unload scene preload: {scenePreloadData.sceneName}");
                scenePreloadData.ReleaseAssets();
                scenePreloadData.ReleaseLocations();
                NetworkPreloadManager.Instance.StopCoroutine(scenePreloadData.coroutine);
                allScenePreloadData.RemoveAt(i);
            }
        }
        unloadingScenePreloads = false;
    }

    private static void NetworkPreloadManager_StartNewScenePreload_string_bool(On.RoR2.Networking.NetworkPreloadManager.orig_StartNewScenePreload_string_bool orig, string sceneCachedName, bool preserveSceneInMenus)
    {
        Logger.LogWarning($"Attempt scene preload: {sceneCachedName} with preserveSceneInMenus = {preserveSceneInMenus}");
        if (preloadBudgetPerFrame <= 0)
        {
            return;
        }
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
        scenePreloadData.coroutine = NetworkPreloadManager.Instance.StartCoroutine(DoScenePreloadCoroutine(scenePreloadData));
    }

    private static IEnumerator DoScenePreloadCoroutine(NewScenePreloadData scenePreloadData)
    {
        var preloadLocations = scenePreloadData.locationsHandle.WaitForCompletion();
        Logger.LogWarning($"preloading {preloadLocations.Count} resource locations");
        Stopwatch frameStopwatch = new();
        frameStopwatch.Start();
        int preloadsThisFrame = 0;
        for (int i = 0; i < preloadLocations.Count; i++)
        {
            if (frameStopwatch.ElapsedMilliseconds >= preloadBudgetPerFrame)
            {
                Logger.LogMessage($"did {preloadsThisFrame} preloads in {frameStopwatch.ElapsedMilliseconds}ms");
                yield return null;
                preloadsThisFrame = 0;
                frameStopwatch.Restart();
            }
            scenePreloadData.assetHandles.Add(Addressables.LoadAssetAsync<Object>(preloadLocations[i]));
            preloadsThisFrame++;
        }

        scenePreloadData.ReleaseLocations();
    }
}

    /*private static IEnumerator DoScenePreloadCoroutineOld(NewScenePreloadData scenePreloadData)
    {
        var preloadLocations = scenePreloadData.locationsHandle.WaitForCompletion();
        Logger.LogWarning($"preloading {preloadLocations.Count} resource locations");
        List<IResourceLocation> preloadLocationsThisFrame = [];
        Stopwatch frameStopwatch = new();
        for (int i = 0; i < preloadLocations.Count; i++)
        {
            preloadLocationsThisFrame.Add(preloadLocations[i]);

        }
        foreach (var location in preloadLocations)
        {
            preloadLocationsThisFrame.Add(location);
            if (preloadLocationsThisFrame.Count >= maxPreloadsPerFrame)
            {
                PreloadAssets();
                preloadLocationsThisFrame.Clear();
                yield return null;
            }
        }
        if (preloadLocationsThisFrame.Count > 0)
        {
            PreloadAssets();
        }

        scenePreloadData.ReleaseLocations();

        void PreloadAssets()
        {
            frameStopwatch.Restart();
            scenePreloadData.assetHandles.Add(Addressables.LoadAssetsAsync<Object>(preloadLocationsThisFrame, null));
            frameStopwatch.Stop();
            //Logger.LogMessage($"{dependenciesCount} deps: {stopwatch.ElapsedMilliseconds}ms");
            if (frameStopwatch.ElapsedMilliseconds > 1)
            {
                Logger.LogWarning($"Frame took {frameStopwatch.ElapsedMilliseconds}ms!");
                foreach (var location in preloadLocationsThisFrame)
                {
                    Logger.LogMessage("Location...");
                    Logger.LogMessage($"type: {location.GetType().FullName}");
                    Logger.LogMessage($"InternalId: {location.InternalId}");
                    Logger.LogMessage($"ProviderId: {location.ProviderId}");
                    Logger.LogMessage($"Dependencies count: {(location.HasDependencies ? location.Dependencies.Count : "None")}");
                    Logger.LogMessage($"Data: {location.Data?.GetType().FullName}");
                    Logger.LogMessage($"Primary Key: {location.PrimaryKey}");
                    Logger.LogMessage($"ResourceType: {location.ResourceType?.FullName}");
                }
            }
        }
    }
}*/

/*foreach (var location in locations)
        {
            Logger.LogMessage("Location...");
            Logger.LogMessage($"type: {location.GetType().FullName}");
            Logger.LogMessage($"InternalId: {location.InternalId}");
            Logger.LogMessage($"ProviderId: {location.ProviderId}");
            Logger.LogMessage($"Dependencies count: {(location.HasDependencies ? location.Dependencies.Count : "None")}");
            Logger.LogMessage($"Data: {location.Data}");
            Logger.LogMessage($"Primary Key: {location.PrimaryKey}");
            Logger.LogMessage($"ResourceType: {location.ResourceType?.FullName}");
        }*/
/*SceneDef sceneDef = SceneCatalog.FindSceneDef(sceneCachedName);
var sceneLocation = Addressables.LoadResourceLocationsAsync(sceneDef.sceneAddress.RuntimeKey).WaitForCompletion().FirstOrDefault();
Logger.LogMessage("Scene Location...");
Logger.LogMessage($"type: {sceneLocation.GetType().FullName}");
Logger.LogMessage($"InternalId: {sceneLocation.InternalId}");
Logger.LogMessage($"ProviderId: {sceneLocation.ProviderId}");
Logger.LogMessage($"Dependencies count: {(sceneLocation.HasDependencies ? sceneLocation.Dependencies.Count : "None")}");
Logger.LogMessage($"Primary Key: {sceneLocation.PrimaryKey}");
Logger.LogMessage($"ResourceType: {sceneLocation.ResourceType?.FullName}");*/
/*if (NetworkPreloadManager.scenePreloadRequestDictionary.ContainsKey(sceneCachedName))
{
    Logger.LogWarning("contains key already!!!");
    return;
}
NetworkPreloadManager.ScenePreloadData scenePreloadData = new()
{
    cachedSceneName = sceneCachedName,
    preserveInMenuScenes = preserveSceneInMenus,
    assetHandle = Addressables.LoadAssetsAsync<Object>(sceneLocation.Dependencies, null)
};
NetworkPreloadManager.scenePreloadRequestDictionary.Add(sceneCachedName, scenePreloadData);*/
//orig(sceneCachedName, preserveSceneInMenus);