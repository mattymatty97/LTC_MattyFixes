using MattyFixes.Utils;

namespace MattyFixes.ItemVariants;

public sealed class UnknownItem(Item itemProperties, string identifier) : SimpleItem(itemProperties, identifier)
{
    public override ItemCategory.ItemType Type => ItemCategory.ItemType.Unknown;
    public override ItemCategory.ItemLibrary Library => ItemCategory.ItemLibrary.Unknown;
}
