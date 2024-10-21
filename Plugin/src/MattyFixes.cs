using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MattyFixes.Dependency;
using UnityEngine;
using LogType = VertexLibrary.LogType;

namespace MattyFixes;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInDependency("com.github.lethalcompanymodding.vertexlibrary", "1.0.0")]
[BepInDependency("TeamBMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("ainavt.lc.lethalconfig", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("imabatby.lethallevelloader", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("evaisa.lethallib", BepInDependency.DependencyFlags.SoftDependency)]
internal partial class MattyFixes : BaseUnityPlugin
{
    public const string GUID = "mattymatty.MattyFixes";
    public const string NAME = "Matty's Fixes";
    public const string VERSION = "1.1.29";
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


            Log.LogInfo("Initializing Configs");

            PluginConfig.Init();

            Log.LogInfo("Patching Methods");
            var harmony = new Harmony(GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.LogInfo(NAME + " v" + VERSION + " Loaded!");
            
        }
        catch (Exception ex)
        {
            Log.LogError("Exception while initializing: \n" + ex);
        }
    }


    internal static void VerboseMeshLog(VertexLibrary.LogType logLevel, Func<string> message)
    {
        var level = logLevel switch
        {
            LogType.Fatal => LogLevel.Fatal,
            LogType.Error => LogLevel.Error,
            LogType.Warning => LogLevel.Warning,
            LogType.Info1 or LogType.Info2 or LogType.Info3 or LogType.Info4 or LogType.Info=> LogLevel.Info,
            LogType.Debug1 or LogType.Debug2 or LogType.Debug3 or LogType.Debug4 or LogType.Debug=> LogLevel.Debug,
            LogType.All => LogLevel.All,
            _ => LogLevel.None
        };
        VerboseMeshLog(level, message);
    }
    
    internal static void VerboseMeshLog(LogLevel logLevel, Func<string> message)
    {
        if ((PluginConfig.Debug.VerboseMeshes.Value & logLevel) != 0)
            Log.Log(logLevel, message());
    }
    
    internal static void VerboseCupboardLog(LogLevel logLevel, Func<string> message)
    {
        if ((PluginConfig.Debug.VerboseCupboard.Value & logLevel) != 0)
            Log.Log(logLevel, message());
    }
    
    internal static void VerboseItemsLog(LogLevel logLevel, Func<string> message)
    {
        if ((PluginConfig.Debug.VerboseItems.Value & logLevel) != 0)
            Log.Log(logLevel, message());
    }
}