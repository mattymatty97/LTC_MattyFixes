using UnityEngine;

namespace MattyFixes.Config;

internal readonly struct ItemRotationConfig(Vector3 original, Vector3? overrideValue)
{
    public Vector3 Original { get; } = original;

    public Vector3? OverrideValue { get; } = overrideValue;
}
