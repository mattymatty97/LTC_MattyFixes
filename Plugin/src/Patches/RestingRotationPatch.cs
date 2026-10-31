using System;
using System.Globalization;
using HarmonyLib;
using MattyFixes.Config;
using MattyFixes.Dependency;
using MattyFixes.Interfaces;
using MattyFixes.Utils;
using Unity.Netcode;
using UnityEngine;

namespace MattyFixes.Patches;

[HarmonyPatch]
internal static class RestingRotationPatch
{
    private static void TryUpdateItemRotation(IItemVariant item)
    {
        var itemPath = item.Identifier;

        if (!MattyFixes.PluginConfig.ItemClipping.ItemConfigs.TryGetValue(itemPath, out var configHolder))
        {
            MattyFixes.PluginConfig.ItemClipping.ItemConfigs[itemPath] = configHolder = new ConfigHolder();
        }

        if (!MattyFixes.PluginConfig.ItemClipping.ItemRotations.TryGetValue(itemPath, out var rotationConfig))
        {
            var lastSeparator = itemPath.LastIndexOf('/');

            var itemSection = lastSeparator > 0 ? itemPath[..lastSeparator] : "Unknown";
            var itemName = ItemCategory.SanitizeForConfig(itemPath[(lastSeparator + 1)..]);

            itemSection = itemSection.Replace('/', '|');
            itemSection = ItemCategory.SanitizeForConfig(itemSection);

            var ogRotation = item.RestingRotation;
            ogRotation.y = item.FloorYOffset;

            var gameDefault =
                $"{ogRotation.x.ToString(CultureInfo.InvariantCulture)},{ogRotation.y.ToString(CultureInfo.InvariantCulture)},{ogRotation.z.ToString(CultureInfo.InvariantCulture)}";

            var defValue = "default";

            if (PluginResources.ItemClippingFixRotations.TryGetValue(itemPath, out var value))
            {
                defValue = $"{value[0].ToString(CultureInfo.InvariantCulture)},{value[1].ToString(CultureInfo.InvariantCulture)},{value[2].ToString(CultureInfo.InvariantCulture)}";
            }

            if (MattyFixes.ConfigOverrides.TryGetValue(itemPath, out var overrideValue) && overrideValue.RestingRotation != null)
            {
                defValue = "override";
            }

            configHolder.RestingRotation = MattyFixes.Instance.RotationsConfig.Bind(
                itemSection,
                itemName,
                defValue,
                $"Comma separated Vector3 rotation\nuse `default` for the default rotation\nuse `override` for the rotations specified in the override files\ndefault = '{gameDefault}'");

            rotationConfig = new ItemRotationConfig(
                ogRotation,
                defValue == "override" ? overrideValue!.RestingRotation : null
            );

            MattyFixes.PluginConfig.ItemClipping.ItemRotations[itemPath] = rotationConfig;
            configHolder.RestingRotation.SettingChanged += (_, _) => { TryUpdateItemRotation(item); };
            if (LethalConfigProxy.Enabled)
                LethalConfigProxy.AddConfig(configHolder.RestingRotation);
        }

        var parsedRotation = rotationConfig.Original;

        if (configHolder.RestingRotation.Value == "override" && rotationConfig.OverrideValue.HasValue)
            parsedRotation = rotationConfig.OverrideValue.Value;
        else
        {
            var rotation = configHolder.RestingRotation.Value.Split(",");

            if (rotation.Length == 3)
                if (float.TryParse(rotation[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                    if (float.TryParse(rotation[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                        if (float.TryParse(rotation[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                            parsedRotation = new Vector3(x, y, z);
        }

        item.RestingRotation = parsedRotation;
        item.FloorYOffset = (int)parsedRotation.y;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    private static void RegisterItems(StartOfRound __instance, bool __runOriginal)
    {
        if (!MattyFixes.PluginConfig.ItemClipping.Enabled.Value || !__runOriginal)
            return;

        foreach (var item in __instance.allItemsList.itemsList)
        {
            try
            {
                foreach (var variant in item.GetSupportedVariants())
                {
                    TryUpdateItemRotation(variant);
                }
            }
            catch (Exception ex)
            {
                MattyFixes.Log.LogError($"{item.GetIdentifier()} crashed badly ! {ex}");
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(900)]
    [HarmonyPatch(typeof(NetworkBehaviour), nameof(NetworkBehaviour.OnNetworkSpawn))]
    private static void AtGrabbableNetworkSpawn(NetworkBehaviour __instance)
    {
        if (__instance is not GrabbableObject grabbable)
            return;

        var itemVariant = grabbable.GetVariant();

        TryUpdateItemRotation(itemVariant);
    }
}
