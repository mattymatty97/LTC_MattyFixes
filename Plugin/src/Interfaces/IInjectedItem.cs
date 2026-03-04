using InjectionLibrary;
using InjectionLibrary.Attributes;
using MattyFixes.Utils;

[assembly:RequiresInjections]

namespace MattyFixes.Interfaces;

[InjectInterface(typeof(Item))]
public interface IInjectedItem
{
    [HandleErrors(ErrorHandlingStrategy.Ignore)]
    public void Awake();

    public bool                     MattyFixes_IsRegistered         { get; set; }
    public bool                     MattyFixes_HasComputedOffset    { get; set; }

    public ItemCategory.ItemType    MattyFixes_ItemType             { get; set; }
    public string                   MattyFixes_Path                 { get; set; }
}
