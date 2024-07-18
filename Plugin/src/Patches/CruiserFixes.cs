using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using GameNetcodeStuff;
using HarmonyLib;
using MattyFixes.Utils;
using Unity.Netcode;
using UnityEngine;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal class CruiserFixes
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DiscardHeldObject))]
    private static IEnumerable<CodeInstruction> PreventCruiserDrop(IEnumerable<CodeInstruction> instructions,
        ILGenerator ilGenerator)
    {
        var codes = instructions.ToList();

        var checkMethodInfo = AccessTools.Method(typeof(CruiserFixes), nameof(AlternateCruiserParenting));
        var inequalityMethodInfo = AccessTools.Method(typeof(Object), "op_Inequality");

        var matcher = new CodeMatcher(codes, ilGenerator);

        matcher.MatchForward(
            true,
            new CodeMatch(OpCodes.Ldloc_0),
            new CodeMatch(OpCodes.Ldnull),
            new CodeMatch(OpCodes.Call, inequalityMethodInfo),
            new CodeMatch(OpCodes.Brfalse)
        );

        if (!matcher.IsValid)
        {
            MattyFixes.Log.LogError($"Failed to patch DiscardHeldObject for Cruiser!");
            MattyFixes.Log.LogWarning("DiscardHeldObject:\n" + string.Join("\n", matcher.Instructions()));
            return codes;
        }

        var label = matcher.Operand;

        matcher.Advance(1);

        matcher.Insert(
            new CodeInstruction(OpCodes.Call, checkMethodInfo),
            new CodeInstruction(OpCodes.Brtrue, label));

        return matcher.Instructions();
    }


    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DiscardHeldObject))]
    private static void PreventCruiserDrop(PlayerControllerB __instance, 
        ref bool placeObject,
        ref NetworkObject parentObjectTo,
        ref Vector3 placePosition,
        ref bool matchRotationOfParent)
    {
        if (!AlternateCruiserParenting())
            return;
        
        if (placeObject)
            return;

        var grabbable = __instance.currentlyHeldObjectServer;
        if (!grabbable.gameObject.TryGetWorldCentroid(out var center))
            center = grabbable.transform.position;

        RaycastHit hit = default;
        Transform transform = null;

        if (Physics.Raycast(center, -__instance.transform.up, out hit, 4f, 1342179585, QueryTriggerInteraction.Ignore))
            transform = hit.collider.gameObject.transform;

        if (transform == null) 
            return;
        
        var physicsRegion = transform.GetComponentInChildren<PlayerPhysicsRegion>();
        if (physicsRegion == null ||
            !physicsRegion.allowDroppingItems ||
            physicsRegion.itemDropCollider.ClosestPoint(hit.point) != hit.point) 
            return;
        
        var networkObject = transform.GetComponentInParent<NetworkObject>();
        if (networkObject == null) 
            return;
        
        var verticalOffset = 0.04f + grabbable.itemProperties.verticalOffset;
                    
        MattyFixes.Log.LogInfo("parenting item to Cruiser");
        placePosition = networkObject.transform.InverseTransformPoint(
            hit.point + hit.transform.up * verticalOffset);
        parentObjectTo = networkObject;
        placeObject = true;
        matchRotationOfParent = false;
    }
    

    private static bool AlternateCruiserParenting()
    {
        return MattyFixes.PluginConfig.CruiserFixes.Enabled.Value &&
               MattyFixes.PluginConfig.CruiserFixes.AlternateItemDrop.Value;
    }
    
/*
    [HarmonyPostfix]
    [HarmonyPatch(typeof(VehicleCollisionTrigger), nameof(VehicleCollisionTrigger.OnTriggerEnter))]
    private static void OnCruiserTrigger(VehicleCollisionTrigger __instance, Collider other)
    {
        if (!ShouldSkipCruiserParenting())
            return;

        if (!other.CompareTag("PhysicProp"))
            return;

        if (!other.TryGetComponent<GrabbableObject>(out var grabbable))
        {
            grabbable = other.GetComponentInChildren<GrabbableObject>();
            if (grabbable == null)
                return;
        }

        if (grabbable.hasHitGround)
            return;

        var cruiserT = __instance.transform.root;
        if (grabbable.transform.parent == cruiserT)
            return;

        var startPos = grabbable.transform.parent.TransformPoint(grabbable.startFallingPosition);
        var targetPos = grabbable.transform.parent.TransformPoint(grabbable.targetFloorPosition);
        var direction = (targetPos - startPos).normalized;

        var ray = new Ray(startPos, direction);
        if (Physics.Raycast(ray, out var hit, 80f, 1342179585, QueryTriggerInteraction.Ignore))
        {
            var pos = hit.point + hit.transform.up * (0.04f + grabbable.itemProperties.verticalOffset);
            var localpos = cruiserT.InverseTransformPoint(pos);

            StartOfRound.Instance.localPlayerController.ThrowObjectServerRpc();

        }
    }*/
}