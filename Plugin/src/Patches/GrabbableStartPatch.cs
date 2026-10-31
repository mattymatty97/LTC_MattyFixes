using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using MattyFixes.Config;
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

        var variant = __instance.GetVariant();
        var key = variant.Identifier;

        if (MattyFixes.PluginConfig.ItemClipping.ItemOffsets.ContainsKey(key))
            return;

        MattyFixes.Log.LogDebug($"{key} ({__instance.NetworkObjectId}) needs to compute vertical offset - scheduled");

        var shouldUpdatePosition = ShouldSpawnOnGround(__instance) || __instance.transform.parent == CupBoardFix.Closet.gameObject.transform;

        __instance.StartCoroutine(ProcessGrabbable(__instance, shouldUpdatePosition));
    }

    // ReSharper disable function Unity.PerformanceCriticalCodeInvocation
    private static IEnumerator ProcessGrabbable(GrabbableObject grabbable, bool updatePosition = true)
    {
        var variant = grabbable.GetVariant();
        var key = variant.Identifier;

        var oldOffset = variant.VerticalOffset;
        var animators = grabbable.GetComponentsInChildren<Animator>();

        //wait till animators stop
        yield return new WaitUntil(() => animators.All( a =>
            !a || !a.isActiveAndEnabled ||
            Mathf.Approximately(a.speed, 0f) || a.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1));

        //only run the code on the first coroutine that completes
        if (!MattyFixes.PluginConfig.ItemClipping.ItemOffsets.ContainsKey(key))
        {
            MattyFixes.Log.LogDebug($"{key} ({grabbable.NetworkObjectId}) is computing vertical offset");

            var offset = ComputeVerticalOffset(grabbable);

            variant.VerticalOffset = offset;

            var isOriginal = Mathf.Approximately(oldOffset, offset);

            MattyFixes.Log.LogDebug($"{key} {(isOriginal ? "original" : "new")} offset is {offset}");

            if (isOriginal)
                yield break;
        }

        if (!updatePosition)
            yield break;

        var oldPosition = grabbable.targetFloorPosition;
        grabbable.targetFloorPosition -= Vector3.up * oldOffset;
        grabbable.targetFloorPosition += Vector3.up * variant.VerticalOffset;

        MattyFixes.Log.LogDebug(
            $"{key}({grabbable.NetworkObjectId}) position updated [{oldPosition}] -> [{grabbable.targetFloorPosition}]");
    }


    private static float ComputeVerticalOffset(GrabbableObject grabbable)
    {
        var variant = grabbable.GetVariant();
        var itemPath = variant.Identifier;

        try
        {
            if (!MattyFixes.PluginConfig.ItemClipping.ItemConfigs.TryGetValue(itemPath, out var configHolder))
            {
                MattyFixes.PluginConfig.ItemClipping.ItemConfigs[itemPath] = configHolder = new ConfigHolder();
            }

            if (!MattyFixes.PluginConfig.ItemClipping.ItemOffsets.TryGetValue(itemPath, out var offsetConfig))
            {
                var lastSeparator = itemPath.LastIndexOf('/');

                var itemSection = lastSeparator > 0 ? itemPath[..lastSeparator] : "Unknown";
                var itemName = ItemCategory.SanitizeForConfig(itemPath[(lastSeparator + 1)..]);

                itemSection = itemSection.Replace('/', '|');
                itemSection = ItemCategory.SanitizeForConfig(itemSection);

                var ogOffset = variant.VerticalOffset;

                var defValue = "auto";
                if (MattyFixes.ConfigOverrides.TryGetValue(itemPath, out var overrideValue) && overrideValue.VerticalOffset != null)
                {
                    defValue = "override";
                }

                configHolder.VerticalOffset = MattyFixes.Instance.OffsetConfig.Bind(
                    itemSection,
                    itemName,
                    defValue,
                    $"vertical offset for the item\nuse `auto` to let MattyFixes compute the offset\nuse `default` for the default offset\nuse `override` for the offset specified in the override files\ndefault = '{ogOffset}'");

                offsetConfig = new ItemOffsetConfig(
                    ogOffset,
                    defValue == "override" ? overrideValue!.VerticalOffset : null
                );

                MattyFixes.PluginConfig.ItemClipping.ItemOffsets[itemPath] = offsetConfig;
            }

            switch (configHolder.VerticalOffset.Value)
            {
                case "override" when offsetConfig.OverrideValue.HasValue:
                    return offsetConfig.OverrideValue.Value;
                case "default":
                    return offsetConfig.Original;
            }

            if (offsetConfig.ComputedValue.HasValue)
            {
                return offsetConfig.ComputedValue.Value;
            }

            var restingRotation = variant.RestingRotation;

            var executionOptions = new ExecutionOptions()
            {
                VertexCache = VertexesExtensions.GlobalPartialCache,
                CullingMask = MattyFixes.VisibleLayerMask,
                LogHandler = MattyFixes.VerboseMeshLog,
                OverrideMatrix = Matrix4x4.TRS(Vector3.zero,
                    Quaternion.Euler(restingRotation.x, restingRotation.y + 90f, restingRotation.z)
                    ,grabbable.transform.lossyScale)
            };

            float offset;

            if (grabbable.transform.TryGetBounds(out var bounds, executionOptions))
            {
                offset = -bounds.min.y;
                offset += MattyFixes.PluginConfig.ItemClipping.VerticalOffset.Value;
            }
            else
                offset = variant.VerticalOffset;

            offsetConfig.ComputedValue = offset;
            return offset;
        }
        catch (Exception ex)
        {
            MattyFixes.Log.LogError($"{itemPath} Failed to compute vertical offset! {ex}");
        }

        return variant.VerticalOffset;
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
