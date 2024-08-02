using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using MattyFixes.Dependency;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal class OutOfBoundsItemsFix
{
    private static bool _isInitializingGame = false;
    
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
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadUnlockables))]
    private static void CorrectlyPlaceAllUnlockables(StartOfRound __instance)
    {
        foreach (var placeableObject in Object.FindObjectsOfType<AutoParentToShip>()) placeableObject.MoveToOffset();

        Physics.SyncTransforms();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
    private static void ShipLeave(RoundManager __instance, bool despawnAllItems)
    {
        if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
            return;

        if (AsyncLoggerProxy.Enabled)
            AsyncLoggerProxy.WriteEvent(MattyFixes.NAME, "ShipLeave", "Called");

        var objectsOfType = Object.FindObjectsOfType<GrabbableObject>();

        var shipCollider = StartOfRound.Instance.shipInnerRoomBounds;
        var vehicleCollider = Object.FindObjectOfType<VehicleController>()?.boundsCollider;

        var miny = vehicleCollider == null
            ? shipCollider.bounds.min.y
            : Math.Min(shipCollider.bounds.min.y, vehicleCollider.bounds.min.y);

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

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveItemsInShip))]
    private static IEnumerable<CodeInstruction> SaveItemsCorrectly(IEnumerable<CodeInstruction> instructions,
        ILGenerator ilGenerator)
    {
        var codes = instructions.ToList();
        var newOffsetMethod = AccessTools.Method(typeof(OutOfBoundsItemsFix), nameof(ApplyVerticalOffset));
        var getTransformMethod = AccessTools.Property(typeof(Component), nameof(Component.transform)).GetMethod;
        var getPositionMethod = AccessTools.Property(typeof(Transform), nameof(Transform.position)).GetMethod;

        var matcher = new CodeMatcher(codes, ilGenerator);

        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldloc_2),
            new CodeMatch(OpCodes.Ldloc_0),
            new CodeMatch(OpCodes.Ldloc_S),
            new CodeMatch(OpCodes.Ldelem_Ref),
            new CodeMatch(OpCodes.Callvirt, getTransformMethod),
            new CodeMatch(OpCodes.Callvirt, getPositionMethod)
        );

        if (matcher.IsInvalid)
        {
            MattyFixes.Log.LogError("Cannot patch SaveItemsInShip");
            MattyFixes.Log.LogDebug(string.Join("\n", codes));
            return codes;
        }

        matcher.Advance(4);
        matcher.Insert(new CodeInstruction(OpCodes.Dup));
        matcher.Advance(3);
        matcher.Insert(new CodeInstruction(OpCodes.Call, newOffsetMethod));

        MattyFixes.Log.LogInfo("SaveItemsInShip Patched");
        return matcher.Instructions();
    }

    private static Vector3 ApplyVerticalOffset(GrabbableObject grabbable, Vector3 position)
    {
        if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
            return position;
        
        if (grabbable.isHeld || grabbable.isHeldByEnemy || !grabbable.hasHitGround)
            return position;
        
        var newPos = position + Vector3.down * grabbable.itemProperties.verticalOffset;
        newPos += Vector3.up * MattyFixes.PluginConfig.OutOfBounds.VerticalOffset.Value;
        
        if (MattyFixes.PluginConfig.Debug.Verbose.Value)
            MattyFixes.Log.LogDebug(
                $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) fixing saved position pos:{position} newpos:{newPos}");
        return newPos;
    }

    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
    internal class ObjectCreationPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(GrabbableObject __instance, out bool __state)
        {
            __state = __instance.itemProperties.itemSpawnsOnGround;

            if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value && !MattyFixes.PluginConfig.CupBoard.Enabled.Value)
                return;

            //only run patch on join ( playerObject not yet assigned ) or if server is loading
            if (StartOfRound.Instance.localPlayerController && !_isInitializingGame)
                return;
            
            if (__instance is ClipboardItem ||
                (__instance is PhysicsProp && __instance.itemProperties.itemName == "Sticky note"))
                return;

            if (MattyFixes.PluginConfig.Debug.Verbose.Value)
                MattyFixes.Log.LogDebug(
                    $"{__instance.itemProperties.itemName}({__instance.NetworkObjectId}) processing GrabbableObject Prefix");


            if (MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
            {
                __instance.itemProperties.itemSpawnsOnGround = __instance.IsServer;
            }

            if (MattyFixes.PluginConfig.CupBoard.Enabled.Value)
            {
                if (CupBoardFix.Closet.gameObject &&
                    __instance.transform.parent == CupBoardFix.Closet.gameObject.transform)
                    __instance.itemProperties.itemSpawnsOnGround = false;
            }
        }

        [HarmonyPriority(Priority.First)]
        private static void Postfix(GrabbableObject __instance, bool __state)
        {
            __instance.itemProperties.itemSpawnsOnGround = __state;
        }
    }
}