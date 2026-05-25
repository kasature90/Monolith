using Content.Shared.Weather;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Mono.Planets;

[RegisterComponent]
public sealed partial class PlanetMapComponent : Component
{
    [DataField]
    public string Parallax = "bedrock";

    /// <summary>
    /// If theres a planet prototype set, loads this weather onto it.
    /// </summary>
    [DataField]
    public ProtoId<WeatherPrototype>? PlanetWeather;

    /// <summary>
    /// How much time until the thing ends or whatever who cares.
    /// </summary>
    [DataField]
    public double PlanetWeatherEndTime;
}
// Only excludes a grid from garbage clean really.
