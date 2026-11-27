namespace Content.Shared._Mono.Sector;

public enum StrategicResource : byte
{
    Fuel = 0,
    Munitions = 1,
    Parts = 2,
}

public enum SectorControl : byte
{
    Unclaimed = 0,
    Contested = 1,
    Controlled = 2,
}
