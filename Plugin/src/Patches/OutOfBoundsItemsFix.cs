using System;
using HarmonyLib;
using MattyFixes.Dependency;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MattyFixes.Patches
{
    [HarmonyPatch]
    internal class OutOfBoundsItemsFix
    {


        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadUnlockables))]
        private static void CorrectlyPlaceAllUnlockables(StartOfRound __instance)
        {
            foreach (var placeableObject in Object.FindObjectsOfType<AutoParentToShip>())
            {
                placeableObject.MoveToOffset();
            }
            
            Physics.SyncTransforms();
        }
        
        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
        internal class ObjectCreationPatch
        {
            private static void Prefix(GrabbableObject __instance, out bool __state)
            {
                __state = __instance.itemProperties.itemSpawnsOnGround;
                
                if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
                    return;
                
                if (__instance is ClipboardItem || (__instance is PhysicsProp && __instance.itemProperties.itemName == "Sticky note"))
                    return;
                
                if (!GameNetworkManager.Instance.gameHasStarted)
                {
                    __instance.itemProperties.itemSpawnsOnGround = __instance.IsServer;
                }
                
                if (__instance.IsServer || GameNetworkManager.Instance.gameHasStarted)
                {
                    if (__instance.scrapPersistedThroughRounds)
                        __instance.transform.position -= Vector3.down * __instance.itemProperties.verticalOffset;
                    
                    if (__instance.itemProperties.itemSpawnsOnGround)
                        __instance.transform.position += Vector3.up * MattyFixes.PluginConfig.OutOfBounds.VerticalOffset.Value;
                }
            
            }
        
            private static void Postfix(GrabbableObject __instance, bool __state)
            {
                __instance.itemProperties.itemSpawnsOnGround = __state;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
        private static void ShipLeave(RoundManager __instance, bool despawnAllItems)
        {
            if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
                return;

            if (AsyncLoggerProxy.Enabled)
                AsyncLoggerProxy.WriteEvent(MattyFixes.NAME, "ShipLeave", $"Called");
            
            GrabbableObject[] objectsOfType = Object.FindObjectsOfType<GrabbableObject>();

            var shipCollider = StartOfRound.Instance.shipInnerRoomBounds;
            var vehicleCollider = Object.FindObjectOfType<VehicleController>()?.boundsCollider;
            
            var miny = vehicleCollider == null ? shipCollider.bounds.min.y : 
                Math.Min(shipCollider.bounds.min.y, vehicleCollider.bounds.min.y);

            foreach (var item in objectsOfType)
            {
                if (!item.isInShipRoom)
                    continue;
                
                var transform = item.transform;
                if (transform.position.y >= miny)
                    continue;
                
                transform.position = shipCollider.bounds.center;
                item.targetFloorPosition = transform.localPosition;
                item.FallToGround();
            }
        }
    }
}