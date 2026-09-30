using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FastSlowDegreeWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _length; private readonly Sum _fast, _slow, _weighted;
    private readonly Average? _signal;
    private BigInteger _count, _previousPrice, _previousSpread;
    internal FastSlowDegreeWindow(MovingAvgType kind, int length, int fastLength, int slowLength, int signalLength, bool external = false)
    {
        _length = Math.Max(1, length); _fast = new(fastLength); _slow = new(slowLength); _weighted = new(_length);
        if (!external || StrengthWindow.Supports(kind)) _signal = new(kind, Math.Max(1, signalLength));
    }
    internal static double SinePhase(BigInteger phase, int length)
    {
        length = Math.Max(1, length); var period = 2L * length; var residue = (long)(phase % period); if (residue < 0) residue += period;
        var negative = residue >= length; if (negative) residue -= length;
        if (2 * residue > length) residue = length - residue;
        var value = residue == 0 ? 0 : 2 * residue == length ? 1 : Math.Sin(Angle(residue, length));
        return negative ? -value : value;
    }
    private static double Angle(long numerator, int denominator) { var sum = new ExactMeanAccumulator(); sum.Add(Math.PI, new BigInteger(numerator)); return sum.Mean(denominator); }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    private static RocBankValue Value(BigInteger units)
    {
        for (var shift = 0; ; shift += 32)
        { var value = ExactMeanAccumulator.UnitRatio(units, BigInteger.One << shift); if (!double.IsInfinity(value)) return new(value, shift); }
    }
    internal (double Line, double SignalLine, double Histogram, Signal Trade) Next(double price, bool final, double? externalSignal = null, bool includeSignal = true)
    {
        var n = _count + 1; var first = ExactVarianceWindow.Units(SinePhase(n * n, _length)); var second = ExactVarianceWindow.Units(SinePhase(n * (n - 1), _length));
        var coefficient = RocBankValue.RoundUnits(first - second, n);
        // The common quadratic part of the two weighted legs cancels exactly.
        var difference = _fast.Next(coefficient, final) - _slow.Next(coefficient, final);
        var weighted = RocBankValue.RoundUnits(_previousPrice * difference, Unit);
        var line = RocBankValue.RoundUnits(_weighted.Next(weighted, final), BigInteger.One);
        var signal = !includeSignal ? default : externalSignal.HasValue ? new RocBankValue(externalSignal.Value) : _signal!.Next(Value(line), final);
        var spread = line - Units(signal);
        var trade = spread.Sign > 0 && spread > _previousSpread ? Signal.StrongBuy : spread.Sign < 0 && spread < _previousSpread ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _count = n; _previousPrice = ExactVarianceWindow.Units(price); _previousSpread = spread; }
        return (ExactMeanAccumulator.UnitRatio(line, BigInteger.One), signal.Publish(), ExactMeanAccumulator.UnitRatio(spread, BigInteger.One), trade);
    }
    internal static double[] ComponentSignal(StockData data, List<double> prices, MovingAvgType kind, int length, int fastLength, int slowLength, int signalLength, bool callbacks)
    {
        var supported = StrengthWindow.Supports(kind); using var window = new FastSlowDegreeWindow(kind, length, fastLength, slowLength, signalLength, true);
        var line = new double[prices.Count]; var defaultSignal = new double[prices.Count];
        for (var i = 0; i < prices.Count; i++) { var point = window.Next(prices[i], true, includeSignal: supported); line[i] = point.Line; defaultSignal[i] = point.SignalLine; }
        var custom = callbacks ? ComponentAverage.Take(line, Math.Max(1, signalLength))?.ToArray() : null;
        if (custom is not null) return custom;
        if (supported) return defaultSignal;
        var caller = data.CaptureInputSeries(); var result = CalculationsHelper.GetMovingAverageList(data, kind, Math.Max(1, signalLength), line.ToList()).ToArray(); data.RestoreInputSeries(caller); return result;
    }
    internal void Reset() { _fast.Reset(); _slow.Reset(); _weighted.Reset(); _signal?.Reset(); _count = _previousPrice = _previousSpread = default; }
    public void Dispose() => _signal?.Dispose();
    private sealed class Sum
    {
        private readonly int _length; private readonly Queue<BigInteger> _history = new(); private BigInteger _total;
        internal Sum(int length) => _length = Math.Max(1, length);
        internal BigInteger Next(BigInteger value, bool final)
        {
            var full = _history.Count == _length; var total = _total + value - (full ? _history.Peek() : BigInteger.Zero);
            if (final) { if (full) _history.Dequeue(); _history.Enqueue(value); _total = total; } return total;
        }
        internal void Reset() { _history.Clear(); _total = default; }
    }
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
