using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal class CupBoardFix
{
    private static ClosetHolder? _closet;

    internal static ClosetHolder GetCloset()
    {
        if (!_closet.HasValue)
        {
            ClosetHolder holder;
            holder = new ClosetHolder();
            holder.Unlockable = StartOfRound.Instance.unlockablesList.unlockables
                .Find(u => u.unlockableName == "Cupboard");
            holder.gameObject = GameObject.Find("/Environment/HangarShip/StorageCloset");
            holder.Collider = holder.gameObject.GetComponent<Collider>();
            holder.Shelves = holder.gameObject.GetComponentsInChildren<PlaceableObjectsSurface>().Select(s =>
                new ShelfHolder
                {
                    Shelf = s,
                    Collider = s.GetComponent<Collider>()
                }).ToList();
            _closet = holder;
        }

        return _closet.Value;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnLocalDisconnect))]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnDestroy))]
    private static void ResetOnDisconnect()
    {
        _closet = null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SyncShipUnlockablesClientRpc))]
    private static void AfterCupboardSync(StartOfRound __instance)
    {
        var networkManager = __instance.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Client ||
            (!networkManager.IsClient && !networkManager.IsHost))
            return;

        if (__instance.IsServer)
            return;

        var closet = GetCloset();

        if (closet.Unlockable.inStorage)
            return;

        closet.gameObject.GetComponent<AutoParentToShip>().MoveToOffset();

        Physics.SyncTransforms();

        var grabbables = Object.FindObjectsOfType<GrabbableObject>();
        foreach (var grabbable in grabbables.Where(g => g.isInShipRoom))
        {
            var offset = 0f;
            if (grabbable.hasHitGround)
                offset = grabbable.itemProperties.verticalOffset;
            ShelfCheck(grabbable, offset);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadShipGrabbableItems))]
    private static void OnServerSpawn(GrabbableObject __instance)
    {
        var closet = GetCloset();

        if (closet.Unlockable.inStorage)
            return;

        closet.gameObject.GetComponent<AutoParentToShip>().MoveToOffset();

        Physics.SyncTransforms();

        var grabbables = Object.FindObjectsOfType<GrabbableObject>();
        foreach (var grabbable in grabbables.Where(g => g.isInShipRoom))
        {
            var offset = 0f;
            if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
                if (grabbable.hasHitGround)
                    offset = grabbable.itemProperties.verticalOffset;
            ShelfCheck(grabbable, offset);
        }
    }

    private static void ShelfCheck(GrabbableObject grabbable, float offset = 0f)
    {
        MattyFixes.Log.LogDebug(
            $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - Cupboard Triggered!");

        if (grabbable is ClipboardItem ||
            (grabbable is PhysicsProp && grabbable.itemProperties.itemName == "Sticky note"))
            return;

        var tolerance = MattyFixes.PluginConfig.CupBoard.Tolerance.Value;
        var sqrTolerance = tolerance * tolerance;
        try
        {
            var pos = grabbable.transform.position + Vector3.down * offset;

            MattyFixes.Log.LogDebug(
                $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - Item pos {pos}!");

            var closet = GetCloset();

            var distance = float.MaxValue;
            PlaceableObjectsSurface found = null;
            Vector3? closest = null;

            MattyFixes.Log.LogDebug(
                $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - Cupboard pos {closet.Collider.bounds.min}!");

            var closetCollider = closet.Collider;
            if (pos.y < closetCollider.bounds.max.y && closetCollider.bounds.SqrDistance(pos) <= sqrTolerance)
            {
                foreach (var shelfHolder in closet.Shelves)
                {
                    var hitPoint = shelfHolder.Collider.ClosestPoint(pos);
                    var tmp = pos.y - hitPoint.y;

                    MattyFixes.VerboseLog(
                        $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - Shelve is {tmp} away!");

                    if (tmp >= 0 && tmp < distance)
                    {
                        found = shelfHolder.Shelf;
                        distance = tmp;
                        closest = hitPoint;
                    }
                }

                MattyFixes.VerboseLog(
                    $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - Chosen Shelve is {distance} away!");

                MattyFixes.VerboseLog(
                    $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - With hitpoint at {closest}!");
            }

            var transform = grabbable.transform;
            if (found != null)
            {
                Vector3 newPos;
                if (MattyFixes.PluginConfig.ItemClipping.Enabled.Value)
                    newPos = ItemPatches.FixPlacement(closest.Value, found.transform, grabbable);
                else
                    newPos = closest.Value + Vector3.up * MattyFixes.PluginConfig.CupBoard.Shift.Value;
                MattyFixes.VerboseLog(
                    $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - With newPos at {newPos}!");
                transform.parent = closet.gameObject.transform;
                transform.position = newPos;
                grabbable.targetFloorPosition = transform.localPosition;
                MattyFixes.Log.LogDebug(
                    $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - Pos on shelf {newPos}!");
            }
        }
        catch (Exception ex)
        {
            MattyFixes.Log.LogError($"Exception while checking for Cupboard {ex}");
        }
    }

    internal struct ClosetHolder
    {
        public UnlockableItem Unlockable;
        public GameObject gameObject;
        public List<ShelfHolder> Shelves;
        public Collider Collider;
    }

    internal struct ShelfHolder
    {
        public PlaceableObjectsSurface Shelf;
        public Collider Collider;
    }
}