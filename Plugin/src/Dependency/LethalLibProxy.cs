using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using LethalLib.Modules;
using MattyFixes.Interfaces;
using MattyFixes.Utils;

namespace MattyFixes.Dependency;

public static class LethalLibProxy
{
    private static bool? _enabled;

    public static bool Enabled
    {
        get
        {
            _enabled ??= Chainloader.PluginInfos.ContainsKey("evaisa.lethallib");
            return _enabled.Value;
        }
    }

    // ReSharper disable function SuspiciousTypeConversion.Global
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void PopulateModdedItems()
    {
        MattyFixes.Log.LogWarning("LethalLib found, reading Items.scrapItems");
        foreach (var scrapItem in Items.scrapItems) RegisterItem(scrapItem.item, scrapItem.modName);
        foreach (var scrapItem in Items.plainItems) RegisterItem(scrapItem.item, scrapItem.modName);
        foreach (var scrapItem in Items.shopItems)  RegisterItem(scrapItem.item, scrapItem.modName);
        return;

        void RegisterItem(Item item, string modName)
        {
            if (((IInjectedItem)item).MattyFixes_ItemType >= ItemCategory.ItemType.Modded)
                return;
            
            ((IInjectedItem)item).MattyFixes_ItemType = ItemCategory.ItemType.Modded;
            ((IInjectedItem)item).MattyFixes_Path     = item.ComputePath("LethalLib", modName);
        }
    }

}
