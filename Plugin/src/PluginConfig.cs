using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using MattyFixes.Dependency;
using UnityEngine;

namespace MattyFixes;

internal partial class MattyFixes
{
    internal static class PluginConfig
    {
        internal static void Init()
        {
            var config = Instance.Config;
            
            //Initialize Configs
            //ReadableMeshes
            ReadableMeshes.Enabled = config.Bind("ReadableMeshes", "enabled", true
                , "convert all meshes to readable at runtime");
            ReadableMeshes.FixLightning = config.Bind("ReadableMeshes", "fix_lightning", true
                , "show lightning particles as dev intended! ( will have no effect if AlternateLightningParticles is active )");
           //BadgeFixes
            BadgeFixes.Enabled = config.Bind("BadgeFixes", "enabled", true
                , "show correct level tag");
            //CupBoard
            CupBoard.Enabled = config.Bind("CupBoard", "enabled", true
                , "prevent items inside or above the Storage Closet from falling to the ground");
            CupBoard.Tolerance = config.Bind("CupBoard", "tolerance", 0.05f
                , new ConfigDescription("how loosely \"close\" the items have to be to the top of the closet for them to count X/Z", new AcceptableValueRange<float>(0f, 0.5f)));
            CupBoard.Shift = config.Bind("CupBoard", "shift", 0.1f
                , new ConfigDescription("how much move the items inside the closet on load ( only if ItemClippingFix disabled )", new AcceptableValueRange<float>(0f,0.5f)));
            //Radar
            Radar.Enabled = config.Bind("Radar", "enabled", true
                , "remove orphan radar icons from deleted/collected scrap");
            Radar.RemoveDeleted = config.Bind("Radar", "deleted_scrap", true
                , "remove orphan radar icons from deleted scrap ( company building )");
            Radar.RemoveOnShip = config.Bind("Radar", "ship_loot", true
                , "remove orphan radar icons from scrap on the ship in a recently created game");
            //ItemClipping
            ItemClipping.Enabled = config.Bind("ItemClipping", "enabled", true
                , "fix rotation and height of various items when on the Ground");
            ItemClipping.RotateOnSpawn = config.Bind("ItemClipping", "rotate_on_spawn", true
                , "fix rotation of newly spawned items");
            ItemClipping.VerticalOffset = config.Bind("ItemClipping", "vertical_offset", 0f
                , new ConfigDescription("additional y offset for items on the ground", new AcceptableValueRange<float>(-0.5f,0.5f)));
            ItemClipping.ManualOffsets = config.Bind("ItemClipping", "manual_offsets", ""
                , "y offset for items on the ground\nDictionary Format: '[key]:[value],[key2]:[value2]'\neg: `Vanilla/Ammo:0.0`");
            //OutOfBounds
            OutOfBounds.Enabled = config.Bind("OutOfBounds", "enabled", true
                , "prevent items from falling below the ship");
            OutOfBounds.VerticalOffset = config.Bind("OutOfBounds", "vertical_offset", 0.01f
                , new ConfigDescription("vertical offset to apply to objects on load to prevent them from clipping into the floor", new AcceptableValueRange<float>(0.001f,1f)));
            OutOfBounds.SpawnInFurniture = config.Bind("OutOfBounds", "spawn_in_furniture", true
                , "Fix items generating inside furniture ( eg: lamps inside the kitchen counter )");
            //AlternateLightningParticles
            LightingParticle.Enabled = config.Bind("AlternateLightningParticles", "enabled", true
                , "use sphere shape for lightning particles ");
            //VerboseDebug
            Debug.VerboseMeshes = config.Bind("Debug", "Mesh Verbosity Level", LogLevel.None,
                "Print A LOT more logs about Meshes");
            Debug.VerboseCupboard = config.Bind("Debug", "Cupboard Verbosity Level", LogLevel.None,
                "Print A LOT more logs about Cupboard detection");
            Debug.VerboseItems = config.Bind("Debug", "Item Verbosity Level", LogLevel.None,
                "Print A LOT more logs about Cupboard detection");


            var offsetString = ItemClipping.ManualOffsets.Value;
            foreach (var entry in offsetString.Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length <= 1)
                    continue;

                var name = parts[0].Trim();
                if (float.TryParse(parts[1], 
                        NumberStyles.Float | NumberStyles.AllowThousands, 
                        NumberFormatInfo.InvariantInfo, 
                        out var value))
                    ItemClipping.ManualOffsetMap.Add(name, value);
            }


            if (LethalConfigProxy.Enabled)
            {
                LethalConfigProxy.AddButton("Cleanup", "Clear old entries", "remove unused entries in the config file\n(IF RUN FROM MENU WILL DELETE ALL ITEM OFFSETS!!)", "Clean&Save", RemoveOrphans);
                LethalConfigProxy.AddConfig(ReadableMeshes.Enabled, true);
                LethalConfigProxy.AddConfig(ReadableMeshes.FixLightning);
                LethalConfigProxy.AddConfig(BadgeFixes.Enabled, true);
                LethalConfigProxy.AddConfig(CupBoard.Enabled);
                LethalConfigProxy.AddConfig(CupBoard.Tolerance);
                LethalConfigProxy.AddConfig(CupBoard.Shift);
                LethalConfigProxy.AddConfig(Radar.Enabled);
                LethalConfigProxy.AddConfig(Radar.RemoveDeleted);
                LethalConfigProxy.AddConfig(Radar.RemoveOnShip);
                LethalConfigProxy.AddConfig(ItemClipping.Enabled);
                LethalConfigProxy.AddConfig(ItemClipping.RotateOnSpawn);
                LethalConfigProxy.AddConfig(ItemClipping.VerticalOffset);
                LethalConfigProxy.AddConfig(ItemClipping.ManualOffsets, true);
                LethalConfigProxy.AddConfig(OutOfBounds.Enabled, true);
                LethalConfigProxy.AddConfig(OutOfBounds.VerticalOffset);
                LethalConfigProxy.AddConfig(OutOfBounds.SpawnInFurniture, true);
                LethalConfigProxy.AddConfig(LightingParticle.Enabled, true);
                LethalConfigProxy.AddConfig(Debug.VerboseMeshes);
                LethalConfigProxy.AddConfig(Debug.VerboseCupboard);
                LethalConfigProxy.AddConfig(Debug.VerboseItems);
            }
        }

        internal static void RemoveOrphans()
        {
            var config = Instance.Config;
            //remove unused options
            var orphanedEntriesProp = config.GetType()
                .GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);

            var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp!.GetValue(config, null);

            orphanedEntries.Clear();
            config.Save(); // Save the config file
        }
        
        internal static class ReadableMeshes
        {
            internal static ConfigEntry<bool> Enabled;
            internal static ConfigEntry<bool> FixLightning;
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
            internal static readonly Dictionary<string, float> ManualOffsetMap = new(StringComparer.InvariantCultureIgnoreCase);
            internal static readonly Dictionary<Item, ItemRotationConfig> ItemRotations = new();
        }

        internal static class OutOfBounds
        {
            internal static ConfigEntry<bool> Enabled;
            internal static ConfigEntry<float> VerticalOffset;
            internal static ConfigEntry<bool> SpawnInFurniture;
        }

        internal static class LightingParticle
        {
            internal static ConfigEntry<bool> Enabled;
        }

        internal static class Debug
        {
            internal static ConfigEntry<LogLevel> VerboseMeshes;
            internal static ConfigEntry<LogLevel> VerboseCupboard;
            internal static ConfigEntry<LogLevel> VerboseItems;
        }
    }
    
    internal readonly struct ItemRotationConfig(Vector3 original, ConfigEntry<string> config)
    {
        public Vector3 Original { get; } = original;
        public ConfigEntry<string> Config { get; } = config;
    }
}