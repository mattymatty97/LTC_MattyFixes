using MattyFixes.Interfaces;
using MattyFixes.Utils;
using UnityEngine;

namespace MattyFixes.ItemVariants;

public abstract class SimpleItem(Item itemProperties, string identifier) : IItemVariant
{
    public Item ItemProperties { get; } = itemProperties;

    public abstract ItemCategory.ItemType Type { get; }

    public abstract ItemCategory.ItemLibrary Library { get; }
    public string Identifier => identifier;

    public Vector3 RestingRotation
    {
        get => ItemProperties.restingRotation;
        set => ItemProperties.restingRotation = value;
    }

    public int FloorYOffset
    {
        get => ItemProperties.floorYOffset;
        set => ItemProperties.floorYOffset = value;
    }

    public float VerticalOffset
    {
        get => ItemProperties.verticalOffset;
        set => ItemProperties.verticalOffset = value;
    }
}
