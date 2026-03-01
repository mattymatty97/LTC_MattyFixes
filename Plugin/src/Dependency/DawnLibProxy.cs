using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using Dawn;
using MattyFixes.Interfaces;
using MattyFixes.Utils;

namespace MattyFixes.Dependency;

public static class DawnLibProxy
{
    private static bool? _enabled;

    public static bool Enabled
    {
        get
        {
            _enabled ??= Chainloader.PluginInfos.ContainsKey("com.github.teamxiaolan.dawnlib");
            return _enabled.Value;
        }
    }
    
    // ReSharper disable function SuspiciousTypeConversion.Global
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void PopulateModdedItems()
    {
        MattyFixes.Log.LogWarning("DawnLib found, reading LethalContent.Items for modded items");
        foreach (var (key, dawnItemInfo) in LethalContent.Items)
        {
            if (((IInjectedItem)dawnItemInfo.Item).MattyFixes_ItemType >= ItemCategory.ItemType.Modded)
                continue;
            
            if (key.IsVanilla())
            {
                ((IInjectedItem)dawnItemInfo.Item).MattyFixes_ItemType  = ItemCategory.ItemType.Vanilla;
                ((IInjectedItem)dawnItemInfo.Item).MattyFixes_Path      = dawnItemInfo.Item.ComputePath("Vanilla");
            }
            else
            {
                ((IInjectedItem)dawnItemInfo.Item).MattyFixes_ItemType  = ItemCategory.ItemType.Modded;
                ((IInjectedItem)dawnItemInfo.Item).MattyFixes_Path      = dawnItemInfo.Item.ComputePath("DawnLib", key.Namespace);
            }
            
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static ItemCategory.ItemType DefineItem(Item item, out string path)
    {
        var info = item.GetDawnInfo();

        if (info == null)
        {
            path = item.ComputePath("Unknown");
            return ItemCategory.ItemType.Unknown;
        }
        
        if (info.Key.IsVanilla())
        {
            path = item.ComputePath("Vanilla");
            return ItemCategory.ItemType.Vanilla;
        }

        path = item.ComputePath("DawnLib", info.Key.Namespace);
        return ItemCategory.ItemType.Modded;
    }
}
