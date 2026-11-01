using MattyFixes.Utils;

namespace MattyFixes.ItemVariants;

public sealed class VanillaItem(Item itemProperties, string identifier) : SimpleItem(itemProperties, identifier)
{
    public override ItemCategory.ItemType Type => ItemCategory.ItemType.Vanilla;
    public override ItemCategory.ItemLibrary Library => ItemCategory.ItemLibrary.Vanilla;
}
