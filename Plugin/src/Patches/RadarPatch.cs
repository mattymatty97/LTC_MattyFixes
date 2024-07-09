using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MattyFixes.Patches
{
    [HarmonyPatch]
    internal class RadarPatch
    {
        [HarmonyPatch]
        internal class ItemInShipPatch
        {

            [HarmonyPostfix]
            [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.LateUpdate))]
            [HarmonyPriority(Priority.Last)]
            private static void UpdatePatch(GrabbableObject __instance, bool __runOriginal)
            {
                if (!__runOriginal)
                    return;

                if (__instance.radarIcon != null && __instance.radarIcon.gameObject != null && __instance.isInShipRoom)
                    Object.Destroy(__instance.radarIcon.gameObject);
            }
        }

        [HarmonyPatch]
        internal class DeletedObjectPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(NetworkBehaviour), nameof(NetworkBehaviour.OnDestroy))]
            private static void DestroyPatch(NetworkBehaviour __instance)
            {
                if (!MattyFixes.PluginConfig.Radar.Enabled.Value ||
                    !MattyFixes.PluginConfig.Radar.RemoveDeleted.Value)
                    return;

                var obj = __instance as GrabbableObject;
                if (obj != null && obj.radarIcon != null && obj.radarIcon.gameObject != null)
                    Object.Destroy(obj.radarIcon.gameObject);
            }
        }
        
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
        private static Exception GracefullyHandleExceptionsFromMods(GrabbableObject __instance, Exception __exception)
        {
            if (__exception != null)
            {
                Debug.LogException(__exception);
            }
            return null;
        }
    }
}