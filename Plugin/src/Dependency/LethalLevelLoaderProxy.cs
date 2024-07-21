using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using JetBrains.Annotations;
using LethalLevelLoader;

namespace MattyFixes.Dependency;

public static class LethalLevelLoaderProxy
{
    private static bool? _enabled;

    public static bool Enabled
    {
        get
        {
            _enabled ??= Chainloader.PluginInfos.ContainsKey("imabatby.lethallevelloader");
            return _enabled.Value;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void GetModdedItems([NotNull] in Dictionary<Item, string> items)
    {
        
        MattyFixes.Log.LogWarning("LethalLevelLoader found, reading PatchedContent.ExtendedItems");
        foreach (var extendedItem in PatchedContent.ExtendedItems)
        {
            if (extendedItem.ExtendedMod == PatchedContent.VanillaMod)
                continue;

            items.TryAdd(extendedItem.Item, extendedItem.ModName);
        }
    }
}