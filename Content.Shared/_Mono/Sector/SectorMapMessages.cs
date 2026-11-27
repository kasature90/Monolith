using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Sector;

[Serializable, NetSerializable]
public sealed class RequestSectorMapMessage : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class SectorMapStateMessage : EntityEventArgs
{
    public List<SectorMapOutpostDto> Outposts = new();
}

[Serializable, NetSerializable]
public struct SectorMapOutpostDto
{
    public NetEntity Grid;
    public string Name;
    public int SectorX;
    public int SectorY;
    public int SectorRadius;
    public SectorControl Control;
    public ProtoId<CompanyPrototype>? Owner;
    public Dictionary<StrategicResource, int> Pools;
}
