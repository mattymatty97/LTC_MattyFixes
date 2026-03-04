using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using MattyFixes.Interfaces;
using MattyFixes.Utils;
using MattyFixes.Utils.IL;
using UnityEngine;
using VertexLibrary;
using LogLevel = BepInEx.Logging.LogLevel;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal class GrabbableStartPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
    internal static void OnObjectSpawn(GrabbableObject __instance)
    {
        if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value)
            return;

        var itemType = __instance.itemProperties;

        if (((IInjectedItem)itemType).MattyFixes_HasComputedOffset)
            return;

        var key = itemType.GetPath();

        MattyFixes.Log.LogDebug($"{key}({__instance.NetworkObjectId}) needs to compute vertical offset - scheduled");

        var shouldUpdatePosition = ShouldSpawnOnGround(__instance) || __instance.transform.parent == CupBoardFix.Closet.gameObject.transform;

        __instance.StartCoroutine(ProcessGrabbable(__instance, shouldUpdatePosition));
    }

    // ReSharper disable function SuspiciousTypeConversion.Global
    // ReSharper disable function Unity.PerformanceCriticalCodeInvocation
    private static IEnumerator ProcessGrabbable(GrabbableObject grabbable, bool updatePosition = true)
    {
        var itemType = grabbable.itemProperties;
        var key = itemType.GetPath();

        var oldOffset = itemType.verticalOffset;
        var animators = grabbable.GetComponentsInChildren<Animator>();

        //wait till animators stop
        yield return new WaitUntil(() => animators.All(
            a => !a || Mathf.Approximately(a.speed, 0f) || a.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1));

        //only run the code on the first coroutine that completes
        if (!((IInjectedItem)itemType).MattyFixes_HasComputedOffset)
        {
            ((IInjectedItem)itemType).MattyFixes_HasComputedOffset = true;

            MattyFixes.Log.LogDebug($"{key}({grabbable.NetworkObjectId}) is computing vertical offset");

            itemType.verticalOffset = ComputeVerticalOffset(grabbable);

            var isOriginal = Mathf.Approximately(oldOffset, itemType.verticalOffset);

            MattyFixes.Log.LogDebug($"{key} {(isOriginal ? "original" : "new")} offset is {itemType.verticalOffset}");

            if (isOriginal)
                yield break;
        }

        if (!updatePosition)
            yield break;

        var oldPosition = grabbable.targetFloorPosition;
        grabbable.targetFloorPosition -= Vector3.up * oldOffset;
        grabbable.targetFloorPosition += Vector3.up * itemType.verticalOffset;

        MattyFixes.Log.LogDebug(
            $"{key}({grabbable.NetworkObjectId}) position updated [{oldPosition}] -> [{grabbable.targetFloorPosition}]");
    }


    private static float ComputeVerticalOffset(GrabbableObject grabbable)
    {
        var itemType = grabbable.itemProperties;

        var key = itemType.GetPath();

        try
        {

            if (MattyFixes.PluginConfig.ItemClipping.ManualOffsetMap.TryGetValue(key,
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
            MattyFixes.Log.LogError($"{key} Failed to compute vertical offset! {ex}");
        }

        return itemType.verticalOffset;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
    private static IEnumerable<CodeInstruction> RedirectSpawnOnGroundCheck(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
    {
        var codes = instructions.ToList();

        var itemPropertiesFld = typeof(GrabbableObject).GetField(nameof(GrabbableObject.itemProperties), AccessTools.all);
        var spawnsOnGroundFld = typeof(Item).GetField(nameof(Item.itemSpawnsOnGround), AccessTools.all);

        var replacementMethod = typeof(GrabbableStartPatch).GetMethod(nameof(NewSpawnOnGroundCheck), AccessTools.all);

        // = this.originalScale = this.transform.localScale;
        // - if (this.itemProperties.itemSpawnsOnGround)
        // + if (GrabbableStartPatch.NewSpawnOnGroundCheck(this))
        // = {
        var injector = new ILInjector(codes, ilGenerator)
            .Find(
                ILMatcher.Ldarg(),
                ILMatcher.Ldfld(itemPropertiesFld),
                ILMatcher.Ldfld(spawnsOnGroundFld),
                ILMatcher.Branch().CaptureAs(out var branch)
                );

        if (!injector.IsValid)
        {
            // print error
            MattyFixes.Log.LogWarning("GrabbableObject.Start patch failed!!");
            MattyFixes.Log.LogDebug(string.Join("\n", injector.ReleaseInstructions()));
            return codes;
        }

        injector.ReplaceLastMatch(
            InstructionUtilities.MakeLdarg(0),
            new CodeInstruction(OpCodes.Call, replacementMethod),
            branch
        );

        MattyFixes.Log.LogDebug("GrabbableObject.Start patched!");

        return injector.ReleaseInstructions();
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

        if (StartOfRound.Instance.localPlayerController && !StartOfRoundPatch.IsInitializingGame)
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
