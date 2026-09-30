using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FxSniperWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private static readonly BigInteger Constant = ExactVarianceWindow.Units(.015);
    private readonly MovingAvgType _kind; private readonly int _cciLength, _t3Length;
    private readonly BigInteger[] _coefficients, _stages = new BigInteger[6];
    private readonly Queue<BigInteger> _history = new(); private BigInteger _sum, _previous;
    private readonly Average? _mean, _deviation;
    internal FxSniperWindow(MovingAvgType kind, int cciLength, int t3Length, double factor, bool external = false)
    {
        StreamingInputValidation.Finite(factor, nameof(factor)); _kind = kind; _cciLength = Math.Max(1, cciLength); _t3Length = Math.Max(1, t3Length);
        var b = ExactVarianceWindow.Units(factor); var plus = Unit + b;
        _coefficients = new[] { plus * plus * plus, -3 * b * plus * plus, 3 * b * b * plus, -b * b * b };
        if (!external && kind != MovingAvgType.SimpleMovingAverage) { _mean = new(kind, _cciLength); _deviation = new(kind, _cciLength); }
    }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    private static RocBankValue Value(BigInteger units)
    {
        for (var shift = 0; ; shift += 32)
        { var value = ExactMeanAccumulator.UnitRatio(units, BigInteger.One << shift); if (!double.IsInfinity(value)) return new(value, shift); }
    }
    private static BigInteger Quotient(BigInteger numerator, BigInteger denominator)
        => denominator.IsZero ? BigInteger.Zero : denominator.Sign < 0 ? RocBankValue.RoundUnits(-numerator, -denominator) : RocBankValue.RoundUnits(numerator, denominator);
    private BigInteger Cci(double price, bool final, double? externalMean, double? externalDeviation)
    {
        var current = ExactVarianceWindow.Units(price);
        if (_kind == MovingAvgType.SimpleMovingAverage)
        {
            var full = _history.Count == _cciLength; var sum = _sum + current - (full ? _history.Peek() : BigInteger.Zero); var cci = BigInteger.Zero;
            if (_history.Count >= _cciLength - 1)
            {
                var n = new BigInteger(_cciLength); var residual = n * current - sum; var deviation = BigInteger.Abs(residual); var index = 0;
                foreach (var old in _history) { if (!full || index != 0) deviation += BigInteger.Abs(n * old - sum); index++; }
                cci = Quotient(n * residual * Unit * Unit, Constant * deviation);
            }
            if (final) { if (full) _history.Dequeue(); _history.Enqueue(current); _sum = sum; } return cci;
        }
        var mean = externalMean.HasValue ? new RocBankValue(externalMean.Value) : _mean!.Next(new(price), final);
        var difference = RocBankValue.RoundUnits(current - Units(mean), BigInteger.One);
        var dev = externalDeviation.HasValue ? new RocBankValue(externalDeviation.Value) : _deviation!.Next(Value(BigInteger.Abs(difference)), final);
        return Quotient(difference * Unit * Unit, Constant * Units(dev));
    }
    internal (double Line, Signal Trade) Next(double price, bool final, double? externalMean = null, double? externalDeviation = null)
    {
        var value = Cci(price, final, externalMean, externalDeviation); var total = BigInteger.Zero;
        for (var stage = 0; stage < 6; stage++)
        {
            value = RocBankValue.RoundUnits(4 * value + (_t3Length - 1L) * _stages[stage], new BigInteger(_t3Length + 3L));
            if (stage >= 2) total += _coefficients[stage - 2] * value;
            if (final) _stages[stage] = value;
        }
        var line = RocBankValue.RoundUnits(total, Unit * Unit * Unit);
        var trade = line.Sign > 0 && line > _previous ? Signal.StrongBuy : line.Sign < 0 && line < _previous ? Signal.StrongSell : line.Sign > 0 ? Signal.Buy : line.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) _previous = line;
        return (ExactMeanAccumulator.UnitRatio(line, BigInteger.One), trade);
    }
    internal static (double[] Mean, double[] Deviation) Components(StockData data, List<double> prices, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); var caller = data.CaptureInputSeries();
        double[] Mean(List<double> values) => (callbacks ? ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), length)?.ToArray() : null)
            ?? CalculationsHelper.GetMovingAverageList(data, kind, length, values).ToArray();
        var mean = Mean(prices); var residual = new List<double>(prices.Count);
        for (var i = 0; i < prices.Count; i++) residual.Add(Value(BigInteger.Abs(RocBankValue.RoundUnits(ExactVarianceWindow.Units(prices[i]) - ExactVarianceWindow.Units(mean[i]), BigInteger.One))).Publish());
        var deviation = Mean(residual); data.RestoreInputSeries(caller); return (mean, deviation);
    }
    internal void Reset() { _history.Clear(); _sum = _previous = default; Array.Clear(_stages, 0, _stages.Length); _mean?.Reset(); _deviation?.Reset(); }
    public void Dispose() { _mean?.Dispose(); _deviation?.Dispose(); }
    private sealed class Average : IDisposable
    {
        private readonly int _length; private readonly MovingAvgType _kind;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum, _weighted;
        private readonly RocBankAverage? _recursive; private readonly IMovingAverageSmoother? _fallback;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) _recursive = new(kind, length, 1); else if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal RocBankValue Next(RocBankValue value, bool final)
        {
            if (_recursive is not null) return _recursive.Next(value, final);
            if (_fallback is not null) return new(_fallback.Next(value.Publish(), final));
            var sum = _sum; var weighted = _weighted; weighted.Subtract(sum); value.AddTo(ref weighted, _length);
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? RocBankValue.Round(weighted, count: (long)_length * (_length + 1L) / 2)
                : _history.Count < _length - 1 ? default : RocBankValue.Round(sum, count: _length);
            if (final) { _sum = sum; _weighted = weighted; if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _recursive?.Reset(); _fallback?.Reset(); }
        public void Dispose() { _recursive?.Dispose(); _fallback?.Dispose(); }
    }
}
