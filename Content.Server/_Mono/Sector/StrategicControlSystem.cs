using Content.Server._Mono.Company;
using Content.Server.Popups;
using Content.Shared._Mono.Company;
using Content.Shared._Mono.Sector;
using Content.Shared._Mono.Sector.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Sector;

public sealed partial class StrategicControlSystem : EntitySystem
{
    [Dependency] private StrategicDepotSystem _depot = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextUiRefresh;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<StrategicControlTerminalComponent>(StrategicControlUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<StrategicControlClaimMessage>(OnClaim);
            subs.Event<StrategicControlRelinquishMessage>(OnRelinquish);
            subs.Event<StrategicControlSetTradeMessage>(OnSetTrade);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUiRefresh)
            return;

        _nextUiRefresh = _timing.CurTime + TimeSpan.FromSeconds(0.5);

        var query = EntityQueryEnumerator<StrategicControlTerminalComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out _, out var ui))
        {
            if (!_ui.IsUiOpen((uid, ui), StrategicControlUiKey.Key))
                continue;

            UpdateUi(uid);
        }
    }

    private void OnOpened(Entity<StrategicControlTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner);
    }

    private void UpdateUi(EntityUid terminal)
    {
        if (!_depot.TryGetOutpostFromMachine(terminal, out var grid, out var outpost))
            return;

        var worldPos = _transform.GetWorldPosition(Transform(grid));
        var cell = SectorGrid.FromWorld(worldPos);

        _ui.SetUiState(terminal, StrategicControlUiKey.Key, new StrategicControlBoundUserInterfaceState
        {
            OutpostName = MetaData(grid).EntityName,
            Owner = outpost.Owner,
            Control = outpost.Control,
            ClaimProgress = outpost.ClaimProgress,
            Relinquishing = outpost.Relinquishing,
            BuyEnabled = outpost.BuyEnabled,
            SellEnabled = outpost.SellEnabled,
            ResourcePrices = new Dictionary<StrategicResource, int>(outpost.ResourcePrices),
            Pools = new Dictionary<StrategicResource, int>(outpost.Pools),
            WorldPos = worldPos,
            SectorX = cell.X,
            SectorY = cell.Y,
            SectorRadius = outpost.SectorRadius,
        });
    }

    private bool TryGetActorCompany(EntityUid actor, IEntityManager ent, out ProtoId<CompanyPrototype> company)
    {
        company = default;
        if (!TryComp<CompanyComponent>(actor, out var comp))
            return false;

        if (string.IsNullOrEmpty(comp.CompanyName))
            return false;

        company = comp.CompanyName;
        return true;
    }

    private void OnClaim(Entity<StrategicControlTerminalComponent> ent, ref StrategicControlClaimMessage args)
    {
        if (!_depot.TryGetOutpostFromMachine(ent.Owner, out var grid, out var outpost))
            return;

        if (!TryGetActorCompany(args.Actor, EntityManager, out var company))
        {
            _popup.PopupEntity(Loc.GetString("strategic-control-no-company"), ent.Owner, args.Actor);
            return;
        }

        _depot.StartClaim(grid, outpost, company);
        UpdateUi(ent.Owner);
        _popup.PopupEntity(Loc.GetString("strategic-control-claim-started"), ent.Owner, args.Actor);
    }

    private void OnRelinquish(Entity<StrategicControlTerminalComponent> ent, ref StrategicControlRelinquishMessage args)
    {
        if (!_depot.TryGetOutpostFromMachine(ent.Owner, out var grid, out var outpost))
            return;

        if (!TryGetActorCompany(args.Actor, EntityManager, out var company) || outpost.Owner != company)
        {
            _popup.PopupEntity(Loc.GetString("strategic-control-not-owner"), ent.Owner, args.Actor);
            return;
        }

        _depot.StartRelinquish(grid, outpost);
        UpdateUi(ent.Owner);
        _popup.PopupEntity(Loc.GetString("strategic-control-relinquish-started"), ent.Owner, args.Actor);
    }

    private void OnSetTrade(Entity<StrategicControlTerminalComponent> ent, ref StrategicControlSetTradeMessage args)
    {
        if (!_depot.TryGetOutpostFromMachine(ent.Owner, out var grid, out var outpost))
            return;

        if (!TryGetActorCompany(args.Actor, EntityManager, out var company) || outpost.Owner != company)
        {
            _popup.PopupEntity(Loc.GetString("strategic-control-not-owner"), ent.Owner, args.Actor);
            return;
        }

        outpost.BuyEnabled = args.BuyEnabled;
        outpost.SellEnabled = args.SellEnabled;

        foreach (var (res, price) in args.ResourcePrices)
        {
            outpost.ResourcePrices[res] = Math.Clamp(price, 0, 1000000);
        }

        Dirty(grid, outpost);
        UpdateUi(ent.Owner);
    }
}
