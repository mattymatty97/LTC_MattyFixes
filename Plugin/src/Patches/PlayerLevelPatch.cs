using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace MattyFixes.Patches
{
    [HarmonyPatch]
    internal class PlayerLevelPatch
    {
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SyncAllPlayerLevelsClientRpc), typeof(int[]), typeof(int))]
        private static IEnumerable<CodeInstruction> AlwaysUpdateLocalPlayer(IEnumerable<CodeInstruction> instructions)
        {
            if (!MattyFixes.PluginConfig.BadgeFixes.Enabled.Value)
                return instructions;
            
            var codes = instructions.ToList();

            var fieldInfo = typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.localPlayerController));
            
            for (var index = 0; index < codes.Count; index++)
            {
                var curr = codes[index];

                if (curr.LoadsField(fieldInfo))
                {
                    if (codes[index + 2].Branches(out var label))
                    {
                        
                        for (var i = index - 5; i < index + 2; i++)
                        {
                            codes[i] = new CodeInstruction(OpCodes.Nop)
                            {
                                labels = codes[i].labels,
                                blocks = codes[i].blocks
                            };
                        }
                        
                        codes[index + 2] = new CodeInstruction(OpCodes.Br, label)
                        {
                            labels = codes[index + 2].labels,
                            blocks = codes[index + 2].blocks
                        };
                        
                        MattyFixes.Log.LogDebug("SyncAllPlayerLevelsClientRpc patched!");
                        break;
                    }   
                }
            }
            
            return codes;
        }
        
    }
}