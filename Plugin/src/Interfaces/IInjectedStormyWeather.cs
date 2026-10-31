
using InjectionLibrary;
using InjectionLibrary.Attributes;
using JetBrains.Annotations;

[assembly:RequiresInjections]

namespace MattyFixes.Interfaces;

[InjectInterface(typeof(StormyWeather))]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public interface IInjectedStormyWeather
{
    [HandleErrors(ErrorHandlingStrategy.Ignore)]
    public void Start();
}
