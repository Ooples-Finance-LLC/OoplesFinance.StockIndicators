namespace OoplesFinance.StockIndicators.Helpers;

// Log changes and square-root taps are rounded binary64 coefficients. Their
// products and totals, and the final convex price blend, are accumulated exactly.
internal sealed class NaturalWindowMean : IDisposable
{
    private readonly PooledRingBuffer<double> _changes;
    private double _previousLog, _previousPrice;

    internal NaturalWindowMean(int length) => _changes = new PooledRingBuffer<double>(Math.Max(1, length));

    internal double Next(double price, bool commit)
    {
        if (double.IsNaN(price) || double.IsInfinity(price))
            throw new ArgumentOutOfRangeException(nameof(price));
        var log = price > 0 ? Math.Log(price) * 1000 : 0;
        var change = Math.Abs(log - _previousLog);
        var numerator = new ExactMeanAccumulator();
        var denominator = new ExactMeanAccumulator();
        for (var lag = 0; lag < _changes.Capacity && lag <= _changes.Count; lag++)
        {
            var movement = lag == 0 ? change : _changes[_changes.Count - lag];
            var tap = Math.Sqrt(lag + 1d) - Math.Sqrt(lag);
            numerator.AddProduct(movement, tap);
            denominator.Add(movement);
        }
        var ratio = numerator.Ratio(denominator);
        var blend = new ExactMeanAccumulator();
        blend.Add(_previousPrice);
        blend.AddProduct(_previousPrice, ratio, -1);
        blend.AddProduct(price, ratio);
        var result = blend.Mean(1);
        if (commit)
        {
            _changes.TryAdd(change, out _);
            _previousLog = log;
            _previousPrice = price;
        }
        return result;
    }

    internal static void Compute(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        if (output.Length < input.Length) throw new ArgumentException("Output span must be at least input length.", nameof(output));
        if (input.IsEmpty) return;
        using var mean = new NaturalWindowMean(Math.Min(Math.Max(1, length), input.Length));
        for (var i = 0; i < input.Length; i++) output[i] = mean.Next(input[i], true);
    }

    internal void Reset()
    {
        _changes.Clear();
        _previousLog = _previousPrice = 0;
    }

    public void Dispose() => _changes.Dispose();
}
