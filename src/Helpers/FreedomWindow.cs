using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FreedomWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly bool _simple;
    private readonly Average _volumeMean;
    private readonly Moments _volumeScore, _ratioScore;
    private readonly Rank _movementRank, _volumeRank;
    private bool _started; private double _previousPrice, _demand; private BigInteger _previousMargin;
    internal FreedomWindow(MovingAvgType kind, int length)
    {
        length = Math.Max(1, length); _simple = kind == MovingAvgType.SimpleMovingAverage; _volumeMean = new(kind, length);
        _volumeScore = new(length); _ratioScore = new(length); _movementRank = new(length); _volumeRank = new(length);
    }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    internal RocBankValue RelativeVolume(double volume, bool final, double? externalMean = null)
    {
        var mean = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _volumeMean.Next(new(volume), final);
        return _volumeScore.Next(volume, mean, _simple && !externalMean.HasValue, final);
    }
    internal (double Score, double Demand, Signal Trade) Next(double price, double volume, bool final, double? externalMean = null)
    {
        if (double.IsNaN(price) || double.IsInfinity(price) || double.IsNaN(volume) || double.IsInfinity(volume)) throw new ArgumentOutOfRangeException(nameof(price));
        var relative = RelativeVolume(volume, final, externalMean); var move = default(RocBankValue);
        if (_started && _previousPrice != 0)
        {
            var change = new ExactMeanAccumulator(); change.Add(price); change.Add(_previousPrice, -1);
            var signed = RocBankValue.Round(change, _previousPrice); move = new(Math.Abs(signed.Mantissa), signed.UpperShift);
        }
        var normalizedMove = _movementRank.Next(move, final); var normalizedVolume = _volumeRank.Next(relative, final);
        var numerator = new ExactMeanAccumulator(); numerator.Add(normalizedVolume); var denominator = new ExactMeanAccumulator(); denominator.Add(normalizedMove);
        var ratio = normalizedMove == 0 ? 0 : numerator.Ratio(denominator);
        return Finish(price, ratio, final);
    }
    internal (double Score, double Demand, Signal Trade) Finish(double price, double ratio, bool final)
    {
        var score = _ratioScore.Next(ratio, default, true, final).Publish();
        var demand = score >= 2 ? (_started ? _previousPrice : 0) : _started ? _demand : price;
        var margin = ExactVarianceWindow.Units(price) - ExactVarianceWindow.Units(demand);
        var trade = margin.Sign > 0 && margin > _previousMargin ? Signal.StrongBuy : margin.Sign < 0 && margin < _previousMargin ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _started = true; _previousPrice = price; _demand = demand; _previousMargin = margin; }
        return (score, demand, trade);
    }
    internal static (double[] Score, double[] Demand, Signal[] Trades) Calculate(StockData data, List<double> prices, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); var means = callbacks ? ComponentAverage.Take(data.Volumes.ToArray(), length) : null;
        using var window = new FreedomWindow(kind, length); var points = Enumerable.Range(0, prices.Count).Select(i => window.Next(prices[i], data.Volumes[i], true, means is null ? null : means[i])).ToArray();
        return (points.Select(v => v.Score).ToArray(), points.Select(v => v.Demand).ToArray(), points.Select(v => v.Trade).ToArray());
    }
    internal void Reset() { _volumeMean.Reset(); _volumeScore.Reset(); _ratioScore.Reset(); _movementRank.Reset(); _volumeRank.Reset(); _started = false; _previousPrice = _demand = 0; _previousMargin = default; }
    public void Dispose() => _volumeMean.Dispose();
    private sealed class Moments
    {
        private readonly int _length; private readonly Queue<BigInteger> _values = new(); private BigInteger _sum, _squares;
        internal Moments(int length) => _length = length;
        internal RocBankValue Next(double value, RocBankValue mean, bool exactCenter, bool final)
        {
            var current = ExactVarianceWindow.Units(value); var sum = _sum + current; var squares = _squares + current * current;
            if (_values.Count == _length) { var old = _values.Peek(); sum -= old; squares -= old * old; }
            var variance = _values.Count < _length - 1 ? BigInteger.Zero : _length * squares - sum * sum;
            var numerator = exactCenter ? _length * current - sum : _length * (current - Units(mean));
            var score = default(RocBankValue);
            if (!variance.IsZero)
            {
                var squared = (numerator * numerator) << 2148; var shift = 0; var root = ExactPopulationDeviation.RootRatio(squared, variance);
                while (double.IsInfinity(root)) { shift += 1024; root = ExactPopulationDeviation.RootRatio(squared, variance << (2 * shift)); }
                score = new(numerator.Sign < 0 ? -root : root, shift);
            }
            if (final) { if (_values.Count == _length) _values.Dequeue(); _values.Enqueue(current); _sum = sum; _squares = squares; }
            return score;
        }
        internal void Reset() { _values.Clear(); _sum = _squares = default; }
    }
    private sealed class Rank
    {
        private readonly int _length; private BigInteger _index;
        private readonly LinkedList<(BigInteger Index, BigInteger Value)> _high = new(), _low = new();
        internal Rank(int length) => _length = length;
        internal double Next(RocBankValue item, bool final)
        {
            var value = Units(item); var expiry = _index - _length + 1; var high = _high.First; var low = _low.First;
            while (high is not null && high.Value.Index < expiry) high = high.Next;
            while (low is not null && low.Value.Index < expiry) low = low.Next;
            var maximum = high is null ? value : BigInteger.Max(value, high.Value.Value); var minimum = low is null ? value : BigInteger.Min(value, low.Value.Value); var range = maximum - minimum;
            var result = range.IsZero ? 0 : ExactMeanAccumulator.UnitRatio((range + 9 * (value - minimum)) * Unit, range);
            if (final)
            {
                while (_high.First is { } oldHigh && oldHigh.Value.Index < expiry) _high.RemoveFirst();
                while (_low.First is { } oldLow && oldLow.Value.Index < expiry) _low.RemoveFirst();
                while (_high.Last is { } tailHigh && tailHigh.Value.Value <= value) _high.RemoveLast();
                while (_low.Last is { } tailLow && tailLow.Value.Value >= value) _low.RemoveLast();
                _high.AddLast((_index, value)); _low.AddLast((_index, value)); _index++;
            }
            return result;
        }
        internal void Reset() { _high.Clear(); _low.Clear(); _index = default; }
    }
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
