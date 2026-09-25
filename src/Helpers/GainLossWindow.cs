namespace OoplesFinance.StockIndicators.Helpers;

internal static class GainLossShare
{
    internal static StrengthValue Change(double current, double previous)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(current); sum.Add(previous, -1);
        return StrengthValue.Round(sum, 1);
    }
    internal static double Of(ExactMeanAccumulator numerator, ExactMeanAccumulator total, double empty) =>
        total.IsExactlyZero ? empty : numerator.Ratio(total);
}

internal sealed class IntradayGainLossWindow : IDisposable
{
    private readonly PooledRingBuffer<StrengthValue> _changes;
    private ExactMeanAccumulator _numerator, _total;
    internal IntradayGainLossWindow(int length, int capacityHint = int.MaxValue) =>
        _changes = new(Math.Min(Math.Max(1, length), Math.Max(1, capacityHint)));
    internal double Next(double close, double open, bool final)
    {
        var change = GainLossShare.Change(close, open);
        var numerator = _numerator; var total = _total;
        if (_changes.Count == _changes.Capacity)
        {
            var old = _changes[0];
            if (old.Mantissa > 0) old.AddTo(ref numerator, -100);
            old.Absolute.AddTo(ref total, -1);
        }
        if (change.Mantissa > 0) change.AddTo(ref numerator, 100);
        change.Absolute.AddTo(ref total);
        var value = GainLossShare.Of(numerator, total, 0);
        if (final) { _numerator = numerator; _total = total; _changes.TryAdd(change, out _); }
        return value;
    }
    internal void Reset() { _numerator = _total = default; _changes.Clear(); }
    public void Dispose() => _changes.Dispose();
}

internal sealed class RelativeMomentumWindow : IDisposable
{
    private readonly int _lag;
    private readonly PooledRingBuffer<double> _prices;
    private readonly StrengthAverage _gains, _losses, _signal;
    private long _count;
    internal RelativeMomentumWindow(MovingAvgType kind, int length, int lag, int capacityHint = int.MaxValue)
    {
        _lag = Math.Max(1, lag);
        _prices = new(Math.Min(_lag, Math.Max(1, capacityHint)));
        _gains = new(kind, length, capacityHint); _losses = new(kind, length, capacityHint); _signal = new(kind, length, capacityHint);
    }
    internal (double Value, double Signal, double Histogram) Next(double price, bool final)
    {
        var change = _count < _lag ? default : GainLossShare.Change(price, _prices[0]);
        var gain = _gains.Next(change.Mantissa > 0 ? change : default, final);
        var loss = _losses.Next(change.Mantissa < 0 ? change.Absolute : default, final);
        var numerator = new ExactMeanAccumulator(); gain.AddTo(ref numerator, 100);
        var total = new ExactMeanAccumulator(); gain.AddTo(ref total); loss.AddTo(ref total);
        var value = GainLossShare.Of(numerator, total, 100);
        var signal = _signal.Next(new StrengthValue(value), final).Mantissa;
        if (final) { _prices.TryAdd(price, out _); _count++; }
        return (value, signal, value - signal);
    }
    internal void Reset() { _prices.Clear(); _count = 0; _gains.Reset(); _losses.Reset(); _signal.Reset(); }
    public void Dispose() { _prices.Dispose(); _gains.Dispose(); _losses.Dispose(); _signal.Dispose(); }
}
