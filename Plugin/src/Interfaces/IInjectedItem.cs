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

    public ItemCategory.ItemCategoryHolder? MattyFixes_ItemCategory { get; internal set; }
}
