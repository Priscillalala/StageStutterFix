using BepInEx;
using BepInEx.Logging;
using HG.Reflection;
using RoR2;
using RoR2.ContentManagement;
using System.Security;
using System.Security.Permissions;
using Path = System.IO.Path;

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

    public static new ManualLogSource Logger { get; private set; }
    public static string RuntimeDirectory { get; private set; }

    private void Awake()
    {
        Logger = base.Logger;
        RuntimeDirectory = Path.GetDirectoryName(Info.Location);

        PreloadFixer.Init();
    }
}
