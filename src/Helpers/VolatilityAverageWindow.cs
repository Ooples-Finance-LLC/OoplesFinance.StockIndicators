using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// The score is algebraic, but the selected period and final weighted prices are rational.
internal sealed class VolatilityAverageWindow : IDisposable
{
    private readonly int _length, _lookback;
    private readonly Average _mean, _scores, _output;
    private readonly Moments _moments;
    private readonly Queue<Number> _history = new();
    private Number _previousMargin;
    internal VolatilityAverageWindow(MovingAvgType kind, int length, int lookback, int smooth)
    {
        _length = Math.Max(1, length); _lookback = Math.Max(1, lookback);
        _mean = new(kind, _lookback); _scores = new(kind, smooth); _output = new(kind, smooth); _moments = new(_lookback);
    }
    private static Signal Trade(Number margin, Number previous)
        => margin.Sign > 0 && (margin - previous).Sign > 0 ? Signal.StrongBuy
            : margin.Sign < 0 && (margin - previous).Sign < 0 ? Signal.StrongSell
            : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
    internal (double Value, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var value = Number.Of(price);
        var mean = _mean.Next(Radical.Of(value), final).Rational(); var variance = _moments.Next(value, final);
        var score = Radical.QuotientRoot((value - mean).Times(100), variance);
        var period = Period(_scores.Next(score, final), _length, _lookback);
        var history = _history.ToArray(); var weighted = value.Times(period);
        for (var lag = 1; lag < period && lag <= history.Length; lag++) weighted += history[history.Length - lag].Times(period - lag);
        var average = weighted.Divide((long)period * (period + 1L) / 2);
        var line = _output.Next(Radical.Of(average), final).Rational(); var margin = value - line; var trade = Trade(margin, _previousMargin);
        if (final)
        {
            if (_history.Count == _length) _history.Dequeue();
            _history.Enqueue(value); _previousMargin = margin;
        }
        return (line.Publish(), trade);
    }
    internal static int Period(Radical score, int length, int lookback)
    {
        length = Math.Max(1, length); lookback = Math.Max(1, lookback);
        if (lookback > 200) return length; // Even at the clamp, abs(score)/lookback is below 1/2.
        if (score.Compare(default) < 0) score = score.Times(Number.Integer(-1));
        var level = 0;
        if (score.Compare(Number.Integer(100)) >= 0) level = (int)RoundEven(100, lookback);
        else
        {
            for (; level <= 100; level++)
            {
                var comparison = score.Compare(Number.Integer((2L * level + 1) * lookback).Divide(2));
                if (comparison < 0) break;
                if (comparison == 0) { level += level % 2; break; }
            }
        }
        return (int)RoundEven(Math.Max(10, (long)length * (10 - level)), 10);
    }
    private static long RoundEven(long numerator, long denominator)
    {
        var quotient = numerator / denominator; var remainder = numerator % denominator;
        return quotient + (2 * remainder > denominator || 2 * remainder == denominator && quotient % 2 != 0 ? 1 : 0);
    }
    internal static (double[] Values, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, int lookback, int smooth, bool fast = false)
    {
        length = Math.Max(1, length); lookback = Math.Max(1, lookback); smooth = Math.Max(1, smooth);
        foreach (var values in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues })
            foreach (var value in values) StreamingInputValidation.Finite(value, nameof(data));
        var prices = (data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues).Select(Number.Of).ToArray();
        Radical[] Mean(Radical[] values, int period)
        {
            var replacement = fast ? ComponentAverage.Take(values.Select(v => v.Publish()).ToArray(), period) : null;
            if (replacement is not null) return Enumerable.Range(0, values.Length).Select(i => Radical.Of(i < replacement.Count ? Number.Of(replacement[i]) : default)).ToArray();
            var mean = new Average(kind, period); return values.Select(v => mean.Next(v, true)).ToArray();
        }
        var means = Mean(prices.Select(Radical.Of).ToArray(), lookback); var moments = new Moments(lookback);
        var scores = prices.Select((value, i) => Radical.QuotientRoot((value - means[i].Rational()).Times(100), moments.Next(value, true))).ToArray();
        var smoothed = Mean(scores, smooth); var weighted = new Radical[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var period = Period(smoothed[i], length, lookback); Number sum = default;
            for (var lag = 0; lag < period && lag <= i; lag++) sum += prices[i - lag].Times(period - lag);
            weighted[i] = Radical.Of(sum.Divide((long)period * (period + 1L) / 2));
        }
        var lines = Mean(weighted, smooth); var result = new double[prices.Length]; var trades = new Signal[prices.Length]; Number previous = default;
        for (var i = 0; i < prices.Length; i++)
        { var value = lines[i].Rational(); var margin = prices[i] - value; result[i] = value.Publish(); trades[i] = Trade(margin, previous); previous = margin; }
        return (result, trades);
    }
    internal void Reset() { _mean.Reset(); _scores.Reset(); _output.Reset(); _moments.Reset(); _history.Clear(); _previousMargin = default; }
    public void Dispose() => Reset();
    private sealed class Moments
    {
        private readonly int _length;
        private readonly Queue<Number> _values = new();
        private Number _sum, _squares;
        internal Moments(int length) => _length = Math.Max(1, length);
        internal Number Next(Number value, bool final)
        {
            var full = _values.Count == _length; var expired = full ? _values.Peek() : default;
            var count = full ? _length : _values.Count + 1;
            var sum = _sum + value - expired; var squares = _squares + value * value - expired * expired;
            var variance = (squares.Times(count) - sum * sum).Divide((long)count * count);
            if (final) { if (full) _values.Dequeue(); _values.Enqueue(value); _sum = sum; _squares = squares; }
            return variance;
        }
        internal void Reset() { _values.Clear(); _sum = _squares = default; }
    }
    private sealed class Average
    {
        private readonly MovingAvgType _kind;
        private readonly int _length;
        private readonly Queue<Radical> _history = new();
        private Radical _sum = Radical.Zero, _weighted = Radical.Zero, _previous = Radical.Zero;
        private long _count;
        internal Average(MovingAvgType kind, int length)
        {
            if (!StrengthWindow.Supports(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            _kind = kind; _length = Math.Max(1, length);
        }
        internal Radical Next(Radical value, bool final)
        {
            if (_length == 1) return value;
            var sum = _sum; var weighted = _weighted; Radical result;
            var finite = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (finite)
            {
                if (_kind == MovingAvgType.WeightedMovingAverage) weighted = weighted - sum + value.Times(Number.Integer(_length));
                if (_history.Count == _length) sum -= _history.Peek();
                sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage
                    ? weighted.Times(Number.Integer(1).Divide((long)_length * (_length + 1L) / 2))
                    : _count + 1 < _length ? Radical.Zero : sum.Times(Number.Integer(1).Divide(_length));
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = sum.Times(Number.Integer(1).Divide(_count + 1)); }
            else
            {
                var ema = _kind == MovingAvgType.ExponentialMovingAverage;
                result = (_previous.Times(Number.Integer(_length - 1L)) + value.Times(Number.Integer(ema ? 2 : 1)))
                    .Times(Number.Integer(1).Divide(ema ? _length + 1L : _length));
            }
            if (final)
            {
                if (finite) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); }
                _sum = sum; _weighted = weighted; _previous = result; _count++;
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = Radical.Zero; _count = 0; }
    }
    // A canonical rational linear combination of square-root classes. Distinct
    // classes are linearly independent, so cancellation is decided before refinement.
    internal sealed class Radical
    {
        private readonly Dictionary<BigInteger, Number> _terms;
        internal static readonly Radical Zero = new(new());
        private Radical(Dictionary<BigInteger, Number> terms) => _terms = terms;
        internal static Radical Of(Number value) => value.Sign == 0 ? Zero : new(new() { [BigInteger.One] = value });
        internal static Radical QuotientRoot(Number numerator, Number square)
        {
            if (numerator.Sign == 0 || square.Sign == 0) return Zero;
            if (square.Sign < 0) throw new ArgumentOutOfRangeException(nameof(square));
            var kernel = square.Numerator * square.Denominator; var coefficient = numerator.Divide(Number.Integer(square.Numerator));
            foreach (var prime in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
                while (kernel % (prime * prime) == 0) { kernel /= prime * prime; coefficient = coefficient.Times(prime); }
            var root = ExactPopulationDeviation.IntegerRoot(kernel);
            if (root * root == kernel) { kernel = BigInteger.One; coefficient *= Number.Integer(root); }
            return new(new() { [kernel] = coefficient });
        }
        internal Number Rational()
        {
            if (_terms.Count == 0) return default;
            if (_terms.Count == 1 && _terms.TryGetValue(BigInteger.One, out var value)) return value;
            throw new InvalidOperationException("Expected a rational price average.");
        }
        private static void Add(Dictionary<BigInteger, Number> terms, BigInteger kernel, Number coefficient)
        {
            if (coefficient.Sign == 0) return;
            if (terms.TryGetValue(kernel, out var current))
            { var sum = current + coefficient; if (sum.Sign == 0) terms.Remove(kernel); else terms[kernel] = sum; return; }
            foreach (var term in terms)
            {
                var gcd = BigInteger.GreatestCommonDivisor(kernel, term.Key); var a = kernel / gcd; var b = term.Key / gcd;
                var ar = ExactPopulationDeviation.IntegerRoot(a); var br = ExactPopulationDeviation.IntegerRoot(b);
                if (ar * ar != a || br * br != b) continue;
                var sum = term.Value + coefficient * Number.Integer(ar).Divide(Number.Integer(br));
                if (sum.Sign == 0) terms.Remove(term.Key); else terms[term.Key] = sum; return;
            }
            terms.Add(kernel, coefficient);
        }
        public static Radical operator +(Radical a, Radical b)
        {
            if (a._terms.Count == 0) return b; if (b._terms.Count == 0) return a;
            var terms = new Dictionary<BigInteger, Number>(a._terms); foreach (var term in b._terms) Add(terms, term.Key, term.Value); return new(terms);
        }
        public static Radical operator -(Radical a, Radical b) => a + b.Times(Number.Integer(-1));
        internal Radical Times(Number factor)
        {
            if (factor.Sign == 0) return Zero;
            return new(_terms.ToDictionary(term => term.Key, term => term.Value * factor));
        }
        private (BigInteger Low, BigInteger High) Bounds(int precision)
        {
            BigInteger low = 0, high = 0;
            foreach (var term in _terms)
            {
                var value = term.Value; var square = (value.Numerator * value.Numerator * term.Key) << checked(2 * precision);
                var denominator = value.Denominator * value.Denominator;
                var quotient = square / denominator;
                var lower = quotient.IsZero ? BigInteger.Zero : ExactPopulationDeviation.IntegerRoot(quotient);
                var upper = lower * lower * denominator == square ? lower : lower + 1;
                if (value.Sign > 0) { low += lower; high += upper; } else { low -= upper; high -= lower; }
            }
            return (low, high);
        }
        internal int Compare(Number threshold)
        {
            var difference = this - Of(threshold); if (difference._terms.Count == 0) return 0;
            if (difference._terms.Count == 1) return difference._terms.First().Value.Sign;
            for (var precision = 64; ; precision = checked(precision * 2))
            { var (low, high) = difference.Bounds(precision); if (low.Sign > 0) return 1; if (high.Sign < 0) return -1; }
        }
        internal double Publish()
        {
            if (_terms.Count == 0 || _terms.Count == 1 && _terms.ContainsKey(BigInteger.One)) return Rational().Publish();
            for (var precision = 64; ; precision = checked(precision * 2))
            {
                var (low, high) = Bounds(precision); var denominator = Number.Integer(BigInteger.One << precision);
                var a = Number.Integer(low).Divide(denominator).Publish(); var b = Number.Integer(high).Divide(denominator).Publish();
#pragma warning disable S1244 // Exact equality certifies that both interval endpoints round to the same binary64 value; an epsilon cannot certify this.
                if (a == b) return a;
#pragma warning restore S1244
            }
        }
    }
}
