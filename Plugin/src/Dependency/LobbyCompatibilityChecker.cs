using System;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using LobbyCompatibility.Enums;
using LobbyCompatibility.Features;

namespace MattyFixes.Dependency;

public static class LobbyCompatibilityChecker
{
    public static bool Enabled => Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility");

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void Init()
    {
        PluginHelper.RegisterPlugin(MattyFixes.GUID, Version.Parse(MattyFixes.VERSION), CompatibilityLevel.ClientOnly,
            VersionStrictness.Minor);
    }
}