using Content.Shared._Mono.Sector;

namespace Content.Client._Mono.Sector;

public sealed partial class SectorMapSystem : EntitySystem
{
    public event Action<SectorMapStateMessage>? OnState;

    public SectorMapStateMessage? LastState { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SectorMapStateMessage>(OnMapState);
    }

    private void OnMapState(SectorMapStateMessage msg)
    {
        LastState = msg;
        OnState?.Invoke(msg);
    }

    public void Request()
    {
        RaiseNetworkEvent(new RequestSectorMapMessage());
    }
}
