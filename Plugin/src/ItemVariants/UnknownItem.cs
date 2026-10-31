using MattyFixes.Utils;

namespace MattyFixes.ItemVariants;

public sealed class UnknownItem(Item itemProperties) : SimpleItem(itemProperties)
{
    public override ItemCategory.ItemType Type => ItemCategory.ItemType.Unknown;
    public override ItemCategory.ItemLibrary Library => ItemCategory.ItemLibrary.Unknown;
}
