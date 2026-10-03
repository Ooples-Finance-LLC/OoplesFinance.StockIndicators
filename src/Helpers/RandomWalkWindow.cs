using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RandomWalkWindow : IDisposable
{
    private readonly int _length;
    private readonly Average _average;
    private readonly Queue<(double High, double Low)> _history = new();
    private double _previousClose;
    private bool _hasPrevious;
    private Fraction _previousSpread;
    internal RandomWalkWindow(MovingAvgType kind, int length)
    { _length = Math.Max(1, length); _average = new(kind, _length); }
    private static BigInteger Units(double value) => ExactVarianceWindow.Units(value);
    private static BigInteger Range(double high, double low, double previous)
        => BigInteger.Max(Units(high) - Units(low), BigInteger.Max(BigInteger.Abs(Units(high) - Units(previous)), BigInteger.Abs(Units(low) - Units(previous))));
    private (double High, double Low, Signal Trade) Finish(double high, double low, Fraction atr, bool final)
    {
        var prior = _history.Count == _length ? _history.Peek() : (High: 0d, Low: 0d);
        var up = new Fraction(Units(high) - Units(prior.Low)); var down = new Fraction(Units(prior.High) - Units(low));
        var highRatio = atr.Sign == 0 ? default : up / atr; var lowRatio = atr.Sign == 0 ? default : down / atr;
        var spread = highRatio - lowRatio; var change = spread - _previousSpread;
        var trade = spread.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : spread.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
            : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue((high, low)); _previousSpread = spread; }
        return (highRatio.RootScale(_length), lowRatio.RootScale(_length), trade);
    }
    internal (double High, double Low, Signal Trade) Next(double high, double low, double close, bool final)
    {
        StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low)); StreamingInputValidation.Finite(close, nameof(close));
        var range = Range(high, low, _hasPrevious ? _previousClose : close);
        var atr = _average.Next(new Fraction(range), final); var result = Finish(high, low, atr, final);
        if (final) { _previousClose = close; _hasPrevious = true; }
        return result;
    }
    internal static (double[] High, double[] Low, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); var (prices, highs, lows, _, _) = CalculationsHelper.GetInputValuesList(data);
        for (var i = 0; i < prices.Count; i++)
        { StreamingInputValidation.Finite(prices[i], nameof(prices)); StreamingInputValidation.Finite(highs[i], nameof(highs)); StreamingInputValidation.Finite(lows[i], nameof(lows)); }
        using var window = new RandomWalkWindow(kind, length);
        var high = new double[prices.Count]; var low = new double[prices.Count]; var trades = new Signal[prices.Count];
        if (StrengthWindow.Supports(kind) && !ComponentAverage.HasOverrides)
        { for (var i = 0; i < prices.Count; i++) (high[i], low[i], trades[i]) = window.Next(highs[i], lows[i], prices[i], true); }
        else
        {
            var caller = data.CaptureInputSeries();
            try
            {
                double[] averages;
                if (callbacks)
                {
                    var ranges = CalculationsHelper.GetTrueRangeList(data);
                    averages = ComponentAverage.Take(ranges.ToArray(), length)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, length, ranges).ToArray();
                }
                else
                {
#pragma warning disable CS0618
                    averages = data.CalculateAverageTrueRange(kind, length).ChainedValues.ToArray();
#pragma warning restore CS0618
                }
                data.RestoreInputSeries(caller);
                for (var i = 0; i < prices.Count; i++) (high[i], low[i], trades[i]) = window.Finish(highs[i], lows[i], new Fraction(Units(averages[i])), true);
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return (high, low, trades);
    }
    private readonly struct Fraction
    {
        private readonly BigInteger _numerator, _denominator;
        private BigInteger Denominator => _denominator.IsZero ? BigInteger.One : _denominator;
        internal Fraction(BigInteger numerator) { _numerator = numerator; _denominator = BigInteger.One; }
        private Fraction(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero) throw new DivideByZeroException();
            if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
            var common = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            _numerator = numerator / common; _denominator = denominator / common;
        }
        internal int Sign => _numerator.Sign;
        public static Fraction operator +(Fraction a, Fraction b)
        {
            var common = BigInteger.GreatestCommonDivisor(a.Denominator, b.Denominator);
            var aScale = b.Denominator / common; var bScale = a.Denominator / common;
            return new(a._numerator * aScale + b._numerator * bScale, a.Denominator * aScale);
        }
        public static Fraction operator -(Fraction a, Fraction b) => a + new Fraction(-b._numerator, b.Denominator);
        public static Fraction operator /(Fraction a, Fraction b) => new(a._numerator * b.Denominator, a.Denominator * b._numerator);
        internal Fraction Times(long value) => new(_numerator * value, Denominator);
        internal Fraction Divide(long value) => new(_numerator, Denominator * value);
        internal double PublishUnits() => Sign * ExactPopulationDeviation.RootRatio(_numerator * _numerator, Denominator * Denominator);
        internal double RootScale(int length) => Sign * ExactPopulationDeviation.RootRatio((_numerator * _numerator) << 2148, Denominator * Denominator * length);
    }
    private sealed class Average : IDisposable
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<Fraction> _history = new(); private readonly IMovingAverageSmoother? _fallback;
        private Fraction _sum, _weighted, _previous; private long _count;
        internal Average(MovingAvgType kind, int length)
        { _kind = kind; _length = length; if (!StrengthWindow.Supports(kind)) _fallback = MovingAverageSmootherFactory.Create(kind, length); }
        internal Fraction Next(Fraction value, bool final)
        {
            if (_fallback is not null) return new(Units(_fallback.Next(value.PublishUnits(), final)));
            var sum = _sum; var weighted = _weighted; Fraction result;
            var window = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (window)
            {
                weighted = weighted - sum + value.Times(_length);
                if (_history.Count == _length) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Divide((long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? default : sum.Divide(_length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = sum.Divide(_count + 1); }
            else
            { var ema = _kind == MovingAvgType.ExponentialMovingAverage; result = (_previous.Times(_length - 1L) + value.Times(ema ? 2 : 1)).Divide(ema ? _length + 1L : _length); }
            if (final) { if (window) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } _sum = sum; _weighted = weighted; _previous = result; _count++; }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; _fallback?.Reset(); }
        public void Dispose() => _fallback?.Dispose();
    }
    internal void Reset() { _history.Clear(); _average.Reset(); _hasPrevious = false; _previousClose = 0; _previousSpread = default; }
    public void Dispose() { Reset(); _average.Dispose(); }
}
