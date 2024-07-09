using System;
using System.Linq;
using HarmonyLib;
using MattyFixes.Dependency;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MattyFixes.Patches
{
    [HarmonyPatch]
    internal class CupBoardFix
    {

        private static UnlockableItem _storageCabinet = null;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SyncShipUnlockablesClientRpc))]
        private static void AfterCupboardSync(StartOfRound __instance)
        {
            _storageCabinet ??= __instance.unlockablesList.unlockables
                .Find(u => u.unlockableName == "Cupboard");
            
            if (_storageCabinet.inStorage) 
                return;
            
            var grabbables = Object.FindObjectsOfType<GrabbableObject>();
            foreach (var grabbable in grabbables.Where(g => g.isInShipRoom))
            {
                ShelfCheck(grabbable);
            }
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
        private static void OnServerSpawn(GrabbableObject __instance)
        {
            _storageCabinet ??= StartOfRound.Instance.unlockablesList.unlockables
                .Find(u => u.unlockableName == "Cupboard");

            if (!__instance.IsServer || !_storageCabinet.inStorage) 
                return;
            
            ShelfCheck(__instance);
        }
        
        private static void ShelfCheck(GrabbableObject grabbable)
        {
            MattyFixes.Log.LogDebug(
                $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Cupboard Triggered!");
            var tolerance = MattyFixes.PluginConfig.CupBoard.Tolerance.Value;
            try
            {
                var pos = grabbable.transform.position;
                MattyFixes.Log.LogDebug(
                    $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Item pos {pos}!");

                var closet = GameObject.Find("/Environment/HangarShip/StorageCloset");
                PlaceableObjectsSurface[] storageShelves =
                    closet.GetComponentsInChildren<PlaceableObjectsSurface>();
                var collider = closet.GetComponent<Collider>();
                var distance = float.MaxValue;
                PlaceableObjectsSurface found = null;
                Vector3? closest = null;
                
               MattyFixes.Log.LogDebug(
                    $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Cupboard pos {collider.bounds.min}!");
                
                if (collider.bounds.Contains(pos))
                {
                    foreach (var shelf in storageShelves)
                    {
                        var hitPoint = shelf.GetComponent<Collider>().ClosestPoint(pos);
                        var tmp = pos.y - hitPoint.y;
                        
                        if (AsyncLoggerProxy.Enabled)
                            AsyncLoggerProxy.WriteData(MattyFixes.NAME, "CupBoard", $"{grabbable.itemProperties.itemName}({grabbable.gameObject.GetInstanceID()}) - Shelve is {tmp} away!");
                        
                        if (tmp >= 0 && tmp < distance)
                        {
                            found = shelf;
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
                    transform.parent = closet.transform;
                    transform.position = newPos;
                    grabbable.targetFloorPosition = transform.localPosition;
                }
            }
            catch (Exception ex)
            {
                MattyFixes.Log.LogError($"Exception while checking for Cupboard {ex}");
            }
        }
    }
}