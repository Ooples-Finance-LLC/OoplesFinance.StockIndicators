using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FisherLeastSquaresWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _length;
    private readonly Average? _priceMean, _indexMean;
    private readonly Queue<BigInteger> _prices = new(), _residuals = new();
    private BigInteger _sum, _squares, _signed, _absolute, _index, _previousLine, _previousMargin;
    internal FisherLeastSquaresWindow(MovingAvgType kind, int length, bool means = true)
    { _length = Math.Max(1, length); if (means) { _priceMean = new(kind, _length); _indexMean = new(kind, _length); } }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    private static RocBankValue FromUnits(BigInteger value)
    {
        var shift = 0; var rounded = ExactMeanAccumulator.UnitRatio(value, BigInteger.One);
        while (double.IsInfinity(rounded)) { shift += 1024; rounded = ExactMeanAccumulator.UnitRatio(value, BigInteger.One << shift); }
        return new(rounded, shift);
    }
    internal (double Line, Signal Trade) Next(double price, bool final)
    {
        if (double.IsNaN(price) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price));
        var mean = _priceMean!.Next(new(price), final); var indexMean = _indexMean!.Next(FromUnits(_index * Unit), final);
        return Finish(price, mean, indexMean, final);
    }
    internal (double Line, Signal Trade) Finish(double price, RocBankValue mean, RocBankValue indexMean, bool final)
    {
        var value = ExactVarianceWindow.Units(price);
        var residual = RocBankValue.RoundUnits(value - (_index.IsZero ? value : _previousLine), BigInteger.One);
        var signed = _signed + residual; var absolute = _absolute + BigInteger.Abs(residual);
        if (_residuals.Count == _length) { var old = _residuals.Peek(); signed -= old; absolute -= BigInteger.Abs(old); }
        var sum = _sum + value; var squares = _squares + value * value;
        if (_prices.Count == _length) { var old = _prices.Peek(); sum -= old; squares -= old * old; }
        var radicand = _prices.Count < _length - 1 ? BigInteger.Zero : _length * squares - sum * sum;
        var correction = default(RocBankValue);
        if (_length > 1 && !radicand.IsZero && !absolute.IsZero)
        {
            // The means of signed and absolute residuals have the same divisor.
            var z = ExactMeanAccumulator.UnitRatio(signed * Unit, absolute);
            var factor = (_index * Unit - Units(indexMean)) * ExactVarianceWindow.Units(Math.Tanh(z));
            // Full consecutive-index population variance is (N*N-1)/12.
            // Round the complete correction, not an underflowing variance or deviation.
            var numerator = factor * factor * radicand * 12;
            var denominator = ((BigInteger)_length * _length * ((BigInteger)_length * _length - 1)) << 4296;
            var magnitude = ExactPopulationDeviation.RootRatio(numerator, denominator); var shift = 0;
            while (double.IsInfinity(magnitude)) { shift += 1024; magnitude = ExactPopulationDeviation.RootRatio(numerator, denominator << (2 * shift)); }
            correction = new(factor.Sign < 0 ? -magnitude : magnitude, shift);
        }
        var result = new ExactMeanAccumulator(); mean.AddTo(ref result); correction.AddTo(ref result);
        var line = RocBankValue.Round(result); var lineUnits = Units(line); var margin = value - lineUnits;
        var previousMargin = _index.IsZero ? -value : _previousMargin;
        var trade = margin.Sign > 0 && margin > previousMargin ? Signal.StrongBuy : margin.Sign < 0 && margin < previousMargin ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(value);
            if (_residuals.Count == _length) _residuals.Dequeue(); _residuals.Enqueue(residual);
            _sum = sum; _squares = squares; _signed = signed; _absolute = absolute; _previousLine = lineUnits; _previousMargin = margin; _index++;
        }
        return (line.Publish(), trade);
    }
    internal static (double[] Line, Signal[] Trades) Calculate(StockData data, List<double> input, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); var caller = data.CaptureInputSeries();
        RocBankValue[] Mean(double[] source)
        {
            var custom = callbacks ? ComponentAverage.Take(source, length) : null;
            if (custom is not null) return custom.Select(v => new RocBankValue(v)).ToArray();
            if (!StrengthWindow.Supports(kind)) return CalculationsHelper.GetMovingAverageList(data, kind, length, source.ToList()).Select(v => new RocBankValue(v)).ToArray();
            using var mean = new Average(kind, length); return source.Select(v => mean.Next(new(v), true)).ToArray();
        }
        try
        {
            var means = Mean(input.ToArray()); var indices = Mean(Enumerable.Range(0, input.Count).Select(i => (double)i).ToArray());
            using var window = new FisherLeastSquaresWindow(kind, length, false);
            var points = Enumerable.Range(0, input.Count).Select(i => window.Finish(input[i], means[i], indices[i], true)).ToArray();
            return (points.Select(v => v.Line).ToArray(), points.Select(v => v.Trade).ToArray());
        }
        finally { data.RestoreInputSeries(caller); }
    }
    internal void Reset() { _priceMean?.Reset(); _indexMean?.Reset(); _prices.Clear(); _residuals.Clear(); _sum = _squares = _signed = _absolute = _index = _previousLine = _previousMargin = default; }
    public void Dispose() { _priceMean?.Dispose(); _indexMean?.Dispose(); }
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
