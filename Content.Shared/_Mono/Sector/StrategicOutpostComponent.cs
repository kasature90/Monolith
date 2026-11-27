using Content.Shared._Mono.Company;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mono.Sector;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StrategicOutpostComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<CompanyPrototype>? Owner;

    [DataField, AutoNetworkedField]
    public SectorControl Control = SectorControl.Unclaimed;

    [DataField, AutoNetworkedField]
    public Dictionary<StrategicResource, int> Pools = new()
    {
        { StrategicResource.Fuel, 0 },
        { StrategicResource.Munitions, 0 },
        { StrategicResource.Parts, 0 },
    };

    [DataField, AutoNetworkedField]
    public int SectorRadius = 2;

    [DataField, AutoNetworkedField]
    public float ClaimSeconds = 60f;

    [DataField, AutoNetworkedField]
    public ProtoId<CompanyPrototype>? ClaimingCompany;

    [DataField, AutoNetworkedField]
    public float ClaimProgress;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan? ClaimStartedAt;
}
