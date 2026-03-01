using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using LethalLevelLoader;
using MattyFixes.Interfaces;
using MattyFixes.Utils;

namespace MattyFixes.Dependency;

public static class LethalLevelLoaderProxy
{
    private static bool? _enabled;

    public static bool Enabled
    {
        get
        {
            _enabled ??= Chainloader.PluginInfos.ContainsKey("imabatby.lethallevelloader");
            return _enabled.Value;
        }
    }
    
    // ReSharper disable function SuspiciousTypeConversion.Global
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void PopulateModdedItems()
    {
        
        MattyFixes.Log.LogWarning("LethalLevelLoader found, reading PatchedContent.ExtendedItems");
        foreach (var extendedItem in PatchedContent.ExtendedItems)
        {
            if (((IInjectedItem)extendedItem.Item).MattyFixes_ItemType >= ItemCategory.ItemType.Modded)
                continue;

            if (extendedItem.ContentType == ContentType.Vanilla)
            {
                ((IInjectedItem)extendedItem.Item).MattyFixes_ItemType = ItemCategory.ItemType.Vanilla;
                ((IInjectedItem)extendedItem.Item).MattyFixes_Path = extendedItem.Item.ComputePath("Vanilla");
            }
            else
            {
                ((IInjectedItem)extendedItem.Item).MattyFixes_ItemType = ItemCategory.ItemType.Modded;
                ((IInjectedItem)extendedItem.Item).MattyFixes_Path =
                    extendedItem.Item.ComputePath("LethalLevelLoader", extendedItem.ModName);
            }
        }
    }
}
