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
            var category = ((IInjectedItem)extendedItem.Item).MattyFixes_ItemCategory;
            if (category is { Type: >= ItemCategory.ItemType.Modded})
                continue;

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (extendedItem.ContentType)
            {
                case ContentType.Vanilla:
                    ((IInjectedItem)extendedItem.Item).MattyFixes_ItemCategory = new ItemCategory.ItemCategoryHolder(
                        extendedItem.Item,
                        ItemCategory.ItemType.Vanilla,
                        ItemCategory.ItemLibrary.Vanilla,
                        ItemCategory.ComputePath(ItemCategory.ItemLibrary.Vanilla, extendedItem.Item.itemName ?? extendedItem.Item.name)
                    );
                    break;
                case ContentType.Custom:
                    ((IInjectedItem)extendedItem.Item).MattyFixes_ItemCategory = new ItemCategory.ItemCategoryHolder(
                        extendedItem.Item,
                        ItemCategory.ItemType.Modded,
                        ItemCategory.ItemLibrary.LethalLevelLoader,
                        ItemCategory.ComputePath(ItemCategory.ItemLibrary.LethalLevelLoader, extendedItem.ModName, extendedItem.Item.itemName ?? extendedItem.Item.name)
                    );
                    break;
            }
        }
    }
}
