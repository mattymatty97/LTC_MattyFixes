using InjectionLibrary;
using InjectionLibrary.Attributes;
using JetBrains.Annotations;
using MattyFixes.Utils;

[assembly: RequiresInjections]

namespace MattyFixes.Interfaces;

[InjectInterface(typeof(Item))]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public interface IInjectedItem
{
    [HandleErrors(ErrorHandlingStrategy.Ignore)]
    public void Awake();

    public ItemCategory.ItemType MattyFixes_ItemType { get; set; }
    public ItemCategory.ItemLibrary MattyFixes_Library { get; set; }
    public IItemVariant MattyFixes_ItemVariant { get; set; }
    public string MattyFixes_Path { get; set; }
}
