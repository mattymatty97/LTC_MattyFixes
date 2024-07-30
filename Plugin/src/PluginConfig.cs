using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Configuration;
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
            ;
            //Initialize Configs
            //ReadableMeshes
            ReadableMeshes.Enabled = config.Bind("ReadableMeshes", "enabled", true
                , "convert all meshes to readable at runtime");
            ReadableMeshes.FixLightning = config.Bind("ReadableMeshes", "fix_lightning", true
                , "show lightning particles as dev intended! ( will have no effect if AlternateLightningParticles is active )");
            //NameFixes
            NameFixes.Enabled = config.Bind("NameFixes", "enabled", true
                , "[EXPERIMENTAL] fix late joining players reading as 'Unknown' and radar with wrong names");
            //BadgeFixes
            BadgeFixes.Enabled = config.Bind("BadgeFixes", "enabled", true
                , "show correct level tag");
            //CupBoard
            CupBoard.Enabled = config.Bind("CupBoard", "enabled", true
                , "prevent items inside or above the Storage Closet from falling to the ground");
            CupBoard.Tolerance = config.Bind("CupBoard", "tolerance", 0.05f
                , "how loosely \"close\" the items have to be to the top of the closet for them to count X/Z");
            CupBoard.Shift = config.Bind("CupBoard", "shift", 0.1f
                , "how much move the items inside the closet on load ( only if ItemClippingFix disabled )");
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
            ItemClipping.VerticalOffset = config.Bind("ItemClipping", "vertical_offset", 0.01f
                , "additional y offset for items on the ground");
            ItemClipping.ManualOffsets = config.Bind("ItemClipping", "manual_offsets", "Comedy:0.085,Tragedy:0.085"
                , "y offset for items on the ground");
            //OutOfBounds
            OutOfBounds.Enabled = config.Bind("OutOfBounds", "enabled", true
                , "prevent items from falling below the ship");
            OutOfBounds.VerticalOffset = config.Bind("OutOfBounds", "vertical_offset", 0.2f
                , "vertical offset to apply to objects on load");
            //AlternateLightningParticles
            LightingParticle.Enabled = config.Bind("AlternateLightningParticles", "enabled", true
                , "use sphere shape for lightning particles ");
            //CruiserFixes
            CruiserFixes.Enabled = config.Bind("CruiserFixes", "enabled", true
                , "global toggle for cruiser patches");
            CruiserFixes.AlternateItemDrop = config.Bind("CruiserFixes", "alternate_item_drop", true
                , "global toggle for cruiser patches");
            //VerboseDebug
            Debug.Verbose = config.Bind("Debug", "verbose", false
                , "print more logs!");
            
            //Compatibility
            //GeneralImprovements
            Compatibility.GeneralImprovements.FixItemsLoadingSameRotation = config.Bind("Compatibility.GeneralImprovements", "FixItemsLoadingSameRotation", false
                , "maintain restored rotation from GeneralImprovements");
            //SmartItemSaving
            Compatibility.SmartItemSaving.SaveItemRotation = config.Bind("Compatibility.SmartItemSaving", "SaveItemRotation", false
                , "maintain restored rotation from SmartItemSaving");
            


            var offsetString = ItemClipping.ManualOffsets.Value;
            foreach (var entry in offsetString.Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length <= 1)
                    continue;

                var name = parts[0];
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
                LethalConfigProxy.AddConfig(NameFixes.Enabled);
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
                LethalConfigProxy.AddConfig(OutOfBounds.Enabled);
                LethalConfigProxy.AddConfig(LightingParticle.Enabled, true);
                LethalConfigProxy.AddConfig(CruiserFixes.Enabled);
                LethalConfigProxy.AddConfig(CruiserFixes.AlternateItemDrop);
                LethalConfigProxy.AddConfig(Debug.Verbose);
                LethalConfigProxy.AddConfig(Compatibility.GeneralImprovements.FixItemsLoadingSameRotation);
                LethalConfigProxy.AddConfig(Compatibility.SmartItemSaving.SaveItemRotation);
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
            internal static readonly Dictionary<string, float> ManualOffsetMap = new();
            internal static readonly Dictionary<Item, ItemRotationConfig> ItemRotations = new();
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

        internal static class CruiserFixes
        {
            internal static ConfigEntry<bool> Enabled;
            internal static ConfigEntry<bool> AlternateItemDrop;
        }

        internal static class Debug
        {
            internal static ConfigEntry<bool> Verbose;
        }
        
        internal static class Compatibility
        {
            internal static class GeneralImprovements
            {
                internal static ConfigEntry<bool> FixItemsLoadingSameRotation;
            }
            
            internal static class SmartItemSaving
            {
                internal static ConfigEntry<bool> SaveItemRotation;
            }
        }
    }
    
    internal readonly struct ItemRotationConfig(Vector3 original, ConfigEntry<string> config)
    {
        public Vector3 Original { get; } = original;
        public ConfigEntry<string> Config { get; } = config;
    }
}