using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class RangeGainLossWindow : IDisposable
{
    private readonly RollingWindowMax _maximum;
    private readonly RollingWindowMin _minimum;
    private readonly StrengthAverage[] _gains, _losses;
    private readonly StrengthAverage _signal;
    internal RangeGainLossWindow(MovingAvgType kind, int lookback, int[] periods, int signal, int capacityHint = int.MaxValue)
    {
        var capacity = Math.Min(Math.Max(1, lookback), Math.Max(1, capacityHint));
        _maximum = new(capacity); _minimum = new(capacity);
        _gains = periods.Select(p => new StrengthAverage(kind, p, capacityHint)).ToArray();
        _losses = periods.Select(p => new StrengthAverage(kind, p, capacityHint)).ToArray();
        _signal = new(kind, signal, capacityHint);
    }
    internal (double Value, double Signal) Next(double price, bool final)
    {
        var high = final ? _maximum.Add(price, out _) : _maximum.Preview(price, out _);
        var low = final ? _minimum.Add(price, out _) : _minimum.Preview(price, out _);
        var up = GainLossShare.Change(price, low);
        var down = GainLossShare.Change(high, price);
        for (var stage = 0; stage < _gains.Length; stage++)
        {
            up = _gains[stage].Next(up, final); down = _losses[stage].Next(down, final);
        }
        var numerator = new ExactMeanAccumulator(); up.AddTo(ref numerator, 100);
        var total = new ExactMeanAccumulator(); up.AddTo(ref total); down.AddTo(ref total);
        var value = GainLossShare.Of(numerator, total, 100);
        return (value, _signal.Next(new StrengthValue(value), final).Mantissa);
    }
    internal void Reset() { _maximum.Reset(); _minimum.Reset(); foreach (var stage in _gains.Concat(_losses)) stage.Reset(); _signal.Reset(); }
    public void Dispose() { _maximum.Dispose(); _minimum.Dispose(); foreach (var stage in _gains.Concat(_losses)) stage.Dispose(); _signal.Dispose(); }
}
