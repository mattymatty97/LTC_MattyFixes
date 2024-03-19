using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GameNetcodeStuff;
using HarmonyLib;
using Steamworks;
using Unity.Netcode;

namespace MattyFixes.Patches
{
    [HarmonyPatch]
    internal class NamePatches
    {
        private static readonly List<NameTaskHolder> NameTasks = new List<NameTaskHolder>();
        private struct NameTaskHolder
        {
            internal Task _waitingTask;
            internal Friend _friend;
            internal int _playerObjectIndex;
        }
        
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
                    playerScript.playerSteamId = steamID;
                    playerScript.playerUsername = playerScript.name;
                    playerScript.usernameBillboardText.text = playerScript.name;
                    NameTasks.Add(new NameTaskHolder()
                    {
                        _friend = friend,
                        _waitingTask = request,
                        _playerObjectIndex = index
                    });
                    __instance.quickMenuManager.AddUserToPlayerList(steamID, playerScript.playerUsername, finalIndex);
                }
            }
            
            StartOfRound.Instance.StartTrackingAllPlayerVoices();

            GameNetworkManager.Instance!.localPlayerController!.updatePositionForNewlyJoinedClient = true;
            
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LateUpdate))]
        private static void PostUpdate(StartOfRound __instance)
        {
            for (var index = NameTasks.Count; index >= 0; index--)
            {
                var taskHolder = NameTasks[index];
                if (taskHolder._waitingTask.IsCompleted)
                {
                    var playerScript = __instance.allPlayerScripts[taskHolder._playerObjectIndex];
                    var steamID = taskHolder._friend.Id;
                    var playerName = taskHolder._friend.Name;

                    playerName = Regex.Replace(__instance.NoPunctuation(playerName), "[^\\w\\._]", "");

                    if (playerName == string.Empty || playerName.Length == 0)
                        playerName = "Nameless";
                    else if (playerName.Length <= 2)
                        playerName += "0";

                    playerScript.playerSteamId = steamID;
                    playerScript.playerUsername = playerName;
                    playerScript.usernameBillboardText.text = playerName;
                        
                    var duplicateNamesInLobby = playerScript.GetNumberOfDuplicateNamesInLobby();
                    if (duplicateNamesInLobby > 0)
                        playerName = $"{playerName}{duplicateNamesInLobby}";
                        
                    playerScript.quickMenuManager.AddUserToPlayerList(steamID, playerName, taskHolder._playerObjectIndex);
                        
                    foreach (var radarTarget in StartOfRound.Instance.mapScreen.radarTargets)
                    {
                        if (radarTarget.transform == playerScript.transform)
                            radarTarget.name = playerName;
                    }

                    NameTasks.Remove(taskHolder);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnLocalDisconnect))]
        private static void OnDisconnect(StartOfRound __instance)
        {
            NameTasks.Clear();
        }

    }
}