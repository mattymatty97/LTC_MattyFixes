using MattyFixes.Utils;

namespace MattyFixes.ItemVariants;

public sealed class VanillaItem(Item itemProperties) : SimpleItem(itemProperties)
{
    public override ItemCategory.ItemType Type => ItemCategory.ItemType.Vanilla;
    public override ItemCategory.ItemLibrary Library => ItemCategory.ItemLibrary.Vanilla;
}
