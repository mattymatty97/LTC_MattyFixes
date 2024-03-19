using System.Text.RegularExpressions;
using GameNetcodeStuff;
using HarmonyLib;
using Steamworks;
using Unity.Netcode;

namespace MattyFixes.Patches
{
    [HarmonyPatch]
    internal class NamePatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.SendNewPlayerValuesClientRpc))]
        [HarmonyPriority(Priority.First)]
        private static bool PatchNames(PlayerControllerB __instance, ulong[] playerSteamIds)
        {
            if (!MattyFixes.PluginConfig.NameFixes.Enabled.Value)
                return true;
            
            NetworkManager networkManager = __instance.NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
                return true;
            
            if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Client &&
                (networkManager.IsServer || networkManager.IsHost))
                return true;
            
            if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Client || !networkManager.IsClient && !networkManager.IsHost)
                return true;
            
            for (int index = 0; index < playerSteamIds.Length; ++index)
            {
                if (__instance.playersManager.allPlayerScripts[index].isPlayerControlled || __instance.playersManager.allPlayerScripts[index].isPlayerDead)
                {
                    var friend = new Friend(playerSteamIds[index]);
                    var request = friend.RequestInfoAsync();
                    var playerScript =__instance.playersManager.allPlayerScripts[index];
                    var steamID = playerSteamIds[index];
                    var finalIndex = index;
                    request.ContinueWith((task) =>
                    {
                        var playerName = "Unnamed";
                        if (task.IsCompleted)
                        {
                            playerName = friend.Name;

                            playerName = Regex.Replace(__instance.NoPunctuation(playerName), "[^\\w\\._]", "");

                            if (playerName == string.Empty || playerName.Length == 0)
                                playerName = "Nameless";
                            else if (playerName.Length <= 2)
                                playerName += "0";
                        }

                        playerScript.playerSteamId = steamID;
                        playerScript.playerUsername = playerName;
                        playerScript.usernameBillboardText.text = playerName;
                        
                        var duplicateNamesInLobby = __instance.GetNumberOfDuplicateNamesInLobby();
                        if (duplicateNamesInLobby > 0)
                            playerName = $"{playerName}{duplicateNamesInLobby}";
                        
                        __instance.quickMenuManager.AddUserToPlayerList(steamID, playerName, finalIndex);
                        
                        foreach (var radarTarget in StartOfRound.Instance.mapScreen.radarTargets)
                        {
                            if (radarTarget.transform == playerScript.transform)
                                radarTarget.name = playerName;
                        }
                    });
                }
            }
            StartOfRound.Instance.StartTrackingAllPlayerVoices();
            if (!(GameNetworkManager.Instance != null) || !( GameNetworkManager.Instance.localPlayerController !=  null))
                return false;
            GameNetworkManager.Instance.localPlayerController.updatePositionForNewlyJoinedClient = true;
            
            return false;
        }
    }
}