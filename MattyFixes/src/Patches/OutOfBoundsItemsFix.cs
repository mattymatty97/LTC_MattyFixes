﻿using System;
using HarmonyLib;
using MattyFixes.Dependency;
using MattyFixes.Patches.Utility;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MattyFixes.Patches
{
    [HarmonyPatch]
    internal class OutOfBoundsItemsFix
    {
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkBehaviour), nameof(NetworkBehaviour.OnNetworkSpawn))]
        private static void ObjectCreation(NetworkBehaviour __instance)
        {
            if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
                return;
            
            if (__instance.transform.name == "ClipboardManual" || __instance.transform.name == "StickyNoteItem")
                return;

            if (!(__instance is GrabbableObject obj))
                return;

            if (!StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(__instance.transform.position) && !obj.isInShipRoom)
                return;
            
            if (StartOfRound.Instance.localPlayerController != null && !StartOfRound.Instance.localPlayerController.justConnected)
                GrabbableObjectUtility.AppendToHolder(obj, nameof(CupBoardFix), (int)GrabbableObjectUtility.DelayValues.OutOfBounds, UpdateCallback);
            else if(obj.IsServer)
                GrabbableObjectUtility.AppendToHolder(obj,nameof(OutOfBoundsItemsFix), (int)GrabbableObjectUtility.DelayValues.OutOfBoundsServer, UpdateCallback);
            else
                GrabbableObjectUtility.AppendToHolder(obj,nameof(OutOfBoundsItemsFix), (int)GrabbableObjectUtility.DelayValues.OutOfBoundsClient, UpdateCallback);
        }

        private static void UpdateCallback(GrabbableObject __instance,
            GrabbableObjectUtility.UpdateHolder updateHolder)
        {
            var collider = StartOfRound.Instance.shipInnerRoomBounds;

            var closet = GameObject.Find("/Environment/HangarShip/StorageCloset");
            var transform = __instance.transform;

            if (transform.parent != closet.transform)
            {
                var position = updateHolder.OriginalPos;
                position += Vector3.up * MattyFixes.PluginConfig.OutOfBounds.VerticalOffset.Value;
                position += Vector3.down * Math.Min(0, __instance.itemProperties.verticalOffset);
                
                if (position.y < collider.bounds.min.y)
                    position = collider.bounds.center;
                
                transform.position = position;
                __instance.targetFloorPosition = transform.localPosition;
                                
                __instance.FallToGround();
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

            var collider = StartOfRound.Instance.shipInnerRoomBounds;

            foreach (var item in objectsOfType)
            {
                if (!item.isInShipRoom)
                    continue;
                var transform = item.transform;
                if (transform.position.y < collider.bounds.min.y)
                {
                    transform.position = collider.bounds.center;
                    item.targetFloorPosition = transform.localPosition;
                    item.FallToGround();
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.OnHitGround))]
        private static void AfterFall(GrabbableObject __instance)
        {
            if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
                return;
            
            if (!__instance.isInShipRoom)
                return;

            var collider = StartOfRound.Instance.shipInnerRoomBounds;
            
            var transform = __instance.transform;
            if (transform.position.y < collider.bounds.min.y)
            {
                transform.position = collider.bounds.center;
                __instance.targetFloorPosition = transform.localPosition;
                __instance.FallToGround();
            }
        }
    }
}