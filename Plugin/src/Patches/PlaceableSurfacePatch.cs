using System;
using HarmonyLib;
using UnityEngine;

namespace MattyFixes.Patches;

[HarmonyPatch]
public static class PlaceableSurfacePatch
{
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlaceableObjectsSurface), nameof(PlaceableObjectsSurface.itemPlacementPosition))]
    private static bool ItemPlacementPositionPatch(PlaceableObjectsSurface __instance, ref Vector3 __result,
        Transform gameplayCamera, GrabbableObject heldObject)
    {
        if (!MattyFixes.PluginConfig.CupBoard.Enabled.Value)
            return true;

        try
        {
            if (Physics.Raycast(gameplayCamera.position, gameplayCamera.forward, out var val, 7f,
                    1073744640, QueryTriggerInteraction.Ignore))
            {
                var hitPoint = val.collider.ClosestPointOnBounds(val.point);
                __result = hitPoint + Vector3.up * heldObject.itemProperties.verticalOffset;
                return false;
            }

            __result = Vector3.zero;
            return false;
        }
        catch (Exception ex)
        {
            MattyFixes.Log.LogError($"Exception while finding the Placement {ex}");
            return true;
        }
    }
}