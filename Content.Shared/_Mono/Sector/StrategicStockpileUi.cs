using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mono.Sector;

[Serializable, NetSerializable]
public enum StrategicStockpileUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class StrategicStockpileBoundUserInterfaceState : BoundUserInterfaceState
{
    public string OutpostName = string.Empty;
    public ProtoId<CompanyPrototype>? Owner;
    public bool BuyEnabled;
    public bool SellEnabled;
    public int Balance;
    public int PassiveIncome;
    public float PassiveInterval;
    public Dictionary<StrategicResource, int> ResourcePrices = new();
    public Dictionary<StrategicResource, int> Pools = new();
    public bool IsOwner;
}

[Serializable, NetSerializable]
public sealed class StrategicStockpileBuyMessage : BoundUserInterfaceMessage
{
    public StrategicResource Resource;
    public int Amount = 1;
}

[Serializable, NetSerializable]
public sealed class StrategicStockpileWithdrawMessage : BoundUserInterfaceMessage
{
    public int Amount;
}
