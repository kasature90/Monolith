using Content.Shared._Mono.Sector;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Sector.UI;

public sealed class StrategicStockpileBoundUserInterface : BoundUserInterface
{
    private StrategicStockpileWindow? _window;

    public StrategicStockpileBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<StrategicStockpileWindow>();
        _window.OnBuy += (res, amount) =>
            SendMessage(new StrategicStockpileBuyMessage { Resource = res, Amount = amount });
        _window.OnWithdraw += amount =>
            SendMessage(new StrategicStockpileWithdrawMessage { Amount = amount });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is StrategicStockpileBoundUserInterfaceState s)
            _window?.UpdateState(s);
    }
}
