using OoplesFinance.StockIndicators.Helpers;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Streaming;

[PrimaryOutput("Ci")]
public sealed class ConfluenceIndicatorState : IStreamingIndicatorState, IDisposable, ICustomInputConsumer
{
    private readonly ConfluenceWindow? _window;
    private readonly LegacyConfluenceIndicatorState? _legacy;
    private bool _selected;
    public ConfluenceIndicatorState(MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 10)
    {
        if (ConfluenceWindow.Supports(maType)) _window = new(maType, length);
        else _legacy = new(maType, length);
    }
    public IndicatorName Name => IndicatorName.ConfluenceIndicator;
    void ICustomInputConsumer.ReadCloseAsInput()
    { _selected = true; if (_legacy is ICustomInputConsumer consumer) consumer.ReadCloseAsInput(); }
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        if (_legacy is not null) return _legacy.Update(bar, isFinal, includeOutputs);
        StreamingInputValidation.Validate(bar);
        var close = F.Of(bar.Close);
        var full = _selected ? close : (F.Of(bar.Open) + F.Of(bar.High) + F.Of(bar.Low) + close) / 4;
        var value = _window!.Next(close, full, isFinal).Value;
        return new(value, includeOutputs ? new Dictionary<string, double> { { "Ci", value } } : null);
    }
    public void Reset() { _window?.Reset(); _legacy?.Reset(); }
    public void Dispose() => _legacy?.Dispose();
}
