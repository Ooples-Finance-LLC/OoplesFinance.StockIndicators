namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class InternalBarStrengthWindow : IDisposable
{
    private readonly int _length, _smooth;
    private readonly Queue<RocBankValue> _values = new();
    private ExactMeanAccumulator _sum;
    private RocBankValue _signal;
    internal InternalBarStrengthWindow(int length, int smooth = 3) { _length = Math.Max(1, length); _smooth = Math.Max(1, smooth); }
    internal static RocBankValue Position(double high, double low, double close)
    {
        var numerator = new ExactMeanAccumulator(); numerator.Add(close, 100); numerator.Add(low, -100);
        var denominator = new ExactMeanAccumulator(); denominator.Add(high); denominator.Add(low, -1);
        if (denominator.IsExactlyZero) return default;
        for (var shift = 0; ; shift += 1024)
        {
            var scaled = denominator; scaled.ScaleByPowerOfTwo(shift);
            var value = numerator.Ratio(scaled);
            if (!double.IsInfinity(value)) return new(value, shift);
        }
    }
    internal (double Value, double Signal) Next(double high, double low, double close, bool commit)
    {
        var position = Position(high, low, close); var sum = _sum;
        if (_values.Count == _length) _values.Peek().AddTo(ref sum, -1);
        position.AddTo(ref sum);
        var value = RocBankValue.Round(sum, count: Math.Min((long)_values.Count + 1, _length));
        var blend = new ExactMeanAccumulator(); _signal.AddTo(ref blend, _smooth - 1L); value.AddTo(ref blend, 2);
        var signal = RocBankValue.Round(blend, count: _smooth + 1L);
        if (commit)
        {
            _sum = sum; _signal = signal;
            if (_values.Count == _length) _values.Dequeue(); _values.Enqueue(position);
        }
        return (value.Publish(), signal.Publish());
    }
    internal void Reset() { _values.Clear(); _sum = default; _signal = default; }
    public void Dispose() => _values.Clear();
}
