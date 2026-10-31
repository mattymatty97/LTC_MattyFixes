using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using Dawn;
using Dusk;
using HarmonyLib;
using MattyFixes.Interfaces;
using MattyFixes.Utils;
using MonoMod.Utils;
using UnityEngine;

namespace MattyFixes.Dependency;

public static class DawnLibProxy
{
    private static bool? _enabled;

    private static FastReflectionDelegate _setVariantVerticalOffset  = null!;
    private static FastReflectionDelegate _setVariantRestingRotation = null!;
    private static FastReflectionDelegate _setVariantFloorYOffset    = null!;

    public static bool Enabled
    {
        get
        {
            _enabled ??= Chainloader.PluginInfos.ContainsKey("com.github.teamxiaolan.dawnlib");
            return _enabled.Value;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void Init()
    {
        _setVariantVerticalOffset ??= AccessTools.PropertySetter(typeof(DuskItemReplacementDefinition), nameof(DuskItemReplacementDefinition.VerticalOffset)).CreateFastDelegate();
        _setVariantRestingRotation ??= AccessTools.PropertySetter(typeof(DuskItemReplacementDefinition), nameof(DuskItemReplacementDefinition.RestingRotation)).CreateFastDelegate();
        _setVariantFloorYOffset ??= AccessTools.PropertySetter(typeof(DuskItemReplacementDefinition), nameof(DuskItemReplacementDefinition.FloorYOffset)).CreateFastDelegate();
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
                ((IInjectedItem)dawnItemInfo.Item).MattyFixes_Path      = ItemCategory.ComputePath(ItemCategory.ItemLibrary.Vanilla, dawnItemInfo.Item.itemName ?? dawnItemInfo.Item.name);
            }
            else if (!dawnItemInfo.HasTag(DawnLibTags.IsExternal))
            {
                ((IInjectedItem)dawnItemInfo.Item).MattyFixes_ItemType  = ItemCategory.ItemType.Modded;
                ((IInjectedItem)dawnItemInfo.Item).MattyFixes_Path      = ItemCategory.ComputePath(ItemCategory.ItemLibrary.DawnLib, key.Namespace, key.Key);
            }

        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static ItemCategory.ItemType DefineItem(Item item, out string path)
    {
        var info = item.GetDawnInfo();

        //Note: while this is marked as never null, it indeed can be null
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (info == null || info.HasTag(DawnLibTags.IsExternal))
        {
            path = ItemCategory.ComputePath(ItemCategory.ItemLibrary.Unknown, item.itemName ?? item.name);
            return ItemCategory.ItemType.Unknown;
        }

        if (info.Key.IsVanilla())
        {
            path = ItemCategory.ComputePath(ItemCategory.ItemLibrary.Vanilla, item.itemName ?? item.name);
            return ItemCategory.ItemType.Vanilla;
        }

        path = ItemCategory.ComputePath(ItemCategory.ItemLibrary.DawnLib, info.Key.Namespace, info.Key.Key);
        return ItemCategory.ItemType.Modded;
    }

    public static bool IsDawnLibItem(Item item)
    {
        return ((IInjectedItem)item).MattyFixes_Library == ItemCategory.ItemLibrary.DawnLib;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static IItemVariant[] GetItemVariants(Item item)
    {
        var itemInfo = item.GetDawnInfo();
        if (itemInfo?.CustomData.TryGet<List<DuskItemReplacementDefinition>>(DuskKeys.EntityReplacements, out var replacementList) ?? false)
        {
            return [.. replacementList.Select(replacement => new DawnItemVariant(item, replacement))];
        }
        return [((IInjectedItem)item).MattyFixes_ItemVariant];
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static IItemVariant GetItemVariant(GrabbableObject item)
    {
        return item.TryGetGrabbableObjectReplacement(out var variant) ? new DawnItemVariant(item.itemProperties, variant) : ((IInjectedItem)item).MattyFixes_ItemVariant;
    }

    public sealed class DawnItemVariant(Item itemProperties, DuskItemReplacementDefinition replacementDefinition) : IItemVariant
    {
        public DuskItemReplacementDefinition ReplacementDefinition => replacementDefinition;

        public Item ItemProperties => itemProperties;

        public ItemCategory.ItemType Type => ItemCategory.ItemType.Modded;
        public ItemCategory.ItemLibrary Library => ItemCategory.ItemLibrary.DawnLib;

        public string Identifier { get; } = replacementDefinition.Key.IsVanilla() ?
            ItemCategory.ComputePath(ItemCategory.ItemLibrary.Vanilla, replacementDefinition.Key.Key) :
            ItemCategory.ComputePath(ItemCategory.ItemLibrary.DawnLib, replacementDefinition.Key.Namespace, replacementDefinition.Key.Key);
        public Vector3 RestingRotation
        {
            get => ReplacementDefinition.RestingRotation;
            set => _setVariantRestingRotation.Invoke(ReplacementDefinition, value);
        }

        public int FloorYOffset
        {
            get => ReplacementDefinition.FloorYOffset;
            set => _setVariantFloorYOffset.Invoke(ReplacementDefinition, value);
        }

        public float VerticalOffset
        {
            get => ReplacementDefinition.VerticalOffset;
            set => _setVariantVerticalOffset.Invoke(ReplacementDefinition, value);
        }
    }
}
