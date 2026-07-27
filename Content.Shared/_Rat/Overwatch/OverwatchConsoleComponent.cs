using Content.Shared._Mono.Company;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Rat.Overwatch;

/// <summary>
/// Компонент консоли Overwatch для отслеживания членов фракции.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OverwatchConsoleComponent : Component
{
    /// <summary>
    /// Фракция, которую отслеживает эта консоль.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CompanyPrototype> Faction = "None";

    /// <summary>
    /// Текущий фильтр по статусу.
    /// </summary>
    [DataField, AutoNetworkedField]
    public OverwatchMemberStatus? StatusFilter;

    /// <summary>
    /// Текущий фильтр по отряду.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? SquadFilter;

    /// <summary>
    /// Текущий поисковый запрос.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string SearchQuery = string.Empty;
}
