using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AtrFilterWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _length, _deviationLength, _lookback;
    private readonly double _cap;
    private readonly bool _population;
    private readonly Average? _rangeAverage, _squareAverage;
    private readonly Queue<BigInteger> _ranges = new(), _deviations = new();
    private BigInteger _sum, _squares, _previousPrice, _previousAverage, _difference;
    private bool _seeded;
    internal AtrFilterWindow(MovingAvgType kind, int length, int atrLength, int deviationLength, int lookback, double cap, bool external = false)
    {
        if (double.IsNaN(cap) || double.IsInfinity(cap)) throw new ArgumentOutOfRangeException(nameof(cap));
        _length = Math.Max(1, length); _deviationLength = Math.Max(1, deviationLength); _lookback = Math.Max(1, lookback); _cap = cap; _population = kind == MovingAvgType.SimpleMovingAverage;
        if (!external) { _rangeAverage = new(kind, atrLength); _squareAverage = new(kind, _deviationLength); }
    }
    internal static BigInteger RelativeRange(double price, double high, double low, double previous)
    {
        var p = ExactVarianceWindow.Units(price); var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var before = ExactVarianceWindow.Units(previous);
        var range = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - before), BigInteger.Abs(l - before)));
        return p.IsZero ? RocBankValue.RoundUnits(range, BigInteger.One) : RocBankValue.RoundUnits((range * p.Sign) << 1074, BigInteger.Abs(p));
    }
    internal static double Publish(BigInteger value) => ExactMeanAccumulator.UnitRatio(value, BigInteger.One);
    internal static BigInteger Square(BigInteger value) => RocBankValue.RoundUnits(value * value, Unit);
    private static BigInteger Root(BigInteger numerator, BigInteger denominator)
    {
        if (numerator.Sign <= 0) return BigInteger.Zero;
        for (var shift = 0; ; shift += 32)
        { var value = ExactPopulationDeviation.RootRatio(numerator, denominator << (2 * shift)); if (!double.IsInfinity(value)) return ExactVarianceWindow.Units(value) << shift; }
    }
    internal (double Value, Signal Signal) Next(double price, double high, double low, bool commit, double? externalRange = null, double? externalSquares = null, bool customSquares = false)
    {
        var p = ExactVarianceWindow.Units(price); var relative = RelativeRange(price, high, low, _seeded ? Publish(_previousPrice) : price);
        var range = externalRange.HasValue ? ExactVarianceWindow.Units(externalRange.Value) : _rangeAverage!.Next(relative, commit);
        var squared = Square(range); var meanSquare = externalSquares.HasValue ? ExactVarianceWindow.Units(externalSquares.Value) : _squareAverage!.Next(squared, commit);
        var sum = _sum + range; var squares = _squares + range * range;
        if (_ranges.Count == _deviationLength) { var expired = _ranges.Peek(); sum -= expired; squares -= expired * expired; }
        var n = new BigInteger(_deviationLength);
        var deviation = _population && !customSquares ? _ranges.Count < _deviationLength - 1 ? BigInteger.Zero : Root(n * squares - sum * sum, n * n)
            : Root(meanSquare * Unit * n * n - sum * sum, n * n);
        var lowest = deviation; var skip = _deviations.Count == _lookback;
        foreach (var retained in _deviations) { if (skip) { skip = false; continue; } lowest = BigInteger.Min(lowest, retained); }
        var factor = deviation.IsZero ? 1 : ExactMeanAccumulator.UnitRatio(lowest << 1074, deviation);
        var gain = ExactMeanAccumulator.UnitRatio(2 * ExactVarianceWindow.Units(Math.Min(factor, _cap)), _length + 1L);
        var previous = _seeded ? _previousAverage : p; var estimate = RocBankValue.RoundUnits(previous * Unit + ExactVarianceWindow.Units(gain) * (p - previous), Unit);
        var difference = p - estimate; var signal = difference.Sign > 0 && difference > _difference ? Signal.StrongBuy : difference.Sign < 0 && difference < _difference ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { if (_ranges.Count == _deviationLength) _ranges.Dequeue(); _ranges.Enqueue(range); if (_deviations.Count == _lookback) _deviations.Dequeue(); _deviations.Enqueue(deviation); _sum = sum; _squares = squares; _previousPrice = p; _previousAverage = estimate; _difference = difference; _seeded = true; }
        return (Publish(estimate), signal);
    }
    internal void Reset() { _ranges.Clear(); _deviations.Clear(); _rangeAverage?.Reset(); _squareAverage?.Reset(); _sum = _squares = _previousPrice = _previousAverage = _difference = default; _seeded = false; }
    public void Dispose() { _rangeAverage?.Dispose(); _squareAverage?.Dispose(); _ranges.Clear(); _deviations.Clear(); }
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind; private readonly Queue<BigInteger> _history = new(); private readonly IMovingAverageSmoother? _fallback;
        private BigInteger _sum, _weighted, _previous; private long _count;
        internal Average(MovingAvgType kind, int length) { _length = Math.Max(1, length); _kind = kind; if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length); }
        internal BigInteger Next(BigInteger value, bool commit)
        {
            if (_fallback is not null) return ExactVarianceWindow.Units(_fallback.Next(Publish(value), commit));
            var sum = _sum; var weighted = _weighted; BigInteger result;
            if (_kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
            {
                weighted = weighted - sum + _length * value; if (_history.Count == _length) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.RoundUnits(weighted, (BigInteger)_length * (_length + 1L) / 2) : _history.Count < _length - 1 ? BigInteger.Zero : RocBankValue.RoundUnits(sum, _length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length) { sum += value; result = RocBankValue.RoundUnits(sum, _count + 1); }
            else { var ema = _kind == MovingAvgType.ExponentialMovingAverage; result = RocBankValue.RoundUnits((_length - 1L) * _previous + (ema ? 2 : 1) * value, ema ? _length + 1L : _length); }
            if (commit) { _sum = sum; _weighted = weighted; _previous = result; _count++; if (_kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() { _history.Clear(); _fallback?.Dispose(); }
    }
}
