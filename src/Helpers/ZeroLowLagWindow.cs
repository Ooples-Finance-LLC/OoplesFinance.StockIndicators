namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ZeroLowLagWindow : IDisposable
{
    private readonly int _length, _lookback;
    private readonly double _lag;
    private readonly PooledRingBuffer<RocBankValue> _accumulators, _outputs;
    private RocBankValue _previous;
    internal ZeroLowLagWindow(int length, double lag)
    {
        if (double.IsNaN(lag) || double.IsInfinity(lag)) throw new ArgumentOutOfRangeException(nameof(lag));
        _length = Math.Max(1, length); _lookback = Math.Max(1, Math.Min(530, (int)Math.Ceiling(_length / 2d))); _lag = lag;
        _accumulators = new(_length); _outputs = new(_lookback);
    }
    private static RocBankValue Add(RocBankValue first, RocBankValue second, int sign = 1)
    {
        var sum = new ExactMeanAccumulator(); first.AddTo(ref sum); second.AddTo(ref sum, sign); return RocBankValue.Round(sum);
    }
    internal double Next(double price, bool commit)
    {
        var priorOutput = _outputs.Count == _lookback ? _outputs[0] : new RocBankValue(price);
        var priorAccumulator = _accumulators.Count == _length ? _accumulators[0] : default;
        var increment = Add(new RocBankValue(price).Multiply(_lag), priorOutput.Multiply(1 - _lag));
        var accumulator = Add(increment, _previous);
        var difference = Add(accumulator, priorAccumulator, -1);
        var sum = new ExactMeanAccumulator(); difference.AddTo(ref sum); var result = RocBankValue.Round(sum, count: _length);
        if (commit) { _previous = accumulator; _accumulators.TryAdd(accumulator, out _); _outputs.TryAdd(result, out _); }
        return result.Publish();
    }
    internal void Reset() { _previous = default; _accumulators.Clear(); _outputs.Clear(); }
    public void Dispose() { _accumulators.Dispose(); _outputs.Dispose(); }
}
