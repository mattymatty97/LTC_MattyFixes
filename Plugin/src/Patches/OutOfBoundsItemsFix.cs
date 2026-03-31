using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx.Logging;
using HarmonyLib;
using MattyFixes.Dependency;
using MattyFixes.Utils.IL;
using UnityEngine;
using UnityEngine.AI;
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

        var miny = shipCollider.bounds.min.y;

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
    [HarmonyAfter("ShaosilGaming.GeneralImprovements")]
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveItemsInShip))]
    private static IEnumerable<CodeInstruction> SaveItemsCorrectly(IEnumerable<CodeInstruction> instructions,
        ILGenerator ilGenerator)
    {
        var codes = instructions.ToList();
        var newOffsetMethod = typeof(OutOfBoundsItemsFix).GetMethod(nameof(GetAdjustedPosition), AccessTools.all);
        var getTransformMethod = typeof(Component).GetProperty(nameof(Component.transform), AccessTools.all)?.GetMethod;
        var getPositionMethod = typeof(Transform).GetProperty(nameof(Transform.position), AccessTools.all)?.GetMethod;

        // = intList1.Add(index2);
        // - vector3List.Add(objectsByType[index1].transform.position);
        // + vector3List.Add(OutOfBoundsItemsFix.GetAdjustedPosition(objectsByType[index1]));
        // = break;
        var injector = new ILInjector(codes, ilGenerator)
            .Find(
                ILMatcher.Ldloc(),
                ILMatcher.Ldloc(),
                ILMatcher.Ldloc(),
                ILMatcher.Predicate(i => i.opcode == OpCodes.Ldelem_Ref),
                ILMatcher.Callvirt(getTransformMethod),
                ILMatcher.Callvirt(getPositionMethod)
                );

        if (!injector.IsValid)
        {
            // print error
            MattyFixes.Log.LogWarning("GameNetworkManager.SaveItemsInShip patch failed!!");
            MattyFixes.Log.LogDebug(string.Join("\n", injector.ReleaseInstructions()));
            return codes;
        }

        injector
            .GoToMatchEnd()
            .Back(2)
            .Remove(2)
            .Insert(new CodeInstruction(OpCodes.Call, newOffsetMethod));

        MattyFixes.Log.LogDebug("SaveItemsInShip Patched");
        return injector.ReleaseInstructions();
    }

    private static Vector3 GetAdjustedPosition(GrabbableObject grabbable)
    {
        var position = grabbable.transform.position;

        if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
            return position;

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
    private static IEnumerable<CodeInstruction> FixSpawns(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
    {
        var codes = instructions.ToList();

        if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
            return codes;

        if (!MattyFixes.PluginConfig.OutOfBounds.SpawnInFurniture.Value)
            return codes;

        var navmeshPosMethod = typeof(RoundManager).GetMethod(nameof(RoundManager.GetRandomNavMeshPositionInBoxPredictable),
            [typeof(Vector3),typeof(float), typeof(NavMeshHit), typeof(System.Random), typeof(int), typeof(float)]);
        var verticalOffset = typeof(Item).GetField(nameof(Item.verticalOffset), AccessTools.all);
        var multiplyMethod = typeof(Vector3).GetMethod("op_Multiply", [typeof(Vector3), typeof(float)]);
        var addMethod = typeof(Vector3).GetMethod( "op_Addition", [typeof(Vector3), typeof(Vector3)]);

        //  - position = this.GetRandomNavMeshPositionInBoxPredictable(randomScrapSpawn.transform.position, randomScrapSpawn.itemSpawnRange, this.navHit, this.AnomalyRandom) + Vector3.up * ScrapToSpawn[i].verticalOffset;
        //  + position = this.GetRandomNavMeshPositionInBoxPredictable(randomScrapSpawn.transform.position, randomScrapSpawn.itemSpawnRange, this.navHit, this.AnomalyRandom);

        var injector = new ILInjector(codes, ilGenerator)
            .Find(ILMatcher.Call(navmeshPosMethod));

        if (!injector.IsValid)
        {
            // print error
            MattyFixes.Log.LogWarning("RoundManager.SpawnScrapInLevel patch failed 1!!");
            MattyFixes.Log.LogDebug(string.Join("\n", injector.ReleaseInstructions()));
            return codes;
        }

        injector.Find(
            ILMatcher.Ldfld(verticalOffset),
            ILMatcher.Call(multiplyMethod),
            ILMatcher.Call(addMethod),
            ILMatcher.Stloc()
            );

        if (!injector.IsValid)
        {
            // print error
            MattyFixes.Log.LogWarning("RoundManager.SpawnScrapInLevel patch failed 2!!");
            MattyFixes.Log.LogDebug(string.Join("\n", injector.ReleaseInstructions()));
            return codes;
        }

        injector
            .GoToMatchEnd()
            .Back(1)
            .GoToPush(0)
            .RemoveLastMatch();

        MattyFixes.Log.LogDebug("RoundManager.SpawnScrapInLevel patched");

        return injector.ReleaseInstructions();
    }
}
