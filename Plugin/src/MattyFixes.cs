using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
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
    internal class MattyFixes : BaseUnityPlugin
    {
        public const string GUID = "mattymatty.MattyFixes";
        public const string NAME = "Matty's Fixes";
        public const string VERSION = "1.1.5";

        internal static MattyFixes INSTANCE { get; private set;}
        internal static ManualLogSource Log;

        private static readonly string[] IncompatibleGUIDs = new string[]
        {
        };

        internal static readonly List<PluginInfo> FoundIncompatibilities = new List<PluginInfo>();
            
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
        
        internal static class PluginConfig
        {
            internal static void Init()
            {
                var config = INSTANCE.Config;;
                //Initialize Configs
                //ReadableMeshes
                ReadableMeshes.Enabled = config.Bind("ReadableMeshes","enabled",true
                    ,"convert all meshes to readable at runtime");
                ReadableMeshes.UseCollider = config.Bind("ReadableMeshes","use_collider",true
                    ,"use a Mesh collider to get more accurate item sizes");
                ReadableMeshes.FixLignting = config.Bind("ReadableMeshes","fix_lighting",true
                    ,"show lighting particles as dev intended! ( will have no effect if AlternateLightningParticles is active )");
                //NameFixes
                NameFixes.Enabled = config.Bind("NameFixes","enabled",true
                    ,"[EXPERIMENTAL] fix late joining players reading as 'Unknown' and radar with wrong names");
                //BadgeFixes
                BadgeFixes.Enabled = config.Bind("BadgeFixes","enabled",true
                    ,"[EXPERIMENTAL] show correct level tag");
                //CupBoard
                CupBoard.Enabled = config.Bind("CupBoard","enabled",true
                    ,"prevent items inside or above the Storage Closet from falling to the ground");
                CupBoard.Tolerance = config.Bind("CupBoard","tolerance",0.05f
                    ,"how loosely \"close\" the items have to be to the top of the closet for them to count X/Z");
                CupBoard.Shift = config.Bind("CupBoard","shift",0.1f
                    ,"how much move the items inside the closet on load ( only if ItemClippingFix disabled )");
                //Radar
                Radar.Enabled = config.Bind("Radar","enabled",true
                    ,"remove orphan radar icons from deleted/collected scrap");
                Radar.RemoveDeleted = config.Bind("Radar","deleted_scrap",true
                    ,"remove orphan radar icons from deleted scrap ( company building )");
                Radar.RemoveOnShip = config.Bind("Radar","ship_loot",true
                    ,"remove orphan radar icons from scrap on the ship in a recently created game");
                //ItemClipping
                ItemClipping.Enabled = config.Bind("ItemClipping","enabled",true
                    ,"fix rotation and height of various items when on the Ground");
                ItemClipping.RotateOnSpawn = config.Bind("ItemClipping","rotate_on_spawn",true
                    ,"fix rotation of newly spawned items");
                ItemClipping.VerticalOffset = config.Bind("ItemClipping","vertical_offset",-0.01f
                    ,"additional y offset for items on the ground");
                ItemClipping.ManualOffsets = config.Bind("ItemClipping","manual_offsets","Comedy:0.085,Tragedy:0.085"
                    ,"y offset for items on the ground");
                //OutOfBounds
                OutOfBounds.Enabled = config.Bind("OutOfBounds","enabled",true
                    ,"prevent items from falling below the ship");
                OutOfBounds.VerticalOffset = config.Bind("OutOfBounds","vertical_offset",0.2f
                    ,"vertical offset to apply to objects on load");
                //AlternateLightningParticles
                LightingParticle.Enabled = config.Bind("AlternateLightningParticles","enabled",false
                    ,"use sphere shape for lightning particles ");

                
                var offsetString = ItemClipping.ManualOffsets.Value;
                foreach (var entry in offsetString.Split(','))
                {
                    var parts = entry.Split(':');
                    if (parts.Length <= 1)
                        continue;

                    var name = parts[0];
                    if (float.TryParse(parts[1], out var value))
                        ItemClipping.ManualOffsetMap.Add(name, value);
                }
                

                if (LethalConfigProxy.Enabled)
                {
                    LethalConfigProxy.AddConfig(ReadableMeshes.Enabled, true);
                    LethalConfigProxy.AddConfig(ReadableMeshes.UseCollider, true);
                    LethalConfigProxy.AddConfig(ReadableMeshes.FixLignting, false);
                    LethalConfigProxy.AddConfig(NameFixes.Enabled, false);
                    LethalConfigProxy.AddConfig(BadgeFixes.Enabled, true);
                    LethalConfigProxy.AddConfig(CupBoard.Enabled, false);
                    LethalConfigProxy.AddConfig(CupBoard.Tolerance, false);
                    LethalConfigProxy.AddConfig(CupBoard.Shift, false);
                    LethalConfigProxy.AddConfig(Radar.Enabled, false);
                    LethalConfigProxy.AddConfig(Radar.RemoveDeleted, false);
                    LethalConfigProxy.AddConfig(Radar.RemoveOnShip, false);
                    LethalConfigProxy.AddConfig(ItemClipping.Enabled, false);
                    LethalConfigProxy.AddConfig(ItemClipping.RotateOnSpawn, false);
                    LethalConfigProxy.AddConfig(ItemClipping.VerticalOffset, false);
                    LethalConfigProxy.AddConfig(ItemClipping.ManualOffsets, true);
                    LethalConfigProxy.AddConfig(OutOfBounds.Enabled, false);
                    LethalConfigProxy.AddConfig(LightingParticle.Enabled, true);
                }
                
            }

            internal static void RemoveOrphans()
            {
                var config = INSTANCE.Config;
                //remove unused options
                PropertyInfo orphanedEntriesProp = config.GetType().GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);

                var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp!.GetValue(config, null);
                
                orphanedEntries.Clear();
                config.Save(); // Save the config file
            }
            
            internal static class ReadableMeshes
            {
                internal static ConfigEntry<bool> Enabled;
                internal static ConfigEntry<bool> UseCollider;
                internal static ConfigEntry<bool> FixLignting;
            }
            
            internal static class NameFixes
            {
                internal static ConfigEntry<bool> Enabled;
            }
            
            internal static class BadgeFixes
            {
                internal static ConfigEntry<bool> Enabled;
            }
            
            internal static class CupBoard
            {
                internal static ConfigEntry<bool> Enabled;
                internal static ConfigEntry<float> Tolerance;
                internal static ConfigEntry<float> Shift;
            }
            internal static class Radar
            {
                internal static ConfigEntry<bool> Enabled;
                internal static ConfigEntry<bool> RemoveDeleted;
                internal static ConfigEntry<bool> RemoveOnShip;
            }
            
            internal static class ItemClipping
            {
                internal static ConfigEntry<bool> Enabled;
                internal static ConfigEntry<bool> RotateOnSpawn;
                internal static ConfigEntry<float> VerticalOffset;
                internal static ConfigEntry<string> ManualOffsets;
                internal static Dictionary<string, float> ManualOffsetMap = new();
                internal static readonly Dictionary<Item, ConfigEntry<string>> ItemRotations = new();
            }
            
            internal static class OutOfBounds
            {
                internal static ConfigEntry<bool> Enabled;
                internal static ConfigEntry<float> VerticalOffset;
            }
            
            internal static class LightingParticle
            {
                internal static ConfigEntry<bool> Enabled;
            }
        }

    }
}