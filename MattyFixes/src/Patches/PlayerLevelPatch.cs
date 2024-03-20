using System;
using HarmonyLib;

namespace MattyFixes.Patches
{
    [HarmonyPatch]
    internal class PlayerLevelPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SyncPlayerLevelClientRpc))]
        private static void LimitClientLevel1(HUDManager __instance, ref int playerLevelIndex)
        {
            playerLevelIndex = Math.Min(playerLevelIndex, __instance.playerLevels.Length - 1);
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SyncPlayerLevelServerRpc))]
        private static void LimitServerLevel1(HUDManager __instance, ref int playerLevelIndex)
        {
            playerLevelIndex = Math.Min(playerLevelIndex, __instance.playerLevels.Length - 1);
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SyncAllPlayerLevelsClientRpc), typeof(int[]), typeof(bool[]))]
        private static void LimitClientLevel2(HUDManager __instance,ref int[] playerLevelNumbers)
        {
            for (var index = 0; index < playerLevelNumbers.Length; index++)
            {
                playerLevelNumbers[index] = Math.Min(playerLevelNumbers[index], __instance.playerLevels.Length - 1);
            }
        }        
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SyncAllPlayerLevelsClientRpc), typeof(int[]), typeof(int))]
        private static void LimitClientLevel3(HUDManager __instance,ref int[] allPlayerLevels)
        {
            for (var index = 0; index < allPlayerLevels.Length; index++)
            {
                allPlayerLevels[index] = Math.Min(allPlayerLevels[index], __instance.playerLevels.Length - 1);
            }
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SyncAllPlayerLevelsServerRpc), typeof(int), typeof(int))]
        private static void LimitServerLevel2(HUDManager __instance,ref int newPlayerLevel)
        {
            newPlayerLevel = Math.Min(newPlayerLevel, __instance.playerLevels.Length - 1);
        }
    }
}