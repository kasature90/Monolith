using System.Numerics;
using Content.Shared._Mono.Company;
using Content.Shared._Mono.Sector;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Sector;

public sealed partial class SectorSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;

    public bool TryGetOutpostOnGrid(EntityUid grid, out Entity<StrategicOutpostComponent> outpost)
    {
        if (TryComp<StrategicOutpostComponent>(grid, out var comp))
        {
            outpost = (grid, comp);
            return true;
        }

        outpost = default;
        return false;
    }

    public bool TryGetOutpostFromDepot(EntityUid depot, out Entity<StrategicOutpostComponent> outpost)
    {
        var grid = _xform.GetGrid(depot);
        if (grid == null)
        {
            outpost = default;
            return false;
        }

        return TryGetOutpostOnGrid(grid.Value, out outpost);
    }

    public void AddResource(Entity<StrategicOutpostComponent> outpost, StrategicResource resource, int amount)
    {
        outpost.Comp.Pools.TryGetValue(resource, out var cur);
        outpost.Comp.Pools[resource] = Math.Max(0, cur + amount);
        Dirty(outpost);
    }

    public void SetControl(Entity<StrategicOutpostComponent> outpost, SectorControl control, ProtoId<CompanyPrototype>? owner)
    {
        outpost.Comp.Control = control;
        outpost.Comp.Owner = owner;
        Dirty(outpost);
    }

    public (SectorControl Control, ProtoId<CompanyPrototype>? Owner) GetSectorControl(Vector2 world)
    {
        var (sx, sy) = SectorGrid.FromWorld(world);
        ProtoId<CompanyPrototype>? owner = null;
        var controlled = false;
        var contested = false;

        var query = EntityQueryEnumerator<StrategicOutpostComponent, TransformComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out var outpost, out var xform, out _))
        {
            if (outpost.Control != SectorControl.Controlled || outpost.Owner == null)
                continue;

            var center = _xform.GetWorldPosition(uid);
            var (ox, oy) = SectorGrid.FromWorld(center);
            if (SectorGrid.Chebyshev(sx, sy, ox, oy) > outpost.SectorRadius)
                continue;

            if (!controlled)
            {
                controlled = true;
                owner = outpost.Owner;
            }
            else if (owner != outpost.Owner)
            {
                contested = true;
            }
        }

        if (contested)
            return (SectorControl.Contested, null);

        if (controlled)
            return (SectorControl.Controlled, owner);

        return (SectorControl.Unclaimed, null);
    }

    public SectorMapStateMessage BuildMapState()
    {
        var msg = new SectorMapStateMessage();
        var query = EntityQueryEnumerator<StrategicOutpostComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var outpost, out var xform, out var meta))
        {
            var world = _xform.GetWorldPosition(uid);
            var (sx, sy) = SectorGrid.FromWorld(world);

            msg.Outposts.Add(new SectorMapOutpostDto
            {
                Grid = GetNetEntity(uid),
                Name = meta.EntityName,
                SectorX = sx,
                SectorY = sy,
                SectorRadius = outpost.SectorRadius,
                Control = outpost.Control,
                Owner = outpost.Owner,
                Pools = new Dictionary<StrategicResource, int>(outpost.Pools),
                WorldPos = world,
            });
        }

        return msg;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestSectorMapMessage>(OnRequestMap);
    }

    private void OnRequestMap(RequestSectorMapMessage msg, EntitySessionEventArgs args)
    {
        RaiseNetworkEvent(BuildMapState(), args.SenderSession);
    }
}
