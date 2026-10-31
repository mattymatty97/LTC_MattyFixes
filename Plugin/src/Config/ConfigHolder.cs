using BepInEx.Configuration;

namespace MattyFixes.Config;

public class ConfigHolder
{
    public ConfigEntry<string> RestingRotation { get; internal set; }
    public ConfigEntry<string>  VerticalOffset { get; internal set; }
}
