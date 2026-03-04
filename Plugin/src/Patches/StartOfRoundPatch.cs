using System;
using System.Collections;
using HarmonyLib;
using MattyFixes.Dependency;
using MattyFixes.Utils;
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

    [HarmonyPrefix]
    [HarmonyAfter("evaisa.lethallib", "imabatby.lethallevelloader", "com.github.teamxiaolan.dawnlib")]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    private static void PopulateModdedCache(StartOfRound __instance, bool __runOriginal)
    {
        if (LethalLibProxy.Enabled)
            LethalLibProxy.PopulateModdedItems();

        if (LethalLevelLoaderProxy.Enabled)
            LethalLevelLoaderProxy.PopulateModdedItems();

        if (DawnLibProxy.Enabled)
            DawnLibProxy.PopulateModdedItems();

        if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value || !__runOriginal)
            return;

        foreach (var item in __instance.allItemsList.itemsList)
        {
            try
            {
                ItemPatches.TryUpdateItemRotation(item);
            }
            catch (Exception ex)
            {
                MattyFixes.Log.LogError($"{item.GetPath()} crashed badly ! {ex}");
            }
        }
    }
}
