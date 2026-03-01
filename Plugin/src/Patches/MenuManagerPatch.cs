using System;
using HarmonyLib;
using UnityEngine;
using VertexLibrary;
namespace MattyFixes.Patches;

[HarmonyPatch(typeof(MenuManager))]
internal static class MenuManagerPatch
{
    internal static bool GameHasLoaded;
    
    private static bool _isFirstLoad = true;

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MenuManager.Awake))]
    private static void OnAwake(MenuManager __instance)
    {
        GameHasLoaded = true;
    }
    
    [HarmonyFinalizer]
    [HarmonyPatch(nameof(MenuManager.Start))]
    private static void OnStart(MenuManager __instance)
    {
        if (!_isFirstLoad)
            return;
        _isFirstLoad = false;

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
