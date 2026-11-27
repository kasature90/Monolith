using Content.Server.Popups;
using Content.Shared._Mono.Company;
using Content.Shared._Mono.Sector;
using Content.Shared._Mono.Sector.Components;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Sector;

public sealed partial class StrategicDepotSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private Dictionary<string, StrategicTradeContributionPrototype>? _byGood;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StrategicStockpileTerminalComponent, InteractUsingEvent>(OnStockpileDeposit);
        _protos.PrototypesReloaded += _ => _byGood = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StrategicOutpostComponent>();
        while (query.MoveNext(out var grid, out var outpost))
        {
            if (outpost.ClaimStart == null)
                continue;

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
                continue;
            }

            if (outpost.ClaimingCompany == null)
                continue;

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
                _byGood[proto.GoodPrototype] = proto;

            return _byGood;
        }
    }

    private void OnStockpileDeposit(Entity<StrategicStockpileTerminalComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetOutpostFromMachine(ent.Owner, out var grid, out var outpost))
            return;

        var meta = MetaData(args.Used);
        if (meta.EntityPrototype == null)
            return;

        if (!ByGood.TryGetValue(meta.EntityPrototype.ID, out var contrib))
            return;

        args.Handled = true;

        outpost.Pools[contrib.Resource] = outpost.Pools.GetValueOrDefault(contrib.Resource) + contrib.Amount;

        var paid = 0;
        if (outpost.SellEnabled)
        {
            var unit = outpost.ResourcePrices.GetValueOrDefault(contrib.Resource, 0);
            paid = unit * contrib.Amount;
            if (paid > 0)
            {
                var cash = SpawnAtPosition("SpaceCash", Transform(args.User).Coordinates);
                if (TryComp<StackComponent>(cash, out var stack))
                    _stacks.SetCount(cash, paid, stack);
            }
        }

        Dirty(grid, outpost);
        QueueDel(args.Used);

        _popup.PopupEntity(
            Loc.GetString("strategic-stockpile-deposited",
                ("resource", contrib.Resource.ToString()),
                ("amount", contrib.Amount),
                ("paid", paid)),
            ent.Owner,
            args.User);
    }
}
