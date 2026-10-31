namespace MattyFixes.Config;

internal class ItemOffsetConfig(float original, float? overrideValue)
{
    public float Original { get; } = original;

    public float? OverrideValue { get; } = overrideValue;

    public float? ComputedValue { get; internal set; } = null;

    public float CurrentValue { get; internal set; } = original;
}
