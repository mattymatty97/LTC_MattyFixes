using UnityEngine;

namespace MattyFixes.Config;

public class OverrideHolder
{
    public string   Source { get; internal set; } = nameof(MattyFixes);
    public int      Priority { get; internal set; } = 0;

    public Vector3? RestingRotation { get; internal set; } = null!;
    public float?   VerticalOffset { get; internal set; } = null!;
}
