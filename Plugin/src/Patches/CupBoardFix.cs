using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MattyFixes.Dependency;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MattyFixes.Patches
{
    [HarmonyPatch]
    internal class CupBoardFix
    {
        
        private struct ClosetHolder
        {
            public GameObject Closet;
            public List<ShelfHolder> Shelves;
            public Collider Collider;
        }
        
        private struct ShelfHolder
        {
            public PlaceableObjectsSurface Shelf;
            public Collider Collider;
        }

        private static UnlockableItem _storageCabinet = null;
        private static ClosetHolder? _closet = null;

        private static void CheckCloset()
        {
            if (!_closet.HasValue)
            {
                ClosetHolder holder;
                holder = new ClosetHolder();
                holder.Closet = GameObject.Find("/Environment/HangarShip/StorageCloset");
                holder.Collider = holder.Closet.GetComponent<Collider>();
                holder.Shelves = holder.Closet.GetComponentsInChildren<PlaceableObjectsSurface>().Select(s => new ShelfHolder()
                {
                    Shelf = s,
                    Collider = s.GetComponent<Collider>()
                }).ToList();
                _closet = holder;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnLocalDisconnect))]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnDestroy))]
        private static void ResetOnDisconnect()
        {
            _storageCabinet = null;
            _closet = null;
        }
        
        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
        internal class ObjectCreationPatch
        {
            private static void Prefix(GrabbableObject __instance, out bool __state)
            {
                __state = __instance.itemProperties.itemSpawnsOnGround;
                
                if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value)
                    return;
                //do not run twice if OutOfBounds is active too
                if (MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
                    return;

                if (__instance is ClipboardItem || (__instance is PhysicsProp && __instance.itemProperties.itemName == "Sticky note"))
                    return;
                
                if (!GameNetworkManager.Instance.gameHasStarted)
                {
                    __instance.itemProperties.itemSpawnsOnGround = __instance.IsServer;
                }
                
            }
        
            private static void Postfix(GrabbableObject __instance, bool __state)
            {
                __instance.itemProperties.itemSpawnsOnGround = __state;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SyncShipUnlockablesClientRpc))]
        private static void AfterCupboardSync(StartOfRound __instance)
        {
            var networkManager = __instance.NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
                return;
            if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Client || !networkManager.IsClient && !networkManager.IsHost)
                return;
            
            _storageCabinet ??= __instance.unlockablesList.unlockables
                .Find(u => u.unlockableName == "Cupboard");
            
            if (_storageCabinet.inStorage) 
                return;
            
            CheckCloset();
            
            _closet!.Value.Closet.GetComponent<AutoParentToShip>().MoveToOffset();
            
            Physics.SyncTransforms();
            
            var grabbables = Object.FindObjectsOfType<GrabbableObject>();
            foreach (var grabbable in grabbables.Where(g => g.isInShipRoom))
            {
                ShelfCheck(grabbable);
            }
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadShipGrabbableItems))]
        private static void OnServerSpawn(GrabbableObject __instance)
        {
            _storageCabinet ??= StartOfRound.Instance.unlockablesList.unlockables
                .Find(u => u.unlockableName == "Cupboard");
            
            if (_storageCabinet.inStorage) 
                return;
            
            CheckCloset();
            
            _closet!.Value.Closet.GetComponent<AutoParentToShip>().MoveToOffset();
            
            Physics.SyncTransforms();
            
            var grabbables = Object.FindObjectsOfType<GrabbableObject>();
            foreach (var grabbable in grabbables.Where(g => g.isInShipRoom))
            {
                ShelfCheck(grabbable);
            }
        }
        
        private static void ShelfCheck(GrabbableObject grabbable)
        {
            MattyFixes.Log.LogDebug(
                $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Cupboard Triggered!");
            var tolerance = MattyFixes.PluginConfig.CupBoard.Tolerance.Value;
            var sqrTolerance = tolerance * tolerance;
            try
            {
                var pos = grabbable.transform.position;
                MattyFixes.Log.LogDebug(
                    $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Item pos {pos}!");

                CheckCloset();
                
                var distance = float.MaxValue;
                PlaceableObjectsSurface found = null;
                Vector3? closest = null;
                
                MattyFixes.Log.LogDebug(
                    $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Cupboard pos {_closet.Value.Collider.bounds.min}!");

                var closetCollider = _closet.Value.Collider;
                if (pos.y < closetCollider.bounds.max.y && closetCollider.bounds.SqrDistance(pos) <= sqrTolerance)
                {
                    foreach (var shelfHolder in _closet.Value.Shelves)
                    {
                        var hitPoint = shelfHolder.Collider.ClosestPoint(pos);
                        var tmp = pos.y - hitPoint.y;
                        
                        if (AsyncLoggerProxy.Enabled)
                            AsyncLoggerProxy.WriteData(MattyFixes.NAME, "CupBoard", $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Shelve is {tmp} away!");
                        
                        if (tmp >= 0 && tmp < distance)
                        {
                            found = shelfHolder.Shelf;
                            distance = tmp;
                            closest = hitPoint;
                        }
                    }
                    
                    if (AsyncLoggerProxy.Enabled)
                        AsyncLoggerProxy.WriteData(MattyFixes.NAME, "CupBoard", $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Chosen Shelve is {distance} away!");

                    if (AsyncLoggerProxy.Enabled)
                        AsyncLoggerProxy.WriteData(MattyFixes.NAME, "CupBoard",$"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - With hitpoint at {closest}!");
                }
                
                var transform = grabbable.transform;
                if (found != null)
                {
                    Vector3 newPos;
                    if (MattyFixes.PluginConfig.ItemClipping.Enabled.Value)
                    {
                        newPos = ItemPatches.FixPlacement(closest.Value, found.transform, grabbable);
                    }
                    else
                    {
                        newPos = closest.Value + Vector3.up * MattyFixes.PluginConfig.CupBoard.Shift.Value;
                    }
                    if (AsyncLoggerProxy.Enabled)
                        AsyncLoggerProxy.WriteData(MattyFixes.NAME, "CupBoard",$"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - With newPos at {newPos}!");
                    transform.parent = _closet.Value.Closet.transform;
                    transform.position = newPos;
                    grabbable.targetFloorPosition = transform.localPosition;
                    MattyFixes.Log.LogDebug(
                        $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Pos on shelf {newPos}!");

                }
            }
            catch (Exception ex)
            {
                MattyFixes.Log.LogError($"Exception while checking for Cupboard {ex}");
            }
        }
    }
}