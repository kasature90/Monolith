using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Sector;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class SectorCellData
{
    [DataField]
    public SectorControl Control = SectorControl.Unclaimed;

    [DataField]
    public ProtoId<CompanyPrototype>? Owner;

    [DataField]
    public Dictionary<StrategicResource, int> Pools = new()
    {
        { StrategicResource.Fuel, 0 },
        { StrategicResource.Munitions, 0 },
        { StrategicResource.Parts, 0 },
    };

    [DataField]
    public TimeSpan LastChange;
}
