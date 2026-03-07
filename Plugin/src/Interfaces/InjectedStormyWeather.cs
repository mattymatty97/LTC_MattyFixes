
using InjectionLibrary;
using InjectionLibrary.Attributes;

[assembly:RequiresInjections]

namespace MattyFixes.Interfaces;

[InjectInterface(typeof(StormyWeather))]
public interface InjectedStormyWeather
{
    [HandleErrors(ErrorHandlingStrategy.Ignore)]
    public void Start();
}
