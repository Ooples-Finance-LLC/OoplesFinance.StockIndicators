using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Normalize before rounding deviation: a positive subnormal spread must not become a flat window.
internal sealed class StandardizedScoreWindow : IDisposable
{
    private readonly PooledRingBuffer<double>? _window;
    private readonly Queue<double>? _observed;
    // The mathematical period is independent of how many samples are stored.
    private readonly int _length;
    private int Count => _observed?.Count ?? _window!.Count;
    private readonly ExactLinearFitWindow? _long, _short;
    private BigInteger _sum, _squares;
    internal StandardizedScoreWindow(int length, bool fast, bool observedHistory = false)
    {
        length = Math.Max(1, length); _length = length;
        if (observedHistory) _observed = new(); else _window = new PooledRingBuffer<double>(length);
        if (fast) { _long = new ExactLinearFitWindow(length, observedHistory); _short = new ExactLinearFitWindow(MathHelper.MinOrMax((int)Math.Ceiling(length / 2d)), observedHistory); }
    }
    internal double Next(double price, double average, bool exactSimpleMean, bool commit)
    {
        var value = ExactVarianceWindow.Units(_long is null ? price : average);
        var n = new BigInteger(_length);
        var expired = Count == _length ? ExactVarianceWindow.Units(_observed is null ? _window![0] : _observed.Peek()) : BigInteger.Zero;
        var sum = _sum + value - expired; var squares = _squares + value * value - expired * expired;
        var variance = n * squares - sum * sum;
        var numerator = exactSimpleMean ? n * value - sum : n * (value - ExactVarianceWindow.Units(average));
        var denominator = BigInteger.One;
        if (_long is not null)
        {
            var full = _long.Next(average, commit); var half = _short!.Next(average, commit);
            var gap = half.Difference(full); numerator = n * gap.Numerator; denominator = 2 * gap.Denominator;
        }
        var result = Count < _length - 1 || variance.IsZero ? 0
            : numerator.Sign * ExactPopulationDeviation.RootRatio((numerator * numerator) << 2148, variance * denominator * denominator);
        if (commit)
        {
            var sample = _long is null ? price : average;
            _window?.TryAdd(sample, out _);
            if (_observed is not null) { if (Count == _length) _observed.Dequeue(); _observed.Enqueue(sample); }
            _sum = sum; _squares = squares;
        }
        return result;
    }
    internal void Reset() { _window?.Clear(); _observed?.Clear(); _sum = _squares = default; _long?.Reset(); _short?.Reset(); }
    public void Dispose() { _window?.Dispose(); _observed?.Clear(); _long?.Dispose(); _short?.Dispose(); }
    internal static double Inverse(double score, bool fast)
    {
        if (fast) return Math.Tanh(5 * score);
        if (score >= 0) return 100 / (1 + Math.Exp(-2 * score));
        // Scale before exponentiation in the underflow tail, where the unscaled exp can vanish.
        if (score < -350) return Math.Exp(2 * score + Math.Log(100));
        var e = Math.Exp(2 * score); return 100 * e / (1 + e);
    }
}
