using Content.Shared._Mono.Company;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mono.Sector;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StrategicDepotComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ClaimSeconds = 60f;

    [DataField, AutoNetworkedField]
    public ProtoId<CompanyPrototype>? ClaimingCompany;

    [DataField, AutoNetworkedField]
    public float ClaimProgress;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan? ClaimStartedAt;
}
