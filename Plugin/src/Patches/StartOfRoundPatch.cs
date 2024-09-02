using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal class StartOfRoundPatch
{
    internal static bool _isInitializingGame = false;
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    private static void MarkServerStart(StartOfRound __instance)
    {
        _isInitializingGame = true;
        __instance.StartCoroutine(WaitCoupleOfFrames());
    }

    private static IEnumerator WaitCoupleOfFrames()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        _isInitializingGame = false;
    }
}