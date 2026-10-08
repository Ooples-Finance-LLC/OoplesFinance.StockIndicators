using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FisherStochasticWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _range, _smooth;
    private readonly Average[]? _means;
    private readonly LinkedList<(BigInteger Index, BigInteger Value)> _high = new(), _low = new();
    private readonly Queue<(BigInteger Numerator, BigInteger Denominator)> _terms = new();
    private BigInteger _index, _numerator, _denominator, _previousLine, _previousSlope;
    internal FisherStochasticWindow(MovingAvgType kind, int length, int range, int smooth, bool means = true)
    { _range = Math.Max(1, range); _smooth = Math.Max(1, smooth); if (means) _means = Enumerable.Range(0, 10).Select(_ => new Average(kind, Math.Max(1, length))).ToArray(); }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    internal static RocBankValue Rainbow(IReadOnlyList<RocBankValue> stages)
    { var sum = new ExactMeanAccumulator(); for (var i = 0; i < 10; i++) stages[i].AddTo(ref sum, Math.Max(1, 5 - i)); return RocBankValue.Round(sum, count: 20); }
    internal (double Line, Signal Trade) Next(double price, bool final)
    {
        if (double.IsNaN(price) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        var stage = new RocBankValue(price); var sum = new ExactMeanAccumulator();
        for (var i = 0; i < 10; i++) { stage = _means![i].Next(stage, final); stage.AddTo(ref sum, Math.Max(1, 5 - i)); }
        return Finish(RocBankValue.Round(sum, count: 20), final);
    }
    internal (double Line, Signal Trade) Finish(RocBankValue rainbow, bool final)
    {
        var value = Units(rainbow); var expiry = _index - _range + 1; var high = _high.First; var low = _low.First;
        while (high is not null && high.Value.Index < expiry) high = high.Next;
        while (low is not null && low.Value.Index < expiry) low = low.Next;
        var highest = high is null ? value : BigInteger.Max(value, high.Value.Value); var lowest = low is null ? value : BigInteger.Min(value, low.Value.Value);
        var numerator = RocBankValue.RoundUnits(value - lowest, BigInteger.One); var denominator = RocBankValue.RoundUnits(highest - lowest, BigInteger.One);
        var numeratorSum = _numerator + numerator; var denominatorSum = _denominator + denominator;
        if (_terms.Count == _smooth) { numeratorSum -= _terms.Peek().Numerator; denominatorSum -= _terms.Peek().Denominator; }
        var stochastic = ExactMeanAccumulator.UnitRatio(100 * numeratorSum * Unit, denominatorSum + ExactVarianceWindow.Units(.0001));
        stochastic = Math.Max(0, Math.Min(100, stochastic));
        var exponent = new ExactMeanAccumulator(); exponent.Add(50); exponent.Add(stochastic, -1);
        var logisticDenominator = new ExactMeanAccumulator(); logisticDenominator.Add(1); logisticDenominator.Add(Math.Exp(exponent.Mean(5)));
        var hundred = new ExactMeanAccumulator(); hundred.Add(100); var line = hundred.Ratio(logisticDenominator);
        var current = ExactVarianceWindow.Units(line); var slope = current - _previousLine;
        var trade = slope.Sign > 0 && slope > _previousSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < _previousSlope ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        // Crossings of the inherited 30/70 thresholds imply the same slope directions.
        if (final)
        {
            while (_high.First is { } h && h.Value.Index < expiry) _high.RemoveFirst();
            while (_low.First is { } l && l.Value.Index < expiry) _low.RemoveFirst();
            while (_high.Last is { } highTail && highTail.Value.Value <= value) _high.RemoveLast();
            while (_low.Last is { } lowTail && lowTail.Value.Value >= value) _low.RemoveLast();
            _high.AddLast((_index, value)); _low.AddLast((_index, value));
            if (_terms.Count == _smooth) _terms.Dequeue(); _terms.Enqueue((numerator, denominator));
            _numerator = numeratorSum; _denominator = denominatorSum; _previousLine = current; _previousSlope = slope; _index++;
        }
        return (line, trade);
    }
    internal static (double[] Line, Signal[] Trades) Calculate(StockData data, List<double> input, MovingAvgType kind, int length, int range, int smooth, bool callbacks)
    {
        length = Math.Max(1, length); var caller = data.CaptureInputSeries();
        RocBankValue[] Mean(RocBankValue[] source)
        {
            var published = callbacks || !StrengthWindow.Supports(kind) ? source.Select(v => v.Publish()).ToArray() : null;
            var custom = callbacks ? ComponentAverage.Take(published!, length) : null;
            if (custom is not null) return custom.Select(v => new RocBankValue(v)).ToArray();
            if (!StrengthWindow.Supports(kind)) return CalculationsHelper.GetMovingAverageList(data, kind, length, published!.ToList()).Select(v => new RocBankValue(v)).ToArray();
            using var mean = new Average(kind, length); return source.Select(v => mean.Next(v, true)).ToArray();
        }
        try
        {
            var stages = new RocBankValue[10][]; var previous = input.Select(v => new RocBankValue(v)).ToArray();
            for (var depth = 0; depth < 10; depth++) { stages[depth] = Mean(previous); previous = stages[depth]; }
            using var window = new FisherStochasticWindow(kind, length, range, smooth, false);
            var points = Enumerable.Range(0, input.Count).Select(i => window.Finish(Rainbow(stages.Select(v => v[i]).ToArray()), true)).ToArray();
            return (points.Select(v => v.Line).ToArray(), points.Select(v => v.Trade).ToArray());
        }
        finally { data.RestoreInputSeries(caller); }
    }
    internal void Reset() { if (_means is not null) foreach (var mean in _means) mean.Reset(); _high.Clear(); _low.Clear(); _terms.Clear(); _index = _numerator = _denominator = _previousLine = _previousSlope = default; }
    public void Dispose() { if (_means is not null) foreach (var mean in _means) mean.Dispose(); }
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
