using System.Runtime.CompilerServices;
using LobbyCompatibility.Enums;
using LobbyCompatibility.Features;

namespace MattyFixes.Dependency
{
    public static class LobbyCompatibilityChecker
    {
        public static bool Enabled { get { return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility"); } }
        
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Init()
        {
            PluginHelper.RegisterPlugin(MattyFixes.GUID, System.Version.Parse(MattyFixes.VERSION), CompatibilityLevel.ClientOnly, VersionStrictness.Minor);
        }
        
    }
}