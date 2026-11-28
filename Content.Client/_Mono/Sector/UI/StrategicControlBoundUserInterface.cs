using Content.Client.UserInterface;
using Content.Shared._Mono.Sector;
using Robust.Client.UserInterface;

namespace Content.Client._Mono.Sector.UI;

public sealed class StrategicControlBoundUserInterface : BoundUserInterface, IBuiPreTickUpdate
{
    private StrategicControlWindow? _window;
    private InputCoalescer<float> _fp;
    private InputCoalescer<float> _ap;
    private InputCoalescer<float> _pp;
    private bool _tradeDirty;

    private bool _buy;
    private bool _sell;
    private readonly Dictionary<StrategicResource, int> _prices = new()
    {
        { StrategicResource.Fuel, 0 },
        { StrategicResource.Munitions, 0 },
        { StrategicResource.Parts, 0 },
    };

    public StrategicControlBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<StrategicControlWindow>();
        _window.OnClaim += () => SendMessage(new StrategicControlClaimMessage());
        _window.OnRelinquish += () => SendMessage(new StrategicControlRelinquishMessage());

        _window.FuelPriceChanged += val =>
        {
            _fp.Set(val);
            _tradeDirty = true;
        };
        _window.AmmoPriceChanged += val =>
        {
            _ap.Set(val);
            _tradeDirty = true;
        };
        _window.PartPriceChanged += val =>
        {
            _pp.Set(val);
            _tradeDirty = true;
        };

        _window.TradeTogglesChanged += (buy, sell) =>
        {
            _buy = buy;
            _sell = sell;
            _tradeDirty = true;
        };
    }

    void IBuiPreTickUpdate.PreTickUpdate()
    {
        if (_fp.CheckIsModified(out var fp))
        {
            _prices[StrategicResource.Fuel] = (int)fp;
            _tradeDirty = true;
        }

        if (_ap.CheckIsModified(out var ap))
        {
            _prices[StrategicResource.Munitions] = (int)ap;
            _tradeDirty = true;
        }

        if (_pp.CheckIsModified(out var pp))
        {
            _prices[StrategicResource.Parts] = (int)pp;
            _tradeDirty = true;
        }

        if (!_tradeDirty)
            return;

        _tradeDirty = false;
        SendMessage(new StrategicControlSetTradeMessage
        {
            BuyEnabled = _buy,
            SellEnabled = _sell,
            ResourcePrices = new Dictionary<StrategicResource, int>(_prices),
        });
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not StrategicControlBoundUserInterfaceState s)
            return;

        if (_window is not null)
        {
            if (_prices[StrategicResource.Fuel] == 0
                && _prices[StrategicResource.Munitions] == 0
                && _prices[StrategicResource.Parts] == 0
                && s.ResourcePrices.Count > 0)
            {
                foreach (var (res, price) in s.ResourcePrices)
                    _prices[res] = price;

                _buy = s.BuyEnabled;
                _sell = s.SellEnabled;
            }
        }

        _window?.UpdateState(s);
    }
}
