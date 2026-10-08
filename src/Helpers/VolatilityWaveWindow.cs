using OoplesFinance.StockIndicators.Builder.Compute;
using Fraction = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
using Bounds = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Bounds;
namespace OoplesFinance.StockIndicators.Helpers;

// Price windows grow with observations. Irrational power means remain bounded
// expressions through both smoothing stages, including the final extrapolation.
internal sealed class VolatilityWaveWindow : IDisposable
{
    private readonly int _length;
    private readonly Fraction _factor;
    private readonly Queue<Fraction> _history = new();
    private Fraction _sum, _squares;
    private readonly Average _first, _second;
    internal VolatilityWaveWindow(MovingAvgType kind, int length, double factor)
    {
        _factor = Fraction.Of(factor); _length = Math.Max(1, length);
        var smooth = MathHelper.MinOrMax((int)Math.Ceiling(Math.Sqrt(_length)));
        _first = new(kind, smooth); _second = new(kind, smooth);
    }
    internal Expression Weighted(double price, bool final)
    {
        var value = Fraction.Of(price); var full = _history.Count == _length;
        var expired = full ? _history.Peek() : (Fraction)0;
        var count = full ? _length : _history.Count + 1;
        var sum = _sum + value - expired;
        var squares = _squares + value * value - expired * expired;
        var variance = (squares * count - sum * sum) / ((Fraction)count * count);
        // p^4 = 10000*kf^4*variance/price^2; compare clamps before roots.
        Fraction fourth = value.Sign > 0 && _factor.Sign > 0 ? 10000 * _factor.Pow(4) * variance / (value * value) : 0;
        if (fourth < 1) fourth = 1; else if (fourth > 256) fourth = 256;
        var prices = new List<Fraction> { value };
        var history = _history.ToArray();
        for (var i = history.Length - 1; i >= 0 && prices.Count < _length; i--) prices.Add(history[i]);
        var point = new PowerMean(prices, _length, fourth); var result = Expression.Of(point);
        if (final)
        {
            if (full) _history.Dequeue(); _history.Enqueue(value); _sum = sum; _squares = squares;
        }
        return result;
    }
    internal double Next(double price, bool final)
    {
        var weighted = Weighted(price, final); var first = _first.Next(weighted, final);
        return (first.Scale(2) - _second.Next(first, final)).Publish();
    }
    internal void Reset() { _history.Clear(); _sum = _squares = 0; _first.Reset(); _second.Reset(); }
    public void Dispose() => Reset();

    internal static (double[] Values, Signal[] Signals) Calculate(StockData data, MovingAvgType kind, int length, double factor, bool fast = false)
    {
        _ = Fraction.Of(factor);
        foreach (var series in new[] { data.OpenPrices, data.HighPrices, data.LowPrices, data.ClosePrices, data.Volumes, data.ChainedValues })
            foreach (var value in series) _ = Fraction.Of(value);
        var prices = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        length = Math.Max(1, length); var period = MathHelper.MinOrMax((int)Math.Ceiling(Math.Sqrt(length)));
        using var window = new VolatilityWaveWindow(kind, length, factor);
        var weighted = prices.Select(p => window.Weighted(p, true)).ToArray();
        Expression[] Mean(Expression[] values)
        {
            var replacement = fast ? ComponentAverage.Take(ComponentAverage.HasOverrides ? values.Select(v => v.Publish()).ToArray() : Array.Empty<double>(), period) : null;
            if (replacement is not null) return Enumerable.Range(0, values.Length).Select(i => Expression.Of(i < replacement.Count ? Fraction.Of(replacement[i]) : 0)).ToArray();
            var mean = new Average(kind, period); return values.Select(v => mean.Next(v, true)).ToArray();
        }
        var first = Mean(weighted); var second = Mean(first);
        var result = new double[prices.Count]; var signals = new Signal[prices.Count]; var previous = Expression.Of(0);
        for (var i = 0; i < result.Length; i++)
        {
            var line = first[i].Scale(2) - second[i]; result[i] = line.Publish();
            // Finite signals compare the published line. If that line overflows,
            // retain its extended expression instead of importing infinity.
            var margin = Expression.Of(Fraction.Of(prices[i])) - (double.IsInfinity(result[i]) ? line : Expression.Of(Fraction.Of(result[i])));
            var direction = margin.Sign; var movement = (margin - previous).Sign;
            signals[i] = direction > 0 && movement > 0 ? Signal.StrongBuy : direction < 0 && movement < 0 ? Signal.StrongSell
                : direction > 0 ? Signal.Buy : direction < 0 ? Signal.Sell : Signal.None;
            previous = margin;
        }
        return (result, signals);
    }
    internal sealed class PowerMean
    {
        private readonly IReadOnlyList<Fraction> _prices;
        private readonly int _length;
        private readonly Fraction _fourth;
        private int _bits;
        private Bounds _bounds;
        internal PowerMean(IReadOnlyList<Fraction> prices, int length, Fraction fourth) { _prices = prices; _length = length; _fourth = fourth; }
        internal Bounds Evaluate(int bits)
        {
            if (_bits == bits) return _bounds;
            var square = UltimatePowerWeights.Root(_fourth, bits + 16);
            var low = UltimatePowerWeights.Root(square.Lower, bits + 16).Lower;
            var high = UltimatePowerWeights.Root(square.Upper, bits + 16).Upper;
            Bounds result;
            if (low.CompareTo(high) == 0) result = UltimatePowerWeights.Mean(_prices, _length, low, bits);
            else
            {
                // Every normalized base is in (0,1], hence decreases with p.
                var denominatorLow = UltimatePowerWeights.Sum(_length, high, bits).Lower;
                if (denominatorLow < 1) denominatorLow = 1;
                var denominatorHigh = UltimatePowerWeights.Sum(_length, low, bits).Upper;
                Fraction numeratorLow = 0, numeratorHigh = 0;
                for (var i = 0; i < _prices.Count && i < _length; i++)
                {
                    var price = _prices[i]; if (price.Sign == 0) continue;
                    var basis = (Fraction)(_length - i) / _length;
                    var a = UltimatePowerWeights.Power(basis, high, bits + 16).Lower;
                    var b = UltimatePowerWeights.Power(basis, low, bits + 16).Upper;
                    numeratorLow += price * (price.Sign > 0 ? a : b); numeratorHigh += price * (price.Sign > 0 ? b : a);
                }
                result = new(numeratorLow / (numeratorLow.Sign >= 0 ? denominatorHigh : denominatorLow),
                    numeratorHigh / (numeratorHigh.Sign >= 0 ? denominatorLow : denominatorHigh));
            }
            _bits = bits; return _bounds = result;
        }
    }
    internal sealed class Expression
    {
        private readonly Fraction _constant;
        private readonly Dictionary<PowerMean, Fraction> _terms;
        private Expression(Fraction constant, Dictionary<PowerMean, Fraction> terms) { _constant = constant; _terms = terms; }
        internal static Expression Of(Fraction value) => new(value, new());
        internal static Expression Of(PowerMean point)
        {
            var bounds = point.Evaluate(96);
            return bounds.Lower.CompareTo(bounds.Upper) == 0 ? Of(bounds.Lower) : new(0, new() { [point] = 1 });
        }
        internal Expression Scale(Fraction factor) => factor.Sign == 0 ? Of(0)
            : new(_constant * factor, _terms.ToDictionary(p => p.Key, p => p.Value * factor));
        public static Expression operator +(Expression a, Expression b)
        {
            var terms = new Dictionary<PowerMean, Fraction>(a._terms);
            foreach (var p in b._terms)
            {
                var value = terms.TryGetValue(p.Key, out var prior) ? prior + p.Value : p.Value;
                if (value.Sign == 0) terms.Remove(p.Key); else terms[p.Key] = value;
            }
            return new(a._constant + b._constant, terms);
        }
        public static Expression operator -(Expression a, Expression b) => a + b.Scale(-1);
        internal int Sign => _terms.Count == 0 ? _constant.Sign : Math.Sign(Publish());
        internal double Publish() => UltimatePowerWeights.PublishMean(bits =>
        {
            var lower = _constant; var upper = _constant;
            foreach (var p in _terms)
            {
                var bound = p.Key.Evaluate(bits); var factor = p.Value;
                lower += factor * (factor.Sign > 0 ? bound.Lower : bound.Upper);
                upper += factor * (factor.Sign > 0 ? bound.Upper : bound.Lower);
            }
            return new(lower, upper);
        });
    }
    private sealed class Average
    {
        private readonly MovingAvgType _kind;
        private readonly int _length;
        private readonly Queue<Expression> _history = new();
        private Expression _sum = Expression.Of(0), _weighted = Expression.Of(0), _previous = Expression.Of(0);
        private long _count;
        internal Average(MovingAvgType kind, int length)
        { if (!StrengthWindow.Supports(kind)) throw new ArgumentOutOfRangeException(nameof(kind)); _kind = kind; _length = length; }
        internal Expression Next(Expression value, bool final)
        {
            if (_length == 1) return value;
            var sum = _sum; var weighted = _weighted; Expression result;
            var finite = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (finite)
            {
                if (_kind == MovingAvgType.WeightedMovingAverage) weighted = weighted - sum + value.Scale(_length);
                if (_history.Count == _length) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Scale((Fraction)2 / ((Fraction)_length * (_length + 1L)))
                    : _count + 1 < _length ? Expression.Of(0) : sum.Scale((Fraction)1 / _length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { sum += value; result = sum.Scale((Fraction)1 / (_count + 1)); }
            else
            {
                var ema = _kind == MovingAvgType.ExponentialMovingAverage;
                result = (_previous.Scale(_length - 1L) + value.Scale(ema ? 2 : 1)).Scale((Fraction)1 / (ema ? _length + 1L : _length));
            }
            if (final)
            {
                if (finite) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); }
                _sum = sum; _weighted = weighted; _previous = result; _count++;
            }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = Expression.Of(0); _count = 0; }
    }
}
