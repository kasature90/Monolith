using Content.Server._NF.Bank;
using Content.Server.Popups;
using Content.Shared._Mono.Company;
using Content.Shared._Mono.Sector;
using Content.Shared._Mono.Sector.Components;
using Content.Shared._NF.Bank;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Sector;

public sealed partial class StrategicDepotSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private BankSystem _bank = default!;

    private Dictionary<string, StrategicTradeContributionPrototype>? _byGood;
    private TimeSpan _nextUiRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StrategicStockpileTerminalComponent, InteractUsingEvent>(OnStockpileInteractUsing);

        Subs.BuiEvents<StrategicStockpileTerminalComponent>(StrategicStockpileUiKey.Key,
            subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnStockpileOpened);
            subs.Event<StrategicStockpileBuyMessage>(OnBuy);
            subs.Event<StrategicStockpileWithdrawMessage>(OnWithdraw);
        });

        _protos.PrototypesReloaded += _ => _byGood = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StrategicOutpostComponent>();
        while (query.MoveNext(out var grid, out var outpost))
        {
            TickClaim(grid, outpost);
            TickPassiveIncome(grid, outpost);
        }

        if (_timing.CurTime < _nextUiRefresh)
            return;

        _nextUiRefresh = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        RefreshOpenStockpileUis();
    }

    private void TickClaim(EntityUid grid, StrategicOutpostComponent outpost)
    {
        if (outpost.ClaimStart == null)
            return;

        var elapsed = (float)(_timing.CurTime - outpost.ClaimStart.Value).TotalSeconds;
        var duration = Math.Max(0.01f, outpost.ClaimDuration);

        if (outpost.Relinquishing)
        {
            outpost.ClaimProgress = 1f - Math.Clamp(elapsed / duration, 0f, 1f);
            if (outpost.ClaimProgress <= 0f)
            {
                outpost.ClaimProgress = 0f;
                outpost.ClaimStart = null;
                outpost.Relinquishing = false;
                outpost.Owner = null;
                outpost.ClaimingCompany = null;
                outpost.Control = SectorControl.Unclaimed;
            }

            Dirty(grid, outpost);
            return;
        }

        if (outpost.ClaimingCompany == null)
            return;

        outpost.ClaimProgress = Math.Clamp(elapsed / duration, 0f, 1f);
        if (outpost.ClaimProgress >= 1f)
        {
            outpost.ClaimProgress = 1f;
            outpost.ClaimStart = null;
            outpost.Owner = outpost.ClaimingCompany;
            outpost.ClaimingCompany = null;
            outpost.Control = SectorControl.Controlled;
            outpost.Relinquishing = false;
        }

        Dirty(grid, outpost);
    }

    private void TickPassiveIncome(EntityUid grid, StrategicOutpostComponent outpost)
    {
        if (outpost.PassiveIncome <= 0 || outpost.PassiveIncomeInterval <= 0f)
            return;

        if (outpost.Control != SectorControl.Controlled || outpost.Owner == null)
            return;

        if (outpost.NextPassiveIncome <= TimeSpan.Zero)
        {
            outpost.NextPassiveIncome = _timing.CurTime + TimeSpan.FromSeconds(outpost.PassiveIncomeInterval);
            Dirty(grid, outpost);
            return;
        }

        if (_timing.CurTime < outpost.NextPassiveIncome)
            return;

        outpost.Balance += outpost.PassiveIncome;
        outpost.NextPassiveIncome = _timing.CurTime + TimeSpan.FromSeconds(outpost.PassiveIncomeInterval);
        Dirty(grid, outpost);
    }

    public bool TryGetOutpostFromMachine(EntityUid machine, out EntityUid grid, out StrategicOutpostComponent outpost)
    {
        grid = default!;
        outpost = null!;

        var xform = Transform(machine);
        if (xform.GridUid is not { } g)
            return false;

        if (!TryComp(g, out StrategicOutpostComponent? op))
            return false;

        grid = g;
        outpost = op;
        return true;
    }

    public void StartClaim(EntityUid grid, StrategicOutpostComponent outpost, ProtoId<CompanyPrototype> company)
    {
        outpost.Relinquishing = false;
        outpost.ClaimingCompany = company;
        outpost.ClaimStart = _timing.CurTime;
        outpost.ClaimProgress = 0f;
        if (outpost.Owner != company)
            outpost.Control = SectorControl.Contested;
        Dirty(grid, outpost);
    }

    public void StartRelinquish(EntityUid grid, StrategicOutpostComponent outpost)
    {
        outpost.Relinquishing = true;
        outpost.ClaimingCompany = null;
        outpost.ClaimStart = _timing.CurTime;
        outpost.ClaimProgress = 1f;
        Dirty(grid, outpost);
    }

    private Dictionary<string, StrategicTradeContributionPrototype> ByGood
    {
        get
        {
            if (_byGood != null)
                return _byGood;

            _byGood = new Dictionary<string, StrategicTradeContributionPrototype>();
            foreach (var proto in _protos.EnumeratePrototypes<StrategicTradeContributionPrototype>())
            {
                _byGood[proto.GoodPrototype] = proto;
            }

            return _byGood;
        }
    }

    private bool TryGetActorCompany(EntityUid actor, out ProtoId<CompanyPrototype> company)
    {
        company = default;

        if (!TryComp(actor, out CompanyComponent? comp))
            return false;

        // ProtoId<"None"> is the empty affiliation
        if (comp.CompanyName == "None" || string.IsNullOrWhiteSpace(comp.CompanyName.Id))
            return false;

        company = comp.CompanyName;
        return true;
    }

    private static bool SameCompany(ProtoId<CompanyPrototype>? a, ProtoId<CompanyPrototype>? b)
    {
        if (a == null || b == null)
            return false;

        return a.Value.Id == b.Value.Id;
    }

    private void OnStockpileOpened(Entity<StrategicStockpileTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateStockpileUi(ent.Owner, args.Actor);
    }

    private void UpdateStockpileUi(EntityUid terminal, EntityUid? actor = null)
    {
        if (!TryGetOutpostFromMachine(terminal, out var grid, out var outpost))
            return;

        var isOwner = false;
        if (actor != null && TryGetActorCompany(actor.Value, out var company))
            isOwner = SameCompany(outpost.Owner, company);

        _ui.SetUiState(terminal,
            StrategicStockpileUiKey.Key,
            new StrategicStockpileBoundUserInterfaceState
        {
            OutpostName = MetaData(grid).EntityName,
            Owner = outpost.Owner,
            BuyEnabled = outpost.BuyEnabled,
            SellEnabled = outpost.SellEnabled,
            Balance = outpost.Balance,
            PassiveIncome = outpost.PassiveIncome,
            PassiveInterval = outpost.PassiveIncomeInterval,
            ResourcePrices = new Dictionary<StrategicResource, int>(outpost.ResourcePrices),
            Pools = new Dictionary<StrategicResource, int>(outpost.Pools),
            IsOwner = isOwner,
        });
    }

    private void RefreshOpenStockpileUis()
    {
        var query = EntityQueryEnumerator<StrategicStockpileTerminalComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out _, out var ui))
        {
            if (!_ui.IsUiOpen((uid, ui), StrategicStockpileUiKey.Key))
                continue;

            // Must refresh per actor or IsOwner is wrong
            foreach (var actor in _ui.GetActors((uid, ui), StrategicStockpileUiKey.Key))
            {
                UpdateStockpileUi(uid, actor);
            }
        }
    }

    private void OnStockpileInteractUsing(Entity<StrategicStockpileTerminalComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetOutpostFromMachine(ent.Owner, out var grid, out var outpost))
            return;

        // Cash deposit into outpost bank
        if (TryComp<StackComponent>(args.Used, out var stack) &&
            MetaData(args.Used).EntityPrototype?.ID is { } protoId &&
            (protoId == "SpaceCash" || protoId.StartsWith("SpaceCash")))
        {
            args.Handled = true;
            var amount = stack.Count;
            outpost.Balance += amount;
            Dirty(grid, outpost);
            QueueDel(args.Used);
            _popup.PopupEntity(
                Loc.GetString("strategic-stockpile-cash-deposited", ("amount", amount), ("balance", outpost.Balance)),
                ent.Owner,
                args.User);
            UpdateStockpileUi(ent.Owner, args.User);
            return;
        }

        // Trade good sell into pools
        var meta = MetaData(args.Used);
        if (meta.EntityPrototype == null)
            return;

        if (!ByGood.TryGetValue(meta.EntityPrototype.ID, out var contrib))
            return;

        args.Handled = true;

        if (!outpost.SellEnabled)
        {
            _popup.PopupEntity(Loc.GetString("strategic-stockpile-sell-disabled"), ent.Owner, args.User);
            return;
        }

        var unit = outpost.ResourcePrices.GetValueOrDefault(contrib.Resource, 0);
        var payout = unit * contrib.Amount;

        if (payout > outpost.Balance)
        {
            _popup.PopupEntity(Loc.GetString("strategic-stockpile-insufficient-funds"), ent.Owner, args.User);
            return;
        }

        outpost.Balance -= payout;
        outpost.Pools[contrib.Resource] = outpost.Pools.GetValueOrDefault(contrib.Resource) + contrib.Amount;
        Dirty(grid, outpost);
        QueueDel(args.Used);

        if (payout > 0)
        {
            var cash = SpawnAtPosition("SpaceCash", Transform(args.User).Coordinates);
            if (TryComp<StackComponent>(cash, out var cashStack))
                _stacks.SetCount(cash, payout, cashStack);
        }

        _popup.PopupEntity(
            Loc.GetString("strategic-stockpile-deposited",
                ("resource", contrib.Resource.ToString()),
                ("amount", contrib.Amount),
                ("paid", payout)),
            ent.Owner,
            args.User);
        UpdateStockpileUi(ent.Owner, args.User);
    }

    private void OnBuy(Entity<StrategicStockpileTerminalComponent> ent, ref StrategicStockpileBuyMessage args)
    {
        if (!TryGetOutpostFromMachine(ent.Owner, out var grid, out var outpost))
            return;

        if (!outpost.BuyEnabled)
        {
            _popup.PopupEntity(Loc.GetString("strategic-stockpile-buy-disabled"), ent.Owner, args.Actor);
            return;
        }

        var amount = Math.Max(1, args.Amount);
        var available = outpost.Pools.GetValueOrDefault(args.Resource, 0);
        if (available < amount)
        {
            _popup.PopupEntity(Loc.GetString("strategic-stockpile-insufficient-stock"), ent.Owner, args.Actor);
            return;
        }

        var unit = outpost.ResourcePrices.GetValueOrDefault(args.Resource, 0);
        var cost = unit * amount;

        if (!TryChargeActor(args.Actor, cost))
        {
            _popup.PopupEntity(Loc.GetString("strategic-stockpile-cannot-afford", ("cost", cost)), ent.Owner, args.Actor);
            return;
        }

        outpost.Pools[args.Resource] = available - amount;
        outpost.Balance += cost;
        Dirty(grid, outpost);

        if (TryFindGoodForResource(args.Resource, amount, out var goodId, out var goodsToSpawn))
        {
            for (var i = 0; i < goodsToSpawn; i++)
            {
                SpawnAtPosition(goodId, Transform(args.Actor).Coordinates);
            }
        }

        _popup.PopupEntity(
            Loc.GetString("strategic-stockpile-bought",
                ("resource", args.Resource.ToString()),
                ("amount", amount),
                ("cost", cost)),
            ent.Owner,
            args.Actor);
        UpdateStockpileUi(ent.Owner, args.Actor);
    }

    private bool TryFindGoodForResource(StrategicResource resource, int amount, out string goodId, out int count)
    {
        goodId = string.Empty;
        count = 0;

        StrategicTradeContributionPrototype? best = null;
        foreach (var proto in _protos.EnumeratePrototypes<StrategicTradeContributionPrototype>())
        {
            if (proto.Resource != resource || proto.Amount <= 0)
                continue;

            if (amount % proto.Amount != 0)
                continue;

            if (best == null || proto.Amount > best.Amount)
                best = proto;
        }

        if (best == null)
            return false;

        goodId = best.GoodPrototype;
        count = amount / best.Amount;
        return count > 0;
    }

    private bool TryChargeActor(EntityUid actor, int cost)
    {
        if (cost <= 0)
            return true;

        if (TryComp<HandsComponent>(actor, out var hands))
        {
            foreach (var hand in hands.Hands.Values)
            {
                if (hand.HeldEntity is not { } held)
                    continue;

                if (!TryComp<StackComponent>(held, out var stack))
                    continue;

                var proto = MetaData(held).EntityPrototype?.ID;
                if (proto == null || (proto != "SpaceCash" && !proto.StartsWith("SpaceCash")))
                    continue;

                if (stack.Count < cost)
                    continue;

                _stacks.SetCount(held, stack.Count - cost, stack);
                if (stack.Count <= 0)
                    QueueDel(held);
                return true;
            }
        }

        return _bank.TryBankWithdraw(actor, cost);
    }

    private void OnWithdraw(Entity<StrategicStockpileTerminalComponent> ent, ref StrategicStockpileWithdrawMessage args)
    {
        if (!TryGetOutpostFromMachine(ent.Owner, out var grid, out var outpost))
            return;

        if (!TryGetActorCompany(args.Actor, out var company) || !SameCompany(outpost.Owner, company))
        {
            _popup.PopupEntity(Loc.GetString("strategic-control-not-owner"), ent.Owner, args.Actor);
            return;
        }

        var amount = Math.Clamp(args.Amount, 0, outpost.Balance);
        if (amount <= 0)
            return;

        outpost.Balance -= amount;
        Dirty(grid, outpost);

        var cash = Spawn("SpaceCash", Transform(args.Actor).Coordinates);
        if (TryComp<StackComponent>(cash, out var stack))
            _stacks.SetCount(cash, amount, stack);

        _popup.PopupEntity(
            Loc.GetString("strategic-stockpile-cash-withdrawn", ("amount", amount), ("balance", outpost.Balance)),
            ent.Owner,
            args.Actor);
        UpdateStockpileUi(ent.Owner, args.Actor);
    }
}
