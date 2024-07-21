using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MattyFixes.Dependency;

namespace MattyFixes;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInDependency("TeamBMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("ainavt.lc.lethalconfig", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("imabatby.lethallevelloader", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("evaisa.lethallib", BepInDependency.DependencyFlags.SoftDependency)]
internal partial class MattyFixes : BaseUnityPlugin
{
    public const string GUID = "mattymatty.MattyFixes";
    public const string NAME = "Matty's Fixes";
    public const string VERSION = "1.1.12";
    internal static ManualLogSource Log;
    
    internal static MattyFixes Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        Log = Logger;
        try
        {
        
            if (LobbyCompatibilityChecker.Enabled)
                LobbyCompatibilityChecker.Init();

            if (AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(NAME, "Awake", "Started");

            Log.LogInfo("Initializing Configs");

            PluginConfig.Init();

            Log.LogInfo("Patching Methods");
            var harmony = new Harmony(GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.LogInfo(NAME + " v" + VERSION + " Loaded!");

            if (AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(NAME, "Awake", "Finished");
            
        }
        catch (Exception ex)
        {
            Log.LogError("Exception while initializing: \n" + ex);
        }
    }

    internal static void VerboseLog(string logmessage)
    {
        if (PluginConfig.Debug.Verbose.Value)
            Log.LogDebug(logmessage);
    }
}