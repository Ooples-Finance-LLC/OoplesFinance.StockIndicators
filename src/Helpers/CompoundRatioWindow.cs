using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class CompoundRatioWindow : IDisposable
{
    private readonly double[] _weights;
    private readonly ExactMeanAccumulator _denominator;
    private readonly PooledRingBuffer<double> _prices;
    private readonly RocBankAverage? _average;
    private readonly IMovingAverageSmoother? _fallback;
    internal static int SmoothPeriod(int length) => Math.Max(1, (int)Math.Round(Math.Sqrt(Math.Max(1, length))));
    internal CompoundRatioWindow(MovingAvgType kind, int length, bool initializeFallback = true)
    {
        length = Math.Max(1, length); _weights = new double[length]; _prices = new(length);
        var ratio = length == 1 ? 1 : Math.Pow(length, 1d / (length - 1) - 1);
        var basis = 1 + 2 * ratio; var denominator = new ExactMeanAccumulator();
        for (var j = 0; j < length; j++) { _weights[j] = Math.Pow(basis, length - j); denominator.Add(_weights[j]); }
        _denominator = denominator;
        if (StrengthWindow.Supports(kind)) _average = new(kind, SmoothPeriod(length), int.MaxValue);
        else if (initializeFallback) _fallback = MovingAverageSmootherFactory.Create(kind, SmoothPeriod(length));
    }
    internal double Raw(double price, bool commit)
    {
        var top = new ExactMeanAccumulator();
        for (var j = 0; j < _weights.Length && j <= _prices.Count; j++)
            top.AddProduct(j == 0 ? price : _prices[_prices.Count - j], _weights[j]);
        var result = top.Ratio(_denominator);
        if (commit) _prices.TryAdd(price, out _);
        return result;
    }
    internal double Next(double price, bool commit)
    {
        var raw = Raw(price, commit);
        return _average is null ? _fallback!.Next(raw, commit) : _average.Next(new RocBankValue(raw), commit).Publish();
    }
    internal void Reset() { _prices.Clear(); _average?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _prices.Dispose(); _average?.Dispose(); _fallback?.Dispose(); }
}
