using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RsingWindow : IDisposable
{
    private readonly int _length;
    private readonly Average _volume, _signal;
    private readonly Queue<(double Price, BigInteger Range)> _history = new();
    private BigInteger _sum, _squares;
    private Fraction _previous, _previousChange;
    private static BigInteger Units(double value) => ExactVarianceWindow.Units(value);
    internal RsingWindow(MovingAvgType kind, int length)
    { _length = Math.Max(1, length); _volume = new(kind, _length); _signal = new(kind, _length); }
    private Fraction Line(double price, double high, double low, double volume, Fraction mean, bool final)
    {
        var range = Units(high) - Units(low); var full = _history.Count == _length;
        var old = full ? _history.Peek() : (Price: 0d, Range: BigInteger.Zero);
        var sum = _sum + range - old.Range; var squares = _squares + range * range - old.Range * old.Range;
        var variance = _length * squares - sum * sum;
        var value = default(Fraction);
        if (full && mean.Sign != 0 && variance.Sign > 0)
        {
            var change = Units(price) - Units(old.Price);
            var numerator = new Fraction(change * Units(volume) * range).Times(_length) / mean;
            value = numerator.RoundedOverRoot(variance);
        }
        if (final)
        { if (full) _history.Dequeue(); _history.Enqueue((price, range)); _sum = sum; _squares = squares; }
        return value;
    }
    private (double Line, double Signal, Signal Trade) Finish(Fraction line, Fraction signal, bool final)
    {
        var change = signal - _previous; var acceleration = change - _previousChange;
        var trade = change.Sign > 0 && acceleration.Sign > 0 ? Signal.StrongBuy : change.Sign < 0 && acceleration.Sign < 0 ? Signal.StrongSell
            : change.Sign > 0 ? Signal.Buy : change.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _previous = signal; _previousChange = change; }
        return (line.PublishUnits(), signal.PublishUnits(), trade);
    }
    internal (double Line, double Signal, Signal Trade) Next(double price, double high, double low, double volume, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); StreamingInputValidation.Finite(high, nameof(high));
        StreamingInputValidation.Finite(low, nameof(low)); StreamingInputValidation.Finite(volume, nameof(volume));
        var line = Line(price, high, low, volume, _volume.Next(new Fraction(Units(volume)), final), final);
        return Finish(line, _signal.Next(line, final), final);
    }
    internal static (double[] Line, double[] Signal, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, bool callbacks)
    {
        length = Math.Max(1, length); var (prices, highs, lows, _, volumes) = CalculationsHelper.GetInputValuesList(data);
        for (var i = 0; i < prices.Count; i++)
        { StreamingInputValidation.Finite(prices[i], nameof(prices)); StreamingInputValidation.Finite(highs[i], nameof(highs)); StreamingInputValidation.Finite(lows[i], nameof(lows)); StreamingInputValidation.Finite(volumes[i], nameof(volumes)); }
        using var window = new RsingWindow(kind, length); var line = new double[prices.Count]; var signal = new double[prices.Count]; var trades = new Signal[prices.Count];
        if (!callbacks || !ComponentAverage.HasOverrides)
        { for (var i = 0; i < prices.Count; i++) (line[i], signal[i], trades[i]) = window.Next(prices[i], highs[i], lows[i], volumes[i], true); }
        else
        {
            var caller = data.CaptureInputSeries();
            try
            {
                double[] Average(double[] values)
                {
                    var result = ComponentAverage.Take(values, length)?.ToArray() ?? CalculationsHelper.GetMovingAverageList(data, kind, length, values.ToList()).ToArray();
                    data.RestoreInputSeries(caller); foreach (var value in result) StreamingInputValidation.Finite(value, nameof(result)); return result;
                }
                var mean = Average(volumes.ToArray());
                for (var i = 0; i < prices.Count; i++) line[i] = window.Line(prices[i], highs[i], lows[i], volumes[i], new Fraction(Units(mean[i])), true).PublishUnits();
                foreach (var value in line) StreamingInputValidation.Finite(value, nameof(line));
                var average = Average(line);
                for (var i = 0; i < prices.Count; i++) (line[i], signal[i], trades[i]) = window.Finish(new Fraction(Units(line[i])), new Fraction(Units(average[i])), true);
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return (line, signal, trades);
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
        internal Fraction Times(BigInteger value) => new(_numerator * value, Denominator);
        internal Fraction Divide(long value) => new(_numerator, Denominator * value);
        internal double PublishUnits() => Sign * ExactPopulationDeviation.RootRatio(_numerator * _numerator, Denominator * Denominator);
        internal Fraction RoundedOverRoot(BigInteger square)
        {
            var denominator = Denominator * Denominator * square;
            var numerator = _numerator * _numerator;
            var value = ExactPopulationDeviation.RootRatio(numerator, denominator); var shift = 0;
            // Keep the same 53-bit oscillator component beyond binary64's upper
            // exponent so a later signal mean can still cancel to a finite value.
            while (double.IsInfinity(value))
            { shift += 512; denominator <<= 1024; value = ExactPopulationDeviation.RootRatio(numerator, denominator); }
            return new Fraction(Units(Sign * value) << shift);
        }
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
    internal void Reset() { _history.Clear(); _volume.Reset(); _signal.Reset(); _sum = _squares = default; _previous = _previousChange = default; }
    public void Dispose() { Reset(); _volume.Dispose(); _signal.Dispose(); }
}
