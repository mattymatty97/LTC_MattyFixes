using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx.Logging;
using HarmonyLib;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal class GrabbableStartPatch
{

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
    private static IEnumerable<CodeInstruction> RedirectSpawnOnGroundCheck(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var itemPropertiesFld = AccessTools.Field(typeof(GrabbableObject), nameof(GrabbableObject.itemProperties));
        var spawnsOnGroundFld = AccessTools.Field(typeof(Item), nameof(Item.itemSpawnsOnGround));

        var replacementMethod = AccessTools.Method(typeof(GrabbableStartPatch), nameof(NewSpawnOnGroundCheck));
        
        var matcher = new CodeMatcher(codes);


        matcher.MatchForward(false, 
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, itemPropertiesFld),
            new CodeMatch(OpCodes.Ldfld, spawnsOnGroundFld),
            new CodeMatch(OpCodes.Brfalse)
            );

        if (matcher.IsInvalid)
        {
            return codes;
        }

        matcher.Advance(1);

        matcher.RemoveInstructions(2);

        matcher.Insert(new CodeInstruction(OpCodes.Call, replacementMethod));
        
        MattyFixes.Log.LogDebug("GrabbableObject.Start patched!");

        return matcher.Instructions();
    }

    private static bool NewSpawnOnGroundCheck(GrabbableObject grabbableObject)
    {
        var ret = grabbableObject.itemProperties.itemSpawnsOnGround;

        MattyFixes.VerboseItemsLog(LogLevel.Debug, () =>
            $"{grabbableObject.itemProperties.itemName}({grabbableObject.NetworkObjectId}) processing GrabbableObject pos {grabbableObject.transform.position}");
        
        //run normal code if settings are off
        if (!MattyFixes.PluginConfig.OutOfBounds.Enabled.Value && !MattyFixes.PluginConfig.CupBoard.Enabled.Value)
            return ret;
        
        //or if it's one of the pre-existing items
        if (grabbableObject is ClipboardItem ||
            (grabbableObject is PhysicsProp && grabbableObject.itemProperties.itemName == "Sticky note"))
            return ret;
        
        if (!StartOfRound.Instance.localPlayerController || StartOfRoundPatch._isInitializingGame)
        {
            if (MattyFixes.PluginConfig.OutOfBounds.Enabled.Value)
            {
                ret = grabbableObject.IsServer;
            }
            
            if (MattyFixes.PluginConfig.CupBoard.Enabled.Value)
            {
                if (CupBoardFix.Closet.gameObject &&
                    grabbableObject.transform.parent == CupBoardFix.Closet.gameObject.transform)
                    ret = false;
            }
        }
        
        MattyFixes.VerboseItemsLog(LogLevel.Debug, () =>
                $"{grabbableObject.itemProperties.itemName}({grabbableObject.NetworkObjectId}) processing GrabbableObject spawnState " +
                $"OnGround - was: {grabbableObject.itemProperties.itemSpawnsOnGround} new:{ret}");
        
        return ret;
    }
}