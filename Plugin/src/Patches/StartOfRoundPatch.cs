using System;
using System.Collections;
using HarmonyLib;
using MattyFixes.Dependency;
using MonoMod.RuntimeDetour;
using RuntimeIcons.Utils;
using UnityEngine;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal class StartOfRoundPatch
{
    internal static bool IsInitializingGame = false;
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    private static void MarkServerStart(StartOfRound __instance)
    {
        IsInitializingGame = true;
        __instance.StartCoroutine(WaitCoupleOfFrames());
    }

    private static IEnumerator WaitCoupleOfFrames()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        IsInitializingGame = false;
    }
    
    internal static void Init()
    {
        MattyFixes.Hooks.Add(new Hook(AccessTools.Method(typeof(StartOfRound), nameof(StartOfRound.Awake)),
            PrepareItemCache));
    }

    private static void PrepareItemCache(Action<StartOfRound> orig, StartOfRound __instance)
    {
        ItemCategory.ItemModMap.Clear();

        ItemCategory.VanillaItems ??= __instance.allItemsList.itemsList.ToArray();

        foreach (var itemType in ItemCategory.VanillaItems) ItemCategory.ItemModMap.TryAdd(itemType, ("Vanilla", ""));

        orig(__instance);
    }

    [HarmonyPrefix]
    [HarmonyAfter("imabatby.lethallevelloader")]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    private static void PopulateModdedCache(StartOfRound __instance)
    {
        if (LethalLibProxy.Enabled)
            LethalLibProxy.GetModdedItems(in ItemCategory.ItemModMap);

        if (LethalLevelLoaderProxy.Enabled)
            LethalLevelLoaderProxy.GetModdedItems(in ItemCategory.ItemModMap);

        foreach (var itemType in __instance.allItemsList.itemsList)
        {
            ItemCategory.ItemModMap.TryAdd(itemType, ("Unknown", ""));
        }
    }
}
