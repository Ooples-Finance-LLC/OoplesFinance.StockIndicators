using System.Numerics;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class KasePeakV2Window : IDisposable
{
    private readonly int _fast, _slow, _deviationLength, _smooth;
    private readonly BigInteger _sensitivity;
    private readonly Mean _volatility;
    private readonly Queue<double> _returns = new();
    private readonly Queue<BigInteger> _up = new(), _down = new();
    private readonly List<(double High, double Low)> _history = new();
    private int _first;
    private BigInteger _sum, _squares, _upSum, _downSum, _previousOutput;
    private double _previousPrice;
    private bool _hasPrevious;
    internal KasePeakV2Window(MovingAvgType kind, int fast, int slow, int deviationLength, int averageLength, int smooth, double sensitivity)
    {
        if (MathHelper.IsValueNullOrInfinity(sensitivity)) throw new ArgumentOutOfRangeException(nameof(sensitivity));
        _fast = Math.Max(1, fast); _slow = Math.Max(1, slow); _deviationLength = Math.Max(1, deviationLength); _smooth = Math.Max(1, smooth);
        _sensitivity = U(sensitivity); _volatility = new(kind, averageLength);
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static BigInteger Round(BigInteger value, BigInteger divisor) => RocBankValue.RoundUnits(value * divisor.Sign, BigInteger.Abs(divisor));
    private static double Publish(BigInteger value) => ExactMeanAccumulator.UnitRatio(value, BigInteger.One);
    internal static double Log(double numerator, double denominator) => StableLogRatio.OfSameSign(numerator, denominator);
    private double Deviation(double price, bool final)
    {
        var result = 0d;
        if (_hasPrevious)
        {
            var log = Log(price, _previousPrice); var units = U(log); var sum = _sum + units; var squares = _squares + units * units;
            if (_returns.Count == _deviationLength) { var expired = U(_returns.Peek()); sum -= expired; squares -= expired * expired; }
            if (_returns.Count >= _deviationLength - 1)
                result = ExactPopulationDeviation.RootRatio(_deviationLength * squares - sum * sum, (long)_deviationLength * _deviationLength);
            if (final) { if (_returns.Count == _deviationLength) _returns.Dequeue(); _returns.Enqueue(log); _sum = sum; _squares = squares; }
        }
        if (final) { _previousPrice = price; _hasPrevious = true; }
        return result;
    }
    internal (double Value, Signal Trade) Next(double high, double low, double price, bool final, double? externalAverage = null)
    {
        var deviation = Deviation(price, final);
        var divisor = externalAverage.HasValue ? U(externalAverage.Value) : _volatility.Next(U(deviation), final);
        double maxUp = 0, maxDown = 0;
        var lastLag = Math.Min(_slow - 1, _history.Count - _first);
        for (var lag = _fast; lag <= lastLag; lag++)
        {
            var prior = _history[_history.Count - lag]; var root = Math.Sqrt(lag);
            maxUp = Math.Max(maxUp, Log(high, prior.Low) / root);
            maxDown = Math.Max(maxDown, Log(prior.High, low) / root);
        }
        var up = divisor.IsZero ? BigInteger.Zero : Round(U(maxUp) << 1074, divisor);
        var down = divisor.IsZero ? BigInteger.Zero : Round(U(maxDown) << 1074, divisor);
        var upSum = _upSum + up - (_up.Count == _smooth ? _up.Peek() : BigInteger.Zero);
        var downSum = _downSum + down - (_down.Count == _smooth ? _down.Peek() : BigInteger.Zero);
        var count = Math.Min(_up.Count + 1L, _smooth);
        var upMean = Round(upSum, count); var downMean = Round(downSum, count);
        var output = Round((upMean - downMean) * _sensitivity, BigInteger.One << 1074);
        var trade = output.Sign > 0 && output > _previousOutput ? Signal.StrongBuy : output.Sign < 0 && output < _previousOutput ? Signal.StrongSell
            : output.Sign > 0 ? Signal.Buy : output.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (_up.Count == _smooth) { _up.Dequeue(); _down.Dequeue(); } _up.Enqueue(up); _down.Enqueue(down); _upSum = upSum; _downSum = downSum; _previousOutput = output;
            if (_slow > 1)
            {
                if (_history.Count - _first == _slow - 1) _first++;
                _history.Add((high, low));
                if (_first > 1024 && _first >= _history.Count / 2) { _history.RemoveRange(0, _first); _first = 0; }
            }
        }
        return (Publish(output), trade);
    }
    internal static (double[] Values, Signal[] Trades) Compute(StockData data, MovingAvgType kind, int fast, int slow, int deviationLength,
        int averageLength, int smooth, double sensitivity, bool callbacks)
    {
        using var window = new KasePeakV2Window(kind, fast, slow, deviationLength, averageLength, smooth, sensitivity);
        var (input, high, low, _, _) = CalculationsHelper.GetInputValuesList(data); double[]? external = null;
        if (callbacks && ComponentAverage.HasOverrides)
        {
            var deviations = input.Select(price => window.Deviation(price, true)).ToArray();
            external = ComponentAverage.Take(deviations, Math.Max(1, averageLength))?.ToArray()
                ?? CalculationsHelper.GetMovingAverageList(data, kind, Math.Max(1, averageLength), deviations.ToList()).ToArray();
            window.Reset();
        }
        var values = new double[input.Count]; var trades = new Signal[input.Count];
        for (var i = 0; i < input.Count; i++) (values[i], trades[i]) = window.Next(high[i], low[i], input[i], true, external?[i]);
        return (values, trades);
    }
    internal void Reset()
    {
        _returns.Clear(); _up.Clear(); _down.Clear(); _history.Clear(); _volatility.Reset(); _first = 0;
        _sum = _squares = _upSum = _downSum = _previousOutput = default; _previousPrice = 0; _hasPrevious = false;
    }
    public void Dispose() => _volatility.Dispose();
    private sealed class Mean : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<BigInteger> _history = new(); private readonly SpreadAverage? _recursive;
        private BigInteger _sum, _weighted; private long _count;
        internal Mean(MovingAvgType kind, int length)
        {
            _kind = kind; _length = Math.Max(1, length);
            if (kind is not (MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage)) _recursive = new(kind, _length);
        }
        internal BigInteger Next(BigInteger value, bool final)
        {
            if (_recursive is not null) return U(_recursive.Next(new SpreadNumber(Publish(value)), final).Value);
            var weighted = _weighted - _sum + value * _length;
            var sum = _sum + value - (_history.Count == _length ? _history.Peek() : BigInteger.Zero);
            var result = _kind == MovingAvgType.WeightedMovingAverage ? Round(weighted, (long)_length * (_length + 1L) / 2)
                : _count + 1 < _length ? BigInteger.Zero : Round(sum, _length);
            if (final)
            {
                _sum = sum; _weighted = weighted; _count++;
                if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value);
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _count = 0; _recursive?.Reset(); }
        public void Dispose() => _recursive?.Dispose();
    }
}
