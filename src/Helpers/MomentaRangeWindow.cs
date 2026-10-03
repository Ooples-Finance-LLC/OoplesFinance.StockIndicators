using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class MomentaRangeWindow : IDisposable
{
    private readonly RollingWindowMax _maximum;
    private readonly RollingWindowMin _minimum;
    private readonly StrengthWindow _ratio;
    private readonly StrengthAverage _signal;

    internal MomentaRangeWindow(MovingAvgType kind, int lookback, int first, int second, int capacityHint = int.MaxValue)
    {
        var capacity = Math.Min(Math.Max(2, lookback), Math.Max(1, capacityHint));
        _maximum = new RollingWindowMax(capacity);
        _minimum = new RollingWindowMin(capacity);
        _ratio = new StrengthWindow(kind, new[] { first, second }, capacityHint);
        _signal = new StrengthAverage(kind, second, capacityHint);
    }

    internal (double Value, double Signal) Next(double price, bool final)
    {
        var high = final ? _maximum.Add(price, out _) : _maximum.Preview(price, out _);
        var low = final ? _minimum.Add(price, out _) : _minimum.Preview(price, out _);
        var top = new ExactMeanAccumulator(); top.Add(price); top.Add(low, -1);
        var bottom = new ExactMeanAccumulator(); bottom.Add(high); bottom.Add(low, -1);
        var value = Math.Max(0, _ratio.NextPair(StrengthValue.Round(top, 1), StrengthValue.Round(bottom, 1), final));
        return (value, _signal.Next(new StrengthValue(value), final).Mantissa);
    }

    internal void Reset() { _maximum.Reset(); _minimum.Reset(); _ratio.Reset(); _signal.Reset(); }
    public void Dispose() { _maximum.Dispose(); _minimum.Dispose(); _ratio.Dispose(); _signal.Dispose(); }
}
