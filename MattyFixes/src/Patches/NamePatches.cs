using System.Collections.Generic;
using System.Linq;
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
        private static readonly Dictionary<ulong, NameTaskHolder> NameTasks = new Dictionary<ulong, NameTaskHolder>();

        private struct NameTaskHolder
        {
            internal Task _waitingTask;
            internal Friend _friend;
            internal int _playerObjectIndex;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.SendNewPlayerValuesClientRpc))]
        [HarmonyPriority(Priority.First)]
        private static void PatchNames(PlayerControllerB __instance, ulong[] playerSteamIds)
        {
            if (!MattyFixes.PluginConfig.NameFixes.Enabled.Value)
                return;

            NetworkManager networkManager = __instance.NetworkManager;
            if (networkManager == null || !networkManager.IsListening)
                return;

            if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Client &&
                (networkManager.IsServer || networkManager.IsHost))
                return;

            if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Client ||
                !networkManager.IsClient && !networkManager.IsHost)
                return;

            for (int index = 0; index < playerSteamIds.Length; ++index)
            {
                if (__instance.playersManager.allPlayerScripts[index].isPlayerControlled ||
                    __instance.playersManager.allPlayerScripts[index].isPlayerDead)
                {
                    var steamID = playerSteamIds[index];
                    if (NameTasks.ContainsKey(steamID))
                        continue;

                    var friend = new Friend(steamID);
                    var request = friend.RequestInfoAsync();
                    NameTasks[steamID] = new NameTaskHolder
                    {
                        _friend = friend,
                        _waitingTask = request,
                        _playerObjectIndex = index
                    };
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LateUpdate))]
        private static void PostUpdate(StartOfRound __instance)
        {
            var updated = false;
            foreach (var taskHolder in NameTasks.Values.ToArray())
            {
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

                    playerScript.quickMenuManager.AddUserToPlayerList(steamID, playerName,
                        taskHolder._playerObjectIndex);

                    updated = true;

                    NameTasks.Remove(taskHolder._friend.Id);
                }
            }

            if (updated)
                foreach (var radarTarget in StartOfRound.Instance.mapScreen.radarTargets)
                {
                    var playerController = radarTarget.transform.gameObject.GetComponent<PlayerControllerB>();
                    var radarBooster = radarTarget.transform.gameObject.GetComponent<RadarBoosterItem>();
                    var newName = playerController != null ? playerController.playerUsername :
                        radarBooster != null ? radarBooster.radarBoosterName : null;
                    if (newName != null)
                        radarTarget.name = newName;
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