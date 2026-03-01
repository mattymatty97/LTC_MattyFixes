using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MattyFixes.Utils;
using Unity.Netcode;
using UnityEngine;
using LogLevel = BepInEx.Logging.LogLevel;
using Object = UnityEngine.Object;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal class CupBoardFix
{
    private static ClosetHolder? _closet;

    internal static ClosetHolder Closet
    {
        get
        {
            _closet ??= new ClosetHolder();
            return _closet.Value;
        }
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
        if(!__instance.IsRPCClientStage())
            return;

        if (__instance.IsServer)
            return;

        var closet = Closet;
        
        if (closet.IsInitialized)
        {
            MattyFixes.VerboseCupboardLog(LogLevel.Warning, () => "SyncShipUnlockablesClientRpc Cupboard Triggered but was already Initialized!");
            return;
        }

        closet.IsInitialized = true;

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
    [HarmonyPriority(0)]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadShipGrabbableItems))]
    private static void OnServerSpawn(GrabbableObject __instance)
    {
        var closet = Closet;
        
        if (closet.IsInitialized)
        {
            MattyFixes.VerboseCupboardLog(LogLevel.Warning, () => "LoadShipGrabbableItems Cupboard Triggered but was already Initialized!");
            return;
        }

        closet.IsInitialized = true;

        if (closet.Unlockable.inStorage)
            return;

        closet.gameObject.GetComponent<AutoParentToShip>().MoveToOffset();

        Physics.SyncTransforms();

        var grabbables = Object.FindObjectsOfType<GrabbableObject>();
        foreach (var grabbable in grabbables.Where(g => g.isInShipRoom))
        {
            ShelfCheck(grabbable);
        }
    }

    private static void ShelfCheck(GrabbableObject grabbable, float offset = 0f)
    {
        
        MattyFixes.VerboseCupboardLog(LogLevel.Info, () =>
            $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - Cupboard Triggered!");

        if (grabbable is ClipboardItem ||
            (grabbable is PhysicsProp && grabbable.itemProperties.itemName == "Sticky note"))
            return;

        var tolerance = MattyFixes.PluginConfig.CupBoard.Tolerance.Value;
        var sqrTolerance = tolerance * tolerance;
        try
        {
            var pos = grabbable.transform.position + Vector3.down * offset;

            MattyFixes.VerboseCupboardLog(LogLevel.Debug, () =>
                $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - Item pos {pos}!");

            var closet = Closet;

            var distance = float.MaxValue;
            PlaceableObjectsSurface found = null;
            Vector3? closest = null;

            MattyFixes.VerboseCupboardLog(LogLevel.Debug, () =>
                $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - Cupboard pos {closet.Collider.bounds.min}!");

            var closetCollider = closet.Collider;
            if (pos.y < closetCollider.bounds.max.y && closetCollider.bounds.SqrDistance(pos) <= sqrTolerance)
            {
                foreach (var shelfHolder in closet.Shelves)
                {
                    var hitPoint = shelfHolder.Collider.ClosestPointOnBounds(pos);
                    var tmp = pos.y - hitPoint.y;

                    MattyFixes.VerboseCupboardLog(LogLevel.Debug, () =>
                        $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - Shelve is {tmp} away!");

                    if (tmp >= 0 && tmp < distance)
                    {
                        found = shelfHolder.Shelf;
                        distance = tmp;
                        closest = hitPoint;
                    }
                }

                MattyFixes.VerboseCupboardLog(LogLevel.Debug, () =>
                    $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - Chosen Shelve is {distance} away!");

                MattyFixes.VerboseCupboardLog(LogLevel.Debug, () =>
                    $"{grabbable.itemProperties.itemName}({grabbable.NetworkObjectId}) - With hitpoint at {closest}!");
            }

            var transform = grabbable.transform;
            if (found != null)
            {
                Vector3 newPos = closest.Value + Vector3.up * grabbable.itemProperties.verticalOffset; //ItemPatches.FixPlacement(closest.Value, found.transform, grabbable);
                transform.parent = closet.gameObject.transform;
                transform.position = newPos;
                grabbable.targetFloorPosition = transform.localPosition;
                MattyFixes.VerboseCupboardLog(LogLevel.Info, () =>
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
        public readonly UnlockableItem Unlockable;
        public readonly GameObject gameObject;
        public readonly List<ShelfHolder> Shelves;
        public readonly Collider Collider;
        public bool IsInitialized;

        public ClosetHolder()
        {
            Unlockable = StartOfRound.Instance.unlockablesList.unlockables
                .Find(u => u.unlockableName == "Cupboard");
            gameObject = GameObject.Find("/Environment/HangarShip/StorageCloset");
            Collider = gameObject.GetComponent<Collider>();
            Shelves = gameObject.GetComponentsInChildren<PlaceableObjectsSurface>().Select(s =>
                new ShelfHolder
                {
                    Shelf = s,
                    Collider = s.GetComponent<Collider>()
                }).ToList();
        }
    }

    internal struct ShelfHolder
    {
        public PlaceableObjectsSurface Shelf;
        public Collider Collider;
    }
}