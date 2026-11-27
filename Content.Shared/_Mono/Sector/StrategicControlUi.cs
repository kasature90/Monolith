using System.Numerics;
using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Sector;

[Serializable, NetSerializable]
public enum StrategicControlUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class StrategicControlBoundUserInterfaceState : BoundUserInterfaceState
{
    public string OutpostName = string.Empty;
    public ProtoId<CompanyPrototype>? Owner;
    public SectorControl Control;
    public float ClaimProgress;
    public bool Relinquishing;
    public bool BuyEnabled;
    public bool SellEnabled;
    public Dictionary<StrategicResource, int> ResourcePrices = new();
    public Dictionary<StrategicResource, int> Pools = new();
    public Vector2 WorldPos;
    public int SectorX;
    public int SectorY;
    public int SectorRadius;
}

[Serializable, NetSerializable]
public sealed class StrategicControlClaimMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class StrategicControlRelinquishMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class StrategicControlSetTradeMessage : BoundUserInterfaceMessage
{
    public bool BuyEnabled;
    public bool SellEnabled;
    public Dictionary<StrategicResource, int> ResourcePrices = new();
}
