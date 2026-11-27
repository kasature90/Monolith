using Robust.Shared.Prototypes;

namespace Content.Shared._Mono.Sector;

[Prototype]
public sealed partial class StrategicTradeContributionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId GoodPrototype;

    [DataField(required: true)]
    public StrategicResource Resource;

    [DataField]
    public int Amount = 1;
}
