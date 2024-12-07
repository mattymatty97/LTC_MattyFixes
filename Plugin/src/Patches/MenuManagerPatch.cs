using System;
using HarmonyLib;
using UnityEngine;
using VertexLibrary;

namespace MattyFixes.Patches;

[HarmonyPatch(typeof(MenuManager))]
internal static class MenuManagerPatch
{

    private static bool _runOnce;

    [HarmonyFinalizer]
    [HarmonyPatch(nameof(MenuManager.Start))]
    private static void OnStart(MenuManager __instance)
    {
        if (_runOnce)
            return;
        _runOnce = true;

        try
        {
            //cache vertexes for all known items
            var items = Resources.FindObjectsOfTypeAll<GrabbableObject>();

            MattyFixes.Log.LogWarning($"Caching vertexes for {items.Length} items!");
            foreach (var item in items)
            {
                item.transform.CacheVertexes(new ExecutionOptions()
                {
                    CullingMask = MattyFixes.VisibleLayerMask,
                    LogHandler = MattyFixes.VerboseMeshLog,
                    VertexCache = VertexesExtensions.GlobalPartialCache
                });
            }
        }
        catch (Exception ex)
        {
            MattyFixes.Log.LogFatal($"Exception while caching items: {ex}");
        }
    }
    
}
