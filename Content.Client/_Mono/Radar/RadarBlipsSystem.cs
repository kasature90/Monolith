using System.Numerics;
using Content.Shared._Mono.Radar;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Client._Mono.Radar;

public sealed partial class RadarBlipsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IMapManager _map = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private const double BlipStaleSeconds = 3.0;
    private TimeSpan _lastRequestTime = TimeSpan.Zero;
    private static readonly TimeSpan RequestThrottle = TimeSpan.FromMilliseconds(500);

    private TimeSpan _lastUpdatedTime;
    private List<BlipNetData> _blips = new();
    private List<MissileVectorNetData> _missiles = new();
    private List<HitscanNetData> _hitscans = new();
    private List<BlipConfig> _configPalette = new();

    // cached results to avoid allocating on every draw/frame
    private readonly List<BlipData> _cachedBlipData = new();
    private readonly List<MissileVectorData> _cachedMissileData = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GiveBlipsEvent>(HandleReceiveBlips);
        SubscribeNetworkEvent<BlipRemovalEvent>(RemoveBlip);
    }

    private void HandleReceiveBlips(GiveBlipsEvent ev, EntitySessionEventArgs args)
    {
        _configPalette = ev.ConfigPalette;
        _blips = ev.Blips;
        _missiles = ev.Missiles;
        _hitscans = ev.HitscanLines;
        _lastUpdatedTime = _timing.CurTime;
    }

    private void RemoveBlip(BlipRemovalEvent args)
    {
        var blipid = _blips.FirstOrDefault(x => x.Uid == args.NetBlipUid);
        _blips.Remove(blipid);
    }

    public void RequestBlips(EntityUid console)
    {
        // Only request if we have a valid console
        if (!Exists(console))
            return;

        // Add request throttling to avoid network spam
        if (_timing.CurTime - _lastRequestTime < RequestThrottle)
            return;

        _lastRequestTime = _timing.CurTime;

        var netConsole = GetNetEntity(console);
        var ev = new RequestBlipsEvent(netConsole);
        RaiseNetworkEvent(ev);
    }

    /// <summary>
    /// Gets the current blips as world positions with their scale, color and shape.
    /// </summary>
    public List<BlipData> GetCurrentBlips()
    {
        // clear the cache and bail early if the data is stale
        _cachedBlipData.Clear();
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return _cachedBlipData;

        // populate the cached list instead of allocating a new one each frame
        foreach (var blip in _blips)
        {
            var coord = GetCoordinates(blip.Position);

            if (!coord.IsValid(EntityManager))
                continue;

            var predictedPos = new EntityCoordinates(coord.EntityId, coord.Position + blip.Vel * (float)(_timing.CurTime - _lastUpdatedTime).TotalSeconds);

            var predictedMap = _xform.ToMapCoordinates(predictedPos);

            var config = _configPalette[blip.ConfigIndex];
            var rotation = blip.Rotation;
            // hijack our shape if we're on a grid and we want to do that
            if (_map.TryFindGridAt(predictedMap, out var grid, out _) && grid != EntityUid.Invalid)
            {
                if (blip.OnGridConfigIndex is { } gridIdx)
                    config = _configPalette[gridIdx];
                rotation += Transform(grid).LocalRotation;
            }
            var maybeGrid = grid != EntityUid.Invalid ? grid : (EntityUid?)null;

            _cachedBlipData.Add(new(blip.Uid, predictedPos, rotation, maybeGrid, config));
        }

        return _cachedBlipData;
    }

    /// <summary>
    /// Gets the missile vectors to be rendered on the radar
    /// </summary>
    public List<MissileVectorData> GetMissileLines()
    {
        // clear the cache and bail early if the data is stale
        _cachedMissileData.Clear();
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return _cachedMissileData;

        // populate the cached list instead of allocating a new one each frame
        foreach (var missile in _missiles)
        {
            var coord = GetCoordinates(missile.Start);

            if (!coord.IsValid(EntityManager))
                continue;

            var predictedPosStart = new EntityCoordinates(coord.EntityId, coord.Position + missile.Vel * (float)(_timing.CurTime - _lastUpdatedTime).TotalSeconds);
            var posEnd = Vector2.Create(
                predictedPosStart.X + ((float)missile.Range) * (float)Math.Cos(missile.Rotation + Math.PI * -0.5),
                predictedPosStart.Y + ((float)missile.Range) * (float)Math.Sin(missile.Rotation + Math.PI * -0.5));
            var predictedPosEnd = new EntityCoordinates(coord.EntityId, posEnd);

            _cachedMissileData.Add(new(missile.Uid, predictedPosStart, predictedPosEnd, missile.Color));
        }

        return _cachedMissileData;
    }

    /// <summary>
    /// Gets the hitscan lines to be rendered on the radar
    /// </summary>
    public List<HitscanNetData> GetHitscanLines()
    {
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return new();

        return _hitscans;
    }
}

public record struct BlipData
(
    NetEntity NetUid,
    EntityCoordinates Position,
    Angle Rotation,
    EntityUid? GridUid,
    BlipConfig Config
);

public record struct MissileVectorData
(
    NetEntity NetUid,
    EntityCoordinates PositionStart,
    EntityCoordinates PositionEnd,
    Color Color
);
