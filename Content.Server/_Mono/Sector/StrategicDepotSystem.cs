using Content.Shared._Mono.Company;
using Content.Shared._Mono.Sector;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Sector;

public sealed partial class StrategicDepotSystem : EntitySystem
{
    [Dependency] private SectorSystem _sectors = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;

    private Dictionary<string, (StrategicResource Resource, int Amount)>? _contributions;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StrategicDepotComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<StrategicDepotComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        _proto.PrototypesReloaded += _ => _contributions = null;
    }

    private Dictionary<string, (StrategicResource Resource, int Amount)> GetContributions()
    {
        if (_contributions != null)
            return _contributions;

        var map = new Dictionary<string, (StrategicResource, int)>();
        foreach (var proto in _proto.EnumeratePrototypes<StrategicTradeContributionPrototype>())
            map[proto.GoodPrototype] = (proto.Resource, proto.Amount);

        _contributions = map;
        return map;
    }

    private void OnInteractUsing(EntityUid uid, StrategicDepotComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetEntityPrototypeId(args.Used, out var protoId))
            return;

        if (!GetContributions().TryGetValue(protoId, out var contrib))
        {
            _popup.PopupEntity(Loc.GetString("strategic-depot-no-value"), uid, args.User);
            return;
        }

        if (!_sectors.TryGetOutpostFromDepot(uid, out var outpost))
        {
            _popup.PopupEntity(Loc.GetString("strategic-depot-no-outpost"), uid, args.User);
            return;
        }

        _sectors.AddResource(outpost, contrib.Resource, contrib.Amount);
        QueueDel(args.Used);
        args.Handled = true;

        _popup.PopupEntity(
            Loc.GetString("strategic-depot-deposited",
                ("amount", contrib.Amount),
                ("resource", Loc.GetString($"strategic-resource-{contrib.Resource.ToString().ToLowerInvariant()}"))),
            uid,
            args.User);
    }

    private void OnGetAltVerbs(EntityUid uid, StrategicDepotComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("strategic-depot-verb-claim"),
            Act = () => TryClaim(uid, args.User),
            Priority = 2,
        });
    }

    private void TryClaim(EntityUid depot, EntityUid user)
    {
        if (!TryComp<CompanyComponent>(user, out var company) || company.CompanyName == "None")
        {
            _popup.PopupEntity(Loc.GetString("strategic-depot-no-company"), depot, user);
            return;
        }

        if (!_sectors.TryGetOutpostFromDepot(depot, out var outpost))
        {
            _popup.PopupEntity(Loc.GetString("strategic-depot-no-outpost"), depot, user);
            return;
        }

        var o = outpost.Comp;

        if (o.Control == SectorControl.Controlled && o.Owner == company.CompanyName)
        {
            _popup.PopupEntity(Loc.GetString("strategic-depot-already-owned"), depot, user);
            return;
        }

        if (o.ClaimingCompany != company.CompanyName)
        {
            o.ClaimingCompany = company.CompanyName;
            o.ClaimProgress = 0f;
            o.ClaimStartedAt = _timing.CurTime;

            if (o.Control == SectorControl.Controlled && o.Owner != null && o.Owner != company.CompanyName)
                _sectors.SetControl(outpost, SectorControl.Contested, o.Owner);
            else
                Dirty(outpost);

            _popup.PopupEntity(Loc.GetString("strategic-depot-claim-started"), depot, user);
            return;
        }

        var elapsed = (float)(_timing.CurTime - (o.ClaimStartedAt ?? _timing.CurTime)).TotalSeconds;
        o.ClaimProgress = Math.Clamp(elapsed / Math.Max(o.ClaimSeconds, 1f), 0f, 1f);
        Dirty(outpost);

        if (o.ClaimProgress < 1f)
        {
            _popup.PopupEntity(
                Loc.GetString("strategic-depot-claim-progress", ("percent", (int)(o.ClaimProgress * 100))),
                depot,
                user);
            return;
        }

        _sectors.SetControl(outpost, SectorControl.Controlled, company.CompanyName);
        o.ClaimProgress = 0f;
        o.ClaimStartedAt = null;
        o.ClaimingCompany = null;
        Dirty(outpost);

        _popup.PopupEntity(Loc.GetString("strategic-depot-claim-complete"), depot, user);
    }

    private bool TryGetEntityPrototypeId(EntityUid uid, out string id)
    {
        id = string.Empty;
        if (!TryComp<MetaDataComponent>(uid, out var meta) || meta.EntityPrototype == null)
            return false;

        id = meta.EntityPrototype.ID;
        return true;
    }
}
