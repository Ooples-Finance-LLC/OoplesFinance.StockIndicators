using System.Numerics;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class KaseConvergenceWindow : IDisposable
{
    private readonly int _length;
    private readonly BigInteger _sqrt;
    private readonly Mean _atr, _peak, _signal;
    private readonly Queue<(double High, double Low)> _history = new();
    private double _previousPrice;
    private bool _hasPrevious;
    private BigInteger _previousValue;
    internal KaseConvergenceWindow(MovingAvgType kind, int length, int peakLength, int signalLength)
    {
        _length = Math.Max(1, length); _sqrt = U(Math.Sqrt(_length));
        _atr = new(MovingAvgType.WildersSmoothingMethod, _length);
        _peak = new(MovingAvgType.WeightedMovingAverage, peakLength); _signal = new(kind, signalLength);
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static BigInteger Round(BigInteger value, BigInteger divisor) => RocBankValue.RoundUnits(value, divisor);
    private static double Publish(BigInteger value) => ExactMeanAccumulator.UnitRatio(value, BigInteger.One);
    private static BigInteger Range(double high, double low, double previous) => Round(BigInteger.Max(U(high) - U(low),
        BigInteger.Max(BigInteger.Abs(U(high) - U(previous)), BigInteger.Abs(U(low) - U(previous)))), BigInteger.One);
    private BigInteger Difference(double high, double low, double previousHigh, double previousLow, BigInteger atr) => atr.IsZero ? BigInteger.Zero
        : Round((U(high) + U(low) - U(previousHigh) - U(previousLow)) * _sqrt, atr);
    private static Signal Trade(BigInteger value, BigInteger previous) => value.Sign > 0 && value > previous ? Signal.StrongBuy
        : value.Sign < 0 && value < previous ? Signal.StrongSell : value.Sign > 0 ? Signal.Buy : value.Sign < 0 ? Signal.Sell : Signal.None;
    private BigInteger NextPeak(double high, double low, double price, bool final)
    {
        var atr = _atr.Next(Range(high, low, _hasPrevious ? _previousPrice : price), final);
        var previous = _history.Count == _length ? _history.Peek() : default;
        var peak = _peak.Next(Difference(high, low, previous.High, previous.Low, atr), final);
        if (final)
        {
            if (_history.Count == _length) _history.Dequeue(); _history.Enqueue((high, low));
            _previousPrice = price; _hasPrevious = true;
        }
        return peak;
    }
    internal (double Value, Signal Trade) Next(double high, double low, double price, bool final)
    {
        var peak = NextPeak(high, low, price, final); var signal = _signal.Next(peak, final);
        var value = Round(peak - signal, BigInteger.One); var trade = Trade(value, _previousValue);
        if (final) _previousValue = value;
        return (Publish(value), trade);
    }
    internal static (double[] Values, Signal[] Trades) Compute(StockData data, MovingAvgType kind, int length, int peakLength, int signalLength, bool callbacks)
    {
        length = Math.Max(1, length); peakLength = Math.Max(1, peakLength); signalLength = Math.Max(1, signalLength);
        var (input, high, low, _, _) = CalculationsHelper.GetInputValuesList(data);
        var values = new double[input.Count]; var trades = new Signal[input.Count];
        using var window = new KaseConvergenceWindow(StrengthWindow.Supports(kind) ? kind : MovingAvgType.SimpleMovingAverage, length, peakLength, signalLength);
        if (!callbacks || !ComponentAverage.HasOverrides)
        {
            if (StrengthWindow.Supports(kind))
            {
                for (var i = 0; i < input.Count; i++) (values[i], trades[i]) = window.Next(high[i], low[i], input[i], true);
                return (values, trades);
            }
            var peaks = Enumerable.Range(0, input.Count).Select(i => window.NextPeak(high[i], low[i], input[i], true)).ToArray();
            var means = CalculationsHelper.GetMovingAverageList(data, kind, signalLength, peaks.Select(Publish).ToList()); var prior = BigInteger.Zero;
            for (var i = 0; i < input.Count; i++) { var value = Round(peaks[i] - U(means[i]), BigInteger.One); values[i] = Publish(value); trades[i] = Trade(value, prior); prior = value; }
            return (values, trades);
        }
        double[] Average(double[] source, MovingAvgType averageKind, int period) => ComponentAverage.Take(source, period)?.ToArray()
            ?? CalculationsHelper.GetMovingAverageList(data, averageKind, period, source.ToList()).ToArray();
        var ranges = Enumerable.Range(0, input.Count).Select(i => Publish(Range(high[i], low[i], input[i == 0 ? 0 : i - 1]))).ToArray();
        var atr = Average(ranges, MovingAvgType.WildersSmoothingMethod, length);
        var differences = Enumerable.Range(0, input.Count).Select(i => Publish(window.Difference(high[i], low[i], i < length ? 0 : high[i - length], i < length ? 0 : low[i - length], U(atr[i])))).ToArray();
        var peak = Average(differences, MovingAvgType.WeightedMovingAverage, peakLength);
        var signal = Average(peak, kind, signalLength); var previous = BigInteger.Zero;
        for (var i = 0; i < input.Count; i++) { var value = Round(U(peak[i]) - U(signal[i]), BigInteger.One); values[i] = Publish(value); trades[i] = Trade(value, previous); previous = value; }
        return (values, trades);
    }
    internal void Reset() { _atr.Reset(); _peak.Reset(); _signal.Reset(); _history.Clear(); _previousPrice = 0; _hasPrevious = false; _previousValue = default; }
    public void Dispose() { _atr.Dispose(); _peak.Dispose(); _signal.Dispose(); }
    private sealed class Mean : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<BigInteger> _history = new(); private readonly IMovingAverageSmoother? _fallback;
        private BigInteger _sum, _weighted, _previous; private long _count;
        internal Mean(MovingAvgType kind, int length)
        { _kind = kind; _length = Math.Max(1, length); if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, _length); }
        internal BigInteger Next(BigInteger value, bool final)
        {
            if (_fallback is not null) return U(_fallback.Next(Publish(value), final));
            var sum = _sum; var weighted = _weighted; BigInteger result;
            if (_kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
            {
                weighted = weighted - sum + value * _length;
                sum = sum + value - (_history.Count == _length ? _history.Peek() : BigInteger.Zero);
                result = _kind == MovingAvgType.WeightedMovingAverage ? Round(weighted, (long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? BigInteger.Zero : Round(sum, _length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = Round(sum, _count + 1); }
            else
            {
                var ema = _kind == MovingAvgType.ExponentialMovingAverage;
                result = Round(_previous * (_length - 1L) + value * (ema ? 2 : 1), ema ? _length + 1L : _length);
            }
            if (final)
            {
                _sum = sum; _weighted = weighted; _previous = result; _count++;
                if (_kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)
                { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); }
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }
}
