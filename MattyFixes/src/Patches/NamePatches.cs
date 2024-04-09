using System.Collections;
using System.Text.RegularExpressions;
using GameNetcodeStuff;
using HarmonyLib;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace MattyFixes.Patches
{
    [HarmonyPatch]
    internal class NamePatches
    {

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
                var _controller = __instance.playersManager.allPlayerScripts[index];
                if (_controller.isPlayerControlled ||
                    _controller.isPlayerDead)
                {
                    _controller.StartCoroutine(LateUsernameUpdate(_controller, index, playerSteamIds[index]));
                }
            }
        }

        private static IEnumerator LateUsernameUpdate(PlayerControllerB _controller, int index, ulong steamID)
        {
            yield return new WaitUntil(() => !SteamFriends.RequestUserInformation(steamID, false));
            var friend = new Friend(steamID);
            var playerName = friend.Name;

            MattyFixes.Log.LogWarning($"Late Friend update Completed Player {index} ({steamID}) has name {playerName}");
            playerName = Regex.Replace(_controller.NoPunctuation(playerName), "[^\\w\\._]", "");

            if (playerName == string.Empty || playerName.Length == 0)
                playerName = "Nameless";
            else if (playerName.Length <= 2)
                playerName += "0";

            _controller.playerSteamId = steamID;
            _controller.playerUsername = playerName;
            _controller.usernameBillboardText.text = playerName;

            var duplicateNamesInLobby = _controller.GetNumberOfDuplicateNamesInLobby();
            if (duplicateNamesInLobby > 0)
                playerName = $"{playerName}{duplicateNamesInLobby}";

            _controller.quickMenuManager.AddUserToPlayerList(steamID, playerName, index);
                    
            StartOfRound.Instance.mapScreen.ChangeNameOfTargetTransform(_controller.transform, playerName);

        }
    }
}