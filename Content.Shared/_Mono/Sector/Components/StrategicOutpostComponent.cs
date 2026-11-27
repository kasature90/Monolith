using Content.Shared._Mono.Company;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mono.Sector.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StrategicOutpostComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<CompanyPrototype>? Owner;

    [DataField, AutoNetworkedField]
    public SectorControl Control = SectorControl.Unclaimed;

    [DataField, AutoNetworkedField]
    public int SectorRadius = 2;

    [DataField, AutoNetworkedField]
    public Dictionary<StrategicResource, int> Pools = new();

    [DataField, AutoNetworkedField]
    public float ClaimDuration = 60f;

    [DataField, AutoNetworkedField]
    public float ClaimProgress;

    [DataField, AutoNetworkedField]
    public ProtoId<CompanyPrototype>? ClaimingCompany;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan? ClaimStart;

    [DataField, AutoNetworkedField]
    public bool Relinquishing;

    [DataField, AutoNetworkedField]
    public bool BuyEnabled = true;

    [DataField, AutoNetworkedField]
    public bool SellEnabled = true;

    [DataField, AutoNetworkedField]
    public Dictionary<StrategicResource, int> ResourcePrices = new()
    {
        { StrategicResource.Fuel, 300 },
        { StrategicResource.Munitions, 300 },
        { StrategicResource.Parts, 300 },
    };
}
