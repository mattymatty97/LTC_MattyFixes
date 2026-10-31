using MattyFixes.Utils;

namespace MattyFixes.ItemVariants;

public sealed class ModdedItem(Item itemProperties, ItemCategory.ItemLibrary library) : SimpleItem(itemProperties)
{
    public override ItemCategory.ItemType Type => ItemCategory.ItemType.Modded;
    public override ItemCategory.ItemLibrary Library => library;
}
