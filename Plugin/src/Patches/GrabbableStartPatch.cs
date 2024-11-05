using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Pool;
using VertexLibrary;
using LogLevel = BepInEx.Logging.LogLevel;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal class GrabbableStartPatch
{
    private static readonly HashSet<Item> ComputedOffsets = [];
    private static readonly Dictionary<Item, List<GrabbableObject>> PendingObjects = [];

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
    internal static void OnObjectSpawn(GrabbableObject __instance)
    {
        if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value)
            return;

        var itemType = __instance.itemProperties;

        if (ComputedOffsets.Contains(itemType))
            return;

        MattyFixes.Log.LogDebug(
            $"{itemType.itemName}({__instance.NetworkObjectId}) needs to compute vertical offset - scheduled");

        __instance.StartCoroutine(ProcessGrabbable(__instance));

        if (!ShouldSpawnOnGround(__instance) && __instance.transform.parent != CupBoardFix.Closet.gameObject.transform)
            return;

        if (!PendingObjects.TryGetValue(itemType, out var list))
        {
            list = ListPool<GrabbableObject>.Get();
            PendingObjects[itemType] = list;
        }

        list.Add(__instance);

        MattyFixes.Log.LogDebug(
            $"{itemType.itemName}({__instance.NetworkObjectId}) will need to update the position - enqueued");
    }

    private static IEnumerator ProcessGrabbable(GrabbableObject grabbable)
    {
        var itemType = grabbable.itemProperties;

        var animators = grabbable.GetComponentsInChildren<Animator>();

        //wait till animators stop
        yield return new WaitUntil(() => animators.All(
            a => !a || Mathf.Approximately(a.speed, 0f) || a.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1));

        //only run the code on the first coroutine that completes
        if (!ComputedOffsets.Add(itemType))
            yield break;

        MattyFixes.Log.LogDebug($"{itemType.itemName}({grabbable.NetworkObjectId}) is computing vertical offset");

        var oldOffset = itemType.verticalOffset;
        itemType.verticalOffset = ComputeVerticalOffset(grabbable);

        var isOriginal = Mathf.Approximately(oldOffset, itemType.verticalOffset);

        MattyFixes.Log.LogDebug(
            $"{itemType.itemName} {(isOriginal ? "original" : "new")} offset is {itemType.verticalOffset}");

        if (isOriginal)
            yield break;

        if (!PendingObjects.TryGetValue(itemType, out var list))
            yield break;

        foreach (var gObject in list)
        {
            if (!gObject)
                continue;

            var oldPosition = gObject.targetFloorPosition;
            gObject.targetFloorPosition -= Vector3.up * oldOffset;
            gObject.targetFloorPosition += Vector3.up * itemType.verticalOffset;

            MattyFixes.Log.LogDebug(
                $"{itemType.itemName}({gObject.NetworkObjectId}) position updated [{oldPosition}] -> [{gObject.targetFloorPosition}]");
        }

        list.Clear();

        ListPool<GrabbableObject>.Release(list);

        PendingObjects.Remove(itemType);
    }


    private static float ComputeVerticalOffset(GrabbableObject grabbable)
    {
        var itemType = grabbable.itemProperties;

        try
        {
            if (MattyFixes.PluginConfig.ItemClipping.ManualOffsetMap.TryGetValue(itemType.itemName,
                    out var offset))
                return offset;

            var executionOptions = new ExecutionOptions()
            {
                VertexCache = VertexesExtensions.GlobalPartialCache,
                CullingMask = MattyFixes.VisibleLayerMask,
                LogHandler = MattyFixes.VerboseMeshLog,
                OverrideMatrix = Matrix4x4.TRS(Vector3.zero,
                    Quaternion.Euler(
                        grabbable.itemProperties.restingRotation.x, grabbable.itemProperties.floorYOffset + 90f,
                        grabbable.itemProperties.restingRotation.z)
                    , grabbable.transform.lossyScale)
            };

            if (grabbable.transform.TryGetBounds(out var bounds, executionOptions))
            {
                offset = -bounds.min.y;
                offset += MattyFixes.PluginConfig.ItemClipping.VerticalOffset.Value;
            }
            else
                offset = itemType.verticalOffset;

            return offset;
        }
        catch (Exception ex)
        {
            MattyFixes.Log.LogError($"{itemType.itemName} Failed to compute vertical offset! {ex}");
        }

        return itemType.verticalOffset;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
    private static IEnumerable<CodeInstruction> RedirectSpawnOnGroundCheck(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var itemPropertiesFld = AccessTools.Field(typeof(GrabbableObject), nameof(GrabbableObject.itemProperties));
        var spawnsOnGroundFld = AccessTools.Field(typeof(Item), nameof(Item.itemSpawnsOnGround));

        var replacementMethod = AccessTools.Method(typeof(GrabbableStartPatch), nameof(NewSpawnOnGroundCheck));

        var matcher = new CodeMatcher(codes);


        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, itemPropertiesFld),
            new CodeMatch(OpCodes.Ldfld, spawnsOnGroundFld),
            new CodeMatch(OpCodes.Brfalse)
        );

        if (matcher.IsInvalid)
        {
            return codes;
        }

        matcher.Advance(1);

        matcher.RemoveInstructions(2);

        matcher.Insert(new CodeInstruction(OpCodes.Call, replacementMethod));

        MattyFixes.Log.LogDebug("GrabbableObject.Start patched!");

        return matcher.Instructions();
    }

    private static bool NewSpawnOnGroundCheck(GrabbableObject grabbableObject)
    {
        MattyFixes.VerboseItemsLog(LogLevel.Debug, () =>
            $"{grabbableObject.itemProperties.itemName}({grabbableObject.NetworkObjectId}) processing GrabbableObject pos {grabbableObject.transform.position}");

        var ret = ShouldSpawnOnGround(grabbableObject);

        MattyFixes.VerboseItemsLog(LogLevel.Debug, () =>
            $"{grabbableObject.itemProperties.itemName}({grabbableObject.NetworkObjectId}) processing GrabbableObject spawnState " +
            $"OnGround - was: {grabbableObject.itemProperties.itemSpawnsOnGround} new:{ret}");

        return ret;
    }

    private static bool ShouldSpawnOnGround(GrabbableObject grabbableObject)
    {
        var ret = grabbableObject.itemProperties.itemSpawnsOnGround;

        //run normal code if settings are off
        if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value && !MattyFixes.PluginConfig.CupBoard.Enabled.Value)
            return ret;

        //or if it's one of the pre-existing items
        if (grabbableObject is ClipboardItem ||
            (grabbableObject is PhysicsProp && grabbableObject.itemProperties.itemName == "Sticky note"))
            return ret;

        if (StartOfRound.Instance.localPlayerController && !StartOfRoundPatch._isInitializingGame)
            return ret;

        if (MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
        {
            ret = StartOfRound.Instance.IsServer;
        }

        if (!MattyFixes.PluginConfig.CupBoard.Enabled.Value)
            return ret;

        if (CupBoardFix.Closet.gameObject &&
            grabbableObject.transform.parent == CupBoardFix.Closet.gameObject.transform)
            ret = false;

        return ret;
    }
}