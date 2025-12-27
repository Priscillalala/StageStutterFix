using BepInEx;
using BepInEx.Logging;
using HG.Reflection;
using System.Security;
using System.Security.Permissions;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete
[module: UnverifiableCode]
[assembly: SearchableAttribute.OptIn]

namespace StageStutterFix;

[BepInPlugin(GUID, NAME, VERSION)]
public class StageStutterFixPlugin : BaseUnityPlugin
{
    public const string
        GUID = "groovesalad." + NAME,
        NAME = "StageStutterFix",
        VERSION = "1.0.0";

    public static StageStutterFixPlugin Instance { get; private set; }
    public static new ManualLogSource Logger { get; private set; }
    public static long PreloadBudgetPerFrame { get; private set; }

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;
        PreloadBudgetPerFrame = Config.Bind(
            "Stage Stutter Fix", 
            "Max preload time per frame", 
            1L, 
            "The ideal maximum time per frame (in milliseconds) to spend preloading the next stage. A value of zero or less will disable stage preloading"
            ).Value;
        if (PreloadBudgetPerFrame > 0)
        {
            BetterPreloadPatch.Init();
        }
        else
        {
            NoPreloadPatch.Init();
        }
    }
}
