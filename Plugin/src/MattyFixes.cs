using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using MattyFixes.PopUp;
using PluginInfo = BepInEx.PluginInfo;
using MattyFixes.Dependency;

namespace MattyFixes
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("TeamBMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ainavt.lc.lethalconfig", BepInDependency.DependencyFlags.SoftDependency)]
    internal partial class MattyFixes : BaseUnityPlugin
    {
        public const string GUID = "mattymatty.MattyFixes";
        public const string NAME = "Matty's Fixes";
        public const string VERSION = "1.1.10";

        internal static MattyFixes INSTANCE { get; private set;}
        internal static ManualLogSource Log;

        private static readonly string[] IncompatibleGUIDs = new string[]
        {
        };

        internal static readonly List<PluginInfo> FoundIncompatibilities = new List<PluginInfo>();

        internal static void VerboseLog(string logmessage)
        {
            if (PluginConfig.Debug.Verbose.Value)
                Log.LogDebug(logmessage);
        }
            
        private void Awake()
        {
            INSTANCE = this;
            Log = Logger;
            try
            {
                PluginInfo[] incompatibleMods = Chainloader.PluginInfos.Values.Where(p => IncompatibleGUIDs.Contains(p.Metadata.GUID)).ToArray();
                if (incompatibleMods.Length > 0)
                {    
                    FoundIncompatibilities.AddRange(incompatibleMods);
                    foreach (var mod in incompatibleMods)
                    {
                        Log.LogWarning($"{mod.Metadata.Name} is incompatible!");   
                    }
                    Log.LogError($"{incompatibleMods.Length} incompatible mods found! Disabling!");
                    var harmony = new Harmony(GUID);
                    harmony.PatchAll(typeof(PopUpPatch));
                }
                else
                {
                    if (LobbyCompatibilityChecker.Enabled)
                        LobbyCompatibilityChecker.Init();
                                        
                    if (AsyncLoggerProxy.Enabled)
                        AsyncLoggerProxy.WriteEvent(MattyFixes.NAME, "Awake", $"Started");

                    Log.LogInfo("Initializing Configs");

                    PluginConfig.Init();
                    
                    Log.LogInfo("Patching Methods");
                    var harmony = new Harmony(GUID);
                    harmony.PatchAll(Assembly.GetExecutingAssembly());
                    
                    Log.LogInfo(NAME + " v" + VERSION + " Loaded!");
                                        
                    if (AsyncLoggerProxy.Enabled)
                        AsyncLoggerProxy.WriteEvent(MattyFixes.NAME, "Awake", $"Finished");
                }
            }
            catch (Exception ex)
            {
                Log.LogError("Exception while initializing: \n" + ex);
            }
        }
    }
}