using System.Numerics;
using Content.Shared._Mono.Company;
using Content.Shared._Mono.Sector;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.Sector.UI;

public sealed class StrategicMapControl : Control
{
    private const float BaseCellPx = 28f;
    private const float MinZoom = 0.35f;
    private const float MaxZoom = 3.5f;
    private const float ZoomStep = 1.15f;

    [Dependency] private IPrototypeManager _protos = default!;

    private SectorMapStateMessage? _state;
    private NetEntity? _selected;

    private float _zoom = 1f;
    private Vector2 _pan;
    private bool _panning;
    private Vector2 _panStartMouse;
    private Vector2 _panStartOffset;

    private readonly Dictionary<NetEntity, Vector2> _markerScreen = new();

    private float CellPx => BaseCellPx * _zoom;

    public event Action<NetEntity>? OnOutpostClicked;

    public StrategicMapControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        MinHeight = 400;
        RectClipContent = true;
    }

    public void SetState(SectorMapStateMessage? state, NetEntity? selected)
    {
        var hadNothing = _state == null || _state.Outposts.Count == 0;
        _state = state;
        _selected = selected;

        if (hadNothing && state != null && state.Outposts.Count > 0)
            CenterOnContent();
    }

    private void CenterOnContent()
    {
        if (_state == null || _state.Outposts.Count == 0)
            return;

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);

        foreach (var o in _state.Outposts)
        {
            var c = SectorGrid.FromWorld(o.WorldPos);
            var r = o.SectorRadius;
            min = Vector2.Min(min, new Vector2(c.X - r, c.Y - r));
            max = Vector2.Max(max, new Vector2(c.X + r + 1, c.Y + r + 1));
        }

        var centerCells = (min + max) * 0.5f;
        var size = PixelSize;
        _pan = new Vector2(size.X * 0.5f, size.Y * 0.5f) - new Vector2(centerCells.X, centerCells.Y) * CellPx;
    }

    private Color ColorForOutpost(SectorMapOutpostDto o)
    {
        if (o.Owner != null && _protos.TryIndex(o.Owner, out CompanyPrototype? company))
            return company.Color;

        return o.Control switch
        {
            SectorControl.Contested => Color.FromHex("#c9a227"),
            SectorControl.Controlled => Color.FromHex("#3d9e4a"),
            _ => Color.FromHex("#6a6a6a"),
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var cellPx = CellPx;
        _markerScreen.Clear();

        var minCellX = (int)MathF.Floor(-_pan.X / cellPx) - 2;
        var minCellY = (int)MathF.Floor(-_pan.Y / cellPx) - 2;
        var maxCellX = (int)MathF.Ceiling((size.X - _pan.X) / cellPx) + 2;
        var maxCellY = (int)MathF.Ceiling((size.Y - _pan.Y) / cellPx) + 2;

        var sumLin = new Dictionary<(int, int), Vector3>();
        var count = new Dictionary<(int, int), int>();

        if (_state != null)
        {
            foreach (var o in _state.Outposts)
            {
                var center = SectorGrid.FromWorld(o.WorldPos);
                var r = o.SectorRadius;
                var c = ColorForOutpost(o);

                for (var x = center.X - r; x <= center.X + r; x++)
                {
                    for (var y = center.Y - r; y <= center.Y + r; y++)
                    {
                        if (Math.Max(Math.Abs(x - center.X), Math.Abs(y - center.Y)) > r)
                            continue;

                        var lin = new Vector3(c.R * c.R, c.G * c.G, c.B * c.B);
                        var key = (x, y);
                        sumLin[key] = sumLin.GetValueOrDefault(key) + lin;
                        count[key] = count.GetValueOrDefault(key) + 1;
                    }
                }
            }
        }

        foreach (var (key, n) in count)
        {
            var (x, y) = key;
            if (x < minCellX || x > maxCellX || y < minCellY || y > maxCellY)
                continue;

            var mean = sumLin[key] / n;
            var color = new Color(MathF.Sqrt(mean.X), MathF.Sqrt(mean.Y), MathF.Sqrt(mean.Z), 0.30f);
            var origin = CellOriginScreen(x, y, size, cellPx);
            handle.DrawRect(new UIBox2(origin, origin + new Vector2(cellPx, cellPx)), color);
        }

        if (_state != null && _selected != null)
        {
            foreach (var o in _state.Outposts)
            {
                if (o.Grid != _selected)
                    continue;

                var center = SectorGrid.FromWorld(o.WorldPos);
                var r = o.SectorRadius;
                var fill = Color.FromHex("#5eb1ff").WithAlpha(0.12f);
                var edge = Color.FromHex("#5eb1ff").WithAlpha(0.9f);

                for (var x = center.X - r; x <= center.X + r; x++)
                {
                    for (var y = center.Y - r; y <= center.Y + r; y++)
                    {
                        if (Math.Max(Math.Abs(x - center.X), Math.Abs(y - center.Y)) > r)
                            continue;

                        var origin = CellOriginScreen(x, y, size, cellPx);
                        var box = new UIBox2(origin, origin + new Vector2(cellPx, cellPx));
                        handle.DrawRect(box, fill);
                        handle.DrawRect(box, edge, filled: false);
                    }
                }
                break;
            }
        }

        var crossColor = Color.White.WithAlpha(0.4f);
        var arm = cellPx * 0.18f;

        for (var x = minCellX; x <= maxCellX + 1; x++)
        {
            for (var y = minCellY; y <= maxCellY + 1; y++)
            {
                var corner = CellCornerScreen(x, y, size, cellPx);
                handle.DrawLine(corner - new Vector2(arm, 0), corner + new Vector2(arm, 0), crossColor);
                handle.DrawLine(corner - new Vector2(0, arm), corner + new Vector2(0, arm), crossColor);
            }
        }

        if (_state == null)
            return;

        foreach (var o in _state.Outposts)
        {
            var screen = WorldToScreenFlipped(o.WorldPos, size, cellPx);
            _markerScreen[o.Grid] = screen;

            var color = _selected == o.Grid
                ? Color.FromHex("#5eb1ff")
                : ColorForOutpost(o);

            handle.DrawCircle(screen, Math.Max(4f, 6f * _zoom), color);
        }
    }

    private Vector2 CellCornerScreen(int cellX, int cellY, Vector2 size, float cellPx)
    {
        return new Vector2(
            cellX * cellPx + _pan.X,
            size.Y - (cellY * cellPx + _pan.Y));
    }

    private Vector2 CellOriginScreen(int cellX, int cellY, Vector2 size, float cellPx)
    {
        return new Vector2(
            cellX * cellPx + _pan.X,
            size.Y - ((cellY + 1) * cellPx + _pan.Y));
    }

    private Vector2 WorldToScreenFlipped(Vector2 world, Vector2 size, float cellPx)
    {
        var cellFrac = world / SectorGrid.CellSize;
        return new Vector2(
            cellFrac.X * cellPx + _pan.X,
            size.Y - (cellFrac.Y * cellPx + _pan.Y));
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Handled)
            return;

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            var local = args.RelativePosition;
            NetEntity? best = null;
            var bestDist = 12f * _zoom;

            foreach (var (grid, pos) in _markerScreen)
            {
                var d = (local - pos).Length();
                if (d < bestDist)
                {
                    bestDist = d;
                    best = grid;
                }
            }

            if (best != null)
            {
                OnOutpostClicked?.Invoke(best.Value);
                args.Handle();
                return;
            }

            _panning = true;
            _panStartMouse = local;
            _panStartOffset = _pan;
            args.Handle();
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
            _panning = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_panning)
            return;

        var delta = args.RelativePosition - _panStartMouse;
        _pan = _panStartOffset + new Vector2(delta.X, -delta.Y);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        var oldZoom = _zoom;
        if (args.Delta.Y > 0)
            _zoom = Math.Clamp(_zoom * ZoomStep, MinZoom, MaxZoom);
        else if (args.Delta.Y < 0)
            _zoom = Math.Clamp(_zoom / ZoomStep, MinZoom, MaxZoom);
        else
            return;

        if (MathHelper.CloseToPercent(oldZoom, _zoom))
            return;

        var mouse = args.RelativePosition;
        var size = PixelSize;

        var sectorBefore = new Vector2(
            (mouse.X - _pan.X) / (BaseCellPx * oldZoom),
            (size.Y - mouse.Y - _pan.Y) / (BaseCellPx * oldZoom));

        var newCellPx = BaseCellPx * _zoom;
        _pan = new Vector2(
            mouse.X - sectorBefore.X * newCellPx,
            size.Y - mouse.Y - sectorBefore.Y * newCellPx);

        args.Handle();
    }
}
