using System.Numerics;

namespace Content.Shared._Mono.Sector;

public static class SectorGrid
{
    public const float CellSize = 2048f;

    public static (int X, int Y) FromWorld(Vector2 world)
    {
        var x = (int)MathF.Floor(world.X / CellSize);
        var y = (int)MathF.Floor(world.Y / CellSize);
        return (x, y);
    }

    public static long ToKey(int x, int y) => ((long)x << 32) ^ (uint)y;

    public static long ToKey(Vector2 world)
    {
        var (x, y) = FromWorld(world);
        return ToKey(x, y);
    }

    public static Vector2 CellCenter(int x, int y) =>
        new((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);

    public static int Chebyshev(int ax, int ay, int bx, int by) =>
        Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
}
