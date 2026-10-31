using JetBrains.Annotations;
using MattyFixes.Utils;
using UnityEngine;

namespace MattyFixes.Interfaces;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public interface IItemVariant
{
    Item ItemProperties { get; }

    ItemCategory.ItemType Type { get; }

    ItemCategory.ItemLibrary Library { get; }

    string Identifier { get; }

    Vector3 RestingRotation { get; internal set; }

    int FloorYOffset { get; internal set; }

    float VerticalOffset { get; internal set; }
}
