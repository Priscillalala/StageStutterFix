using MonoMod.RuntimeDetour;
using RoR2.Networking;

namespace StageStutterFix;

public static class NoPreloadPatch
{
    public static void Init()
    {
        StageStutterFixPlugin.Logger.LogMessage("Disabling scene preloading");

        var StartNewScenePreload = typeof(NetworkPreloadManager)
            .GetMethod(nameof(NetworkPreloadManager.StartNewScenePreload), [typeof(string), typeof(bool)]);
        new Hook(StartNewScenePreload, DoNothing);

        static void DoNothing(string sceneCachedName, bool preserveSceneInMenus) { }
    }
}