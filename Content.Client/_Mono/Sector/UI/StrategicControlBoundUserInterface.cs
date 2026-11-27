using Content.Shared._Mono.Sector;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Sector.UI;

public sealed class StrategicControlBoundUserInterface : BoundUserInterface
{
    private StrategicControlWindow? _window;

    public StrategicControlBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<StrategicControlWindow>();
        _window.OnClaim += () => SendMessage(new StrategicControlClaimMessage());
        _window.OnRelinquish += () => SendMessage(new StrategicControlRelinquishMessage());
        _window.OnApplyTrade += (buy, sell, prices) =>
            SendMessage(new StrategicControlSetTradeMessage
            {
                BuyEnabled = buy,
                SellEnabled = sell,
                ResourcePrices = prices,
            });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is StrategicControlBoundUserInterfaceState s)
            _window?.UpdateState(s);
    }
}
