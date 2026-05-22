using System.Linq;
using Content.Server._DV.Planet;
using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.GridPreloader;
using Content.Server.Parallax;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Parallax.Biomes;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking.Rules;

public sealed class LoadMapRuleSystem : StationEventSystem<LoadMapRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly GridPreloaderSystem _gridPreloader = default!;
    [Dependency] private readonly BiomeSystem _biome = default!; // Mono
    [Dependency] private readonly MetaDataSystem _meta = default!; // Mono
    [Dependency] private readonly AtmosphereSystem _atmos = default!; // Mono

    private readonly List<(Vector2i, Tile)> _setTiles = new(); // Mono

    protected override void Added(EntityUid uid, LoadMapRuleComponent comp, GameRuleComponent rule, GameRuleAddedEvent args)
    {
        if (comp.PreloadedGrid != null && !_gridPreloader.PreloadingEnabled)
        {
            // Preloading will never work if it's disabled, duh
            Log.Debug($"Immediately ending {ToPrettyString(uid):rule} as preloading grids is disabled by cvar.");
            ForceEndSelf(uid, rule);
            return;
        }

        MapId mapId;
        IReadOnlyList<EntityUid> grids;
        if (comp.GameMap != null)
        {
            // Component has one of three modes, only one of the three fields should ever be populated.
            DebugTools.AssertNull(comp.MapPath);
            DebugTools.AssertNull(comp.GridPath);
            DebugTools.AssertNull(comp.PreloadedGrid);

            var gameMap = _prototypeManager.Index(comp.GameMap.Value);
            grids = GameTicker.LoadGameMap(gameMap, out mapId, null);
            Log.Info($"Created map {mapId} for {ToPrettyString(uid):rule}");
        }
        // Mono start
        if (comp.PlanetMap != null)
        {
            var planet = _prototypeManager.Index(comp.PlanetMap);
            var map = _map.CreateMap(out mapId, runMapInit: true);
            var opts = DeserializationOptions.Default with {InitializeMaps = true};
            _biome.EnsurePlanet(map, _prototypeManager.Index(planet.Biome), mapLight: planet.MapLight);
            // add each marker layer
            var biome = Comp<BiomeComponent>(map);
            foreach (var layer in planet.BiomeMarkerLayers)
            {
                _biome.AddMarkerLayer(map, biome, layer);
            }
            if (planet.AddedComponents is {} added)
                EntityManager.AddComponents(map, added);
            _atmos.SetMapAtmosphere(map, false, planet.Atmosphere);
            _meta.SetEntityName(map, Loc.GetString(planet.MapName));

            if (comp.GridPath is { } gPath)
            {
                _mapLoader.TryLoadGrid(mapId, gPath, out var grid, opts);
                if (grid != null)
                {
                    // clears area of the grid thing whatever its stolen from DV planet slop
                    _setTiles.Clear();
                    var aabb = Comp<MapGridComponent>(grid.Value).LocalAABB;
                    _biome.ReserveTiles(map, aabb.Enlarged(0.2f), _setTiles);
                }
            }

            _map.InitializeMap(map);

            grids = new List<EntityUid> {map};
            Log.Info($"Created map {mapId} for {ToPrettyString(uid):rule}");
        }
        // Mono end
        else if (comp.MapPath is {} path)
        {
            DebugTools.AssertNull(comp.GridPath);
            DebugTools.AssertNull(comp.PreloadedGrid);

            var opts = DeserializationOptions.Default with {InitializeMaps = true};
            if (!_mapLoader.TryLoadMap(path, out var map, out var gridSet, opts))
            {
                Log.Error($"Failed to load map from {path}!");
                ForceEndSelf(uid, rule);
                return;
            }

            grids = gridSet.Select( x => x.Owner).ToList();
            mapId = map.Value.Comp.MapId;
        }
        else if (comp.GridPath is { } gPath)
        {
            DebugTools.AssertNull(comp.PreloadedGrid);

            // I fucking love it when "map paths" choses to ar
            _map.CreateMap(out mapId);
            var opts = DeserializationOptions.Default with {InitializeMaps = true};
            if (!_mapLoader.TryLoadGrid(mapId, gPath, out var grid, opts))
            {
                Log.Error($"Failed to load grid from {gPath}!");
                ForceEndSelf(uid, rule);
                return;
            }

            grids = new List<EntityUid> {grid.Value.Owner};
        }
        else if (comp.PreloadedGrid is {} preloaded)
        {
            // TODO: If there are no preloaded grids left, any rule announcements will still go off!
            if (!_gridPreloader.TryGetPreloadedGrid(preloaded, out var loadedShuttle))
            {
                Log.Error($"Failed to get a preloaded grid with {preloaded}!");
                ForceEndSelf(uid, rule);
                return;
            }

            var mapUid = _map.CreateMap(out mapId, runMapInit: false);
            _transform.SetParent(loadedShuttle.Value, mapUid);
            grids = new List<EntityUid>() { loadedShuttle.Value };
            _map.InitializeMap(mapUid);
        }
        else
        {
            Log.Error($"No valid map prototype or map path associated with the rule {ToPrettyString(uid)}");
            ForceEndSelf(uid, rule);
            return;
        }

        var ev = new RuleLoadedGridsEvent(mapId, grids);
        RaiseLocalEvent(uid, ref ev);

        base.Added(uid, comp, rule, args);
    }
}
