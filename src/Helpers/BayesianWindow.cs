using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class BayesianWindow : IDisposable
{
    private readonly int _length; private readonly double _multiplier, _threshold; private readonly Average? _mean;
    private readonly Queue<(BigInteger Price, int Upper, int Basis)> _history = new();
    private BigInteger _sum, _squares; private long _upperUp, _upperDown, _basisUp, _basisDown;
    private double _previousDown, _previousUp, _previousPrime;
    internal BayesianWindow(MovingAvgType kind, int length, double multiplier = 2.5, double threshold = 15, bool external = false)
    {
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier));
        if (double.IsNaN(threshold) || double.IsInfinity(threshold)) throw new ArgumentOutOfRangeException(nameof(threshold));
        _length = Math.Max(1, length); _multiplier = multiplier; _threshold = threshold / 100; if (!external) _mean = new(kind, _length);
    }
    private static double Evidence(long first, long otherFirst, long second, long otherSecond)
    {
        var firstTotal = Math.Max(1, first + otherFirst); var secondTotal = Math.Max(1, second + otherSecond);
        var joint = (BigInteger)first * second; var opposite = (BigInteger)(firstTotal - first) * (secondTotal - second);
        return joint + opposite == 0 ? 0 : ExactMeanAccumulator.UnitRatio(joint << 1074, joint + opposite);
    }
    private static double Combine(double first, double second)
    {
        var unit = BigInteger.One << 1074; var a = ExactVarianceWindow.Units(first); var b = ExactVarianceWindow.Units(second);
        var joint = a * b; var opposite = (unit - a) * (unit - b);
        return joint + opposite == 0 ? 0 : ExactMeanAccumulator.UnitRatio(joint << 1074, joint + opposite);
    }
    internal (double Down, double Up, double Prime, Signal Signal) Next(double price, bool commit, double? externalMean = null)
    {
        var current = ExactVarianceWindow.Units(price); var full = _history.Count == _length;
        var old = full ? _history.Peek() : (Price: BigInteger.Zero, Upper: 0, Basis: 0);
        var sum = _sum + current - old.Price; var squares = _squares + current * current - old.Price * old.Price;
        var deviation = _history.Count < _length - 1 ? 0 : ExactPopulationDeviation.RootRatio(_length * squares - sum * sum, (BigInteger)_length * _length);
        var mean = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _mean!.Next(new(price), commit);
        var middle = ExactVarianceWindow.Units(mean.Mantissa) << mean.UpperShift;
        var band = new ExactMeanAccumulator(); mean.AddTo(ref band); band.AddProduct(deviation, _multiplier);
        var upperValue = RocBankValue.Round(band); var upperUnits = ExactVarianceWindow.Units(upperValue.Mantissa) << upperValue.UpperShift;
        var upper = current.CompareTo(upperUnits); var basis = current.CompareTo(middle);
        var upperUp = _upperUp + (upper > 0 ? 1 : 0) - (old.Upper > 0 ? 1 : 0); var upperDown = _upperDown + (upper < 0 ? 1 : 0) - (old.Upper < 0 ? 1 : 0);
        var basisUp = _basisUp + (basis > 0 ? 1 : 0) - (old.Basis > 0 ? 1 : 0); var basisDown = _basisDown + (basis < 0 ? 1 : 0) - (old.Basis < 0 ? 1 : 0);
        var down = Evidence(upperUp, upperDown, basisUp, basisDown); var up = Evidence(upperDown, upperUp, basisDown, basisUp); var prime = Combine(down, up);
        var buy = prime > _threshold && _previousPrime == 0 || up < 1 && _previousUp == 1;
        var sell = prime == 0 && _previousPrime > _threshold || down < 1 && _previousDown == 1;
        var signal = buy ? Signal.Buy : sell ? Signal.Sell : Signal.None;
        if (commit)
        {
            if (full) _history.Dequeue(); _history.Enqueue((current, upper, basis)); _sum = sum; _squares = squares;
            _upperUp = upperUp; _upperDown = upperDown; _basisUp = basisUp; _basisDown = basisDown;
            _previousDown = down; _previousUp = up; _previousPrime = prime;
        }
        return (down, up, prime, signal);
    }
    internal static double[] Components(StockData data, List<double> input, MovingAvgType kind, int length)
    {
        var caller = data.CaptureInputSeries(); length = Math.Max(1, length);
        var result = ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), length)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, length, input).ToArray();
        data.RestoreInputSeries(caller); return result;
    }
    internal void Reset() { _history.Clear(); _sum = _squares = default; _upperUp = _upperDown = _basisUp = _basisDown = 0; _previousDown = _previousUp = _previousPrime = 0; _mean?.Reset(); }
    public void Dispose() => _mean?.Dispose();
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool commit)
        {
            if (_recursive is not null) return _recursive.Next(value, commit);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), commit));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count + 1L < _length ? default : RocBankValue.Round(sum, count: _length);
            if (commit) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _sum = _weighted = default; _history.Clear(); _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
