using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx.Logging;
using HarmonyLib;
using MattyFixes.Dependency;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal class OutOfBoundsItemsFix
{
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
    private static void ShipLeave(RoundManager __instance, bool despawnAllItems)
    {
        if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
            return;

        MattyFixes.VerboseItemsLog(LogLevel.Info, () => "Ship left the planet: starting check for OOB items!");

        var shipTransform = StartOfRound.Instance.elevatorTransform;
        var grabbableObjects = shipTransform.GetComponentsInChildren<GrabbableObject>();

        var shipCollider = StartOfRound.Instance.shipInnerRoomBounds;
        var vehicleCollider = Object.FindObjectOfType<VehicleController>()?.boundsCollider;
        
        MattyFixes.VerboseItemsLog(LogLevel.Debug, () => $"Cruiser? {vehicleCollider != null}");

        var miny = vehicleCollider == null
            ? shipCollider.bounds.min.y
            : Math.Min(shipCollider.bounds.min.y, vehicleCollider.bounds.min.y);
        
        MattyFixes.VerboseItemsLog(LogLevel.Debug, () => $"Bottom Ship is at y? {miny}");
        
        foreach (var item in grabbableObjects)
        {
            if (item.NetworkObject.transform.parent != shipTransform)
            {
                MattyFixes.VerboseItemsLog(LogLevel.Debug, () => $"{item.itemProperties.itemName}({item.NetworkObjectId}) was not parented to the ship. SKIPPING!");
                continue;
            }
            
            MattyFixes.VerboseItemsLog(LogLevel.Debug, () => $"{item.itemProperties.itemName}({item.NetworkObjectId}) in ship room? {item.isInShipRoom}");
            if (!item.isInShipRoom)
                continue;

            var transform = item.transform;
            MattyFixes.VerboseItemsLog(LogLevel.Debug, () => $"{item.itemProperties.itemName}({item.NetworkObjectId}) y position? {transform.position.y}");
            if (transform.position.y >= miny)
                continue;

            MattyFixes.VerboseItemsLog(LogLevel.Info, () => $"{item.itemProperties.itemName}({item.NetworkObjectId}) was found OutOfBounds, teleporting inside!");

            transform.position = shipCollider.bounds.center;
            item.targetFloorPosition = transform.localPosition;
            item.FallToGround();
            MattyFixes.VerboseItemsLog(LogLevel.Debug, () => $"{item.itemProperties.itemName}({item.NetworkObjectId}) new pos: {item.targetFloorPosition}");
        }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveItemsInShip))]
    private static IEnumerable<CodeInstruction> SaveItemsCorrectly(IEnumerable<CodeInstruction> instructions,
        ILGenerator ilGenerator)
    {
        /* Don't patch if this is disabled to avoid conflict with ShaosilGaming's GeneralImprovements mod which
         * also patches SaveItemsInShip to save the items rotation in the save data (FixItemsLoadingSameRotation). */
        if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
            return instructions;

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

        MattyFixes.Log.LogDebug("SaveItemsInShip Patched");
        return matcher.Instructions();
    }

    private static Vector3 ApplyVerticalOffset(GrabbableObject grabbable, Vector3 position)
    {
        if (grabbable.isHeld || grabbable.isHeldByEnemy || !grabbable.hasHitGround)
            return position;

        var newPos = position;
        newPos += Vector3.down * grabbable.itemProperties.verticalOffset;
        newPos += Vector3.up   * MattyFixes.PluginConfig.OutOfBounds.VerticalOffset.Value;
        
        MattyFixes.VerboseItemsLog(LogLevel.Debug, () =>
                $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) fixing saved position pos:{position} newpos:{newPos}");
        
        return newPos;
    }


    [HarmonyTranspiler]
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnScrapInLevel))]
    private static IEnumerable<CodeInstruction> FixSpawns(IEnumerable<CodeInstruction> instructions,
        ILGenerator ilGenerator)
    {
        var codes = instructions.ToList();
        
        if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
            return codes;
        
        if (!MattyFixes.PluginConfig.OutOfBounds.SpawnInFurniture.Value)
            return codes;
        
        var getUpMethod = AccessTools.Property(typeof(Vector3), nameof(Vector3.up)).GetMethod;
        var navmeshPosMethod = AccessTools.Method(typeof(RoundManager), "GetRandomNavMeshPositionInBoxPredictable");
        var verticalOffset = AccessTools.Field(typeof(Item), nameof(Item.verticalOffset));
        var multiplyMethod = AccessTools.Method(typeof(Vector3), "op_Multiply", new []{typeof(Vector3), typeof(float)});
        var addMethod = AccessTools.Method(typeof(Vector3), "op_Addition", new []{typeof(Vector3), typeof(Vector3)});
        
        var matcher = new CodeMatcher(codes, ilGenerator);

        matcher.MatchForward(false, 
            new CodeMatch(OpCodes.Call, navmeshPosMethod),
            new CodeMatch(OpCodes.Call, getUpMethod),
            new CodeMatch(OpCodes.Ldloc_S),
            new CodeMatch(OpCodes.Ldfld),
            new CodeMatch(OpCodes.Ldfld),
            new CodeMatch(OpCodes.Ldloc_S),
            new CodeMatch(OpCodes.Ldfld),
            new CodeMatch(OpCodes.Callvirt),
            new CodeMatch(OpCodes.Ldfld, verticalOffset),
            new CodeMatch(OpCodes.Call, multiplyMethod),
            new CodeMatch(OpCodes.Call, addMethod),
            new CodeMatch(OpCodes.Stloc_S)
            );

        if (matcher.IsInvalid)
        {
            MattyFixes.Log.LogError("RoundManager.SpawnScrapInLevel IL Not Found!");
            return codes;
        }

        matcher.Advance(1).RemoveInstructions(10);
        
        MattyFixes.Log.LogDebug("RoundManager.SpawnScrapInLevel patched");
        //MattyFixes.Log.LogError(string.Join("\n", matcher.Instructions()));

        return matcher.Instructions();
    }
}