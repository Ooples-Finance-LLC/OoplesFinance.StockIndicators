namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class SmoothedDeltaWindow : IDisposable
{
    private readonly int _length;
    private readonly StrengthAverage _mean, _travel;
    private readonly PooledRingBuffer<double> _prices, _means;
    private long _count;

    internal SmoothedDeltaWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length);
        _mean = new StrengthAverage(kind, _length, capacityHint);
        _travel = new StrengthAverage(kind, _length, capacityHint);
        var capacity = Math.Min(_length, Math.Max(1, capacityHint));
        _prices = new PooledRingBuffer<double>(capacity);
        _means = new PooledRingBuffer<double>(capacity);
    }

    internal double Next(double price, bool final)
    {
        var mean = _mean.Next(new StrengthValue(price), final).Mantissa;
        var change = new ExactMeanAccumulator();
        var movement = new ExactMeanAccumulator();
        if (_count >= _length)
        {
            change.Add(price); change.Add(_prices[0], -1);
            movement.Add(mean); movement.Add(_means[0], -1);
        }
        var travel = _travel.Next(StrengthValue.Round(change, 1).Absolute, final);
        var numerator = new ExactMeanAccumulator(); StrengthValue.Round(movement, 1).AddTo(ref numerator);
        var denominator = new ExactMeanAccumulator(); travel.AddTo(ref denominator);
        var result = Math.Max(0, Math.Min(1, numerator.Ratio(denominator)));
        if (final) { _prices.TryAdd(price, out _); _means.TryAdd(mean, out _); _count++; }
        return result;
    }

    internal void Reset() { _mean.Reset(); _travel.Reset(); _prices.Clear(); _means.Clear(); _count = 0; }
    public void Dispose() { _mean.Dispose(); _travel.Dispose(); _prices.Dispose(); _means.Dispose(); }
}
