using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
using Rational = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;

namespace OoplesFinance.StockIndicators.Helpers;

// Enclose the recursive candle and smoothing stages. Only a proved binary64
// PercentB crosses into the existing exact band and trade calculation.
internal sealed class CertifiedVervoortModified
{
    private static readonly Number Padding = Number.Of(Math.Pow(2, -78));
    private readonly Stage _first, _second, _third;
    private readonly int _band;
    private readonly Queue<Ball> _window = new();
    private Ball _open;
    private double _previous;
    internal CertifiedVervoortModified(MovingAvgType kind, int band, int smooth)
    { _first = new(kind, smooth); _second = new(kind, smooth); _third = new(kind, smooth); _band = Math.Max(1, band); }
    internal static bool Supports(MovingAvgType kind) => kind is MovingAvgType.ExponentialMovingAverage
        or MovingAvgType.WildersSmoothingMethod or MovingAvgType.DoubleExponentialMovingAverage or MovingAvgType.TripleExponentialMovingAverage;
    private static Number Abs(Number value) => value.Sign < 0 ? default(Number) - value : value;
    private static Number Max(Number a, Number b) => (a - b).Sign >= 0 ? a : b;
    private static Number Upper(Number value) { var rounded = value.Round(80); return rounded + rounded * Padding; }
    private static Rational Exact(Number value)
    { var fraction = value.ToFraction(); return Rational.Integer(fraction.Numerator).Divide(Rational.Integer(fraction.Denominator)); }

    private readonly struct Ball
    {
        private readonly Number _center, _radius;
        private Ball(Number center, Number radius) { _center = center; _radius = radius; }
        internal Number Low => _center - _radius;
        internal Number High => _center + _radius;
        private static Ball Rounded(Number center, Number radius)
        { var rounded = center.Round(160); return new(rounded, Upper(radius + Abs(center - rounded))); }
        internal static Ball Of(double value) => new(Number.Of(value), default);
        internal static Ball Bounds(Number low, Number high) => Rounded((low + high).Divide(2), (high - low).Divide(2));
        public static Ball operator +(Ball a, Ball b) => Rounded(a._center + b._center, a._radius + b._radius);
        public static Ball operator -(Ball a, Ball b) => Rounded(a._center - b._center, a._radius + b._radius);
        public static Ball operator *(Ball a, Ball b) => Rounded(a._center * b._center,
            Abs(a._center) * b._radius + Abs(b._center) * a._radius + a._radius * b._radius);
        internal Ball Times(long value) => Rounded(_center.Times(value), _radius.Times(Math.Abs(value)));
        internal Ball Divide(long value) => Rounded(_center.Divide(value), _radius.Divide(value));
        internal Ball Square()
        {
            var low = Low; var high = High;
            if (low.Sign >= 0) return Bounds(low * low, high * high);
            if (high.Sign <= 0) return Bounds(high * high, low * low);
            return Bounds(default, Max(low * low, high * high));
        }
        internal Ball Absolute()
        {
            if (Low.Sign >= 0) return this;
            if (High.Sign <= 0) return Times(-1);
            return Bounds(default, Max(Abs(Low), Abs(High)));
        }
        internal static Ball Maximum(Ball a, Ball b) => Bounds(Max(a.Low, b.Low), Max(a.High, b.High));
        internal static Ball Minimum(Ball a, Ball b) => Maximum(a.Times(-1), b.Times(-1)).Times(-1);
    }
    private struct Average
    {
        private Ball _sum, _previous;
        private long _count;
        internal Ball Next(Ball value, MovingAvgType kind, int length, bool final)
        {
            var sum = _sum;
            Ball result;
            if (length == 1) result = value;
            else if (kind == MovingAvgType.ExponentialMovingAverage && _count < length)
            { sum += value; result = sum.Divide(_count + 1); }
            else
            { var ema = kind == MovingAvgType.ExponentialMovingAverage; result = (_previous.Times(length - 1L) + value.Times(ema ? 2 : 1)).Divide(ema ? length + 1L : length); }
            if (final) { _sum = sum; _previous = result; _count++; }
            return result;
        }
    }
    private sealed class Stage(MovingAvgType kind, int period)
    {
        private Average _first, _second, _third;
        internal Ball Next(Ball value, bool final)
        {
            var composite = kind is MovingAvgType.DoubleExponentialMovingAverage or MovingAvgType.TripleExponentialMovingAverage;
            var first = _first.Next(value, composite ? MovingAvgType.ExponentialMovingAverage : kind, Math.Max(1, period), final);
            if (!composite) return first;
            var second = _second.Next(first, MovingAvgType.ExponentialMovingAverage, Math.Max(1, period), final);
            return kind == MovingAvgType.DoubleExponentialMovingAverage ? first.Times(2) - second
                : first.Times(3) - second.Times(3) + _third.Next(second, MovingAvgType.ExponentialMovingAverage, Math.Max(1, period), final);
        }
    }
    internal bool TryNext(double input, double high, double low, bool final, out double percent)
    {
        percent = 0;
        var open = (Ball.Of(_previous) + _open).Divide(2);
        var upper = Ball.Maximum(Ball.Of(high), open); var lower = Ball.Minimum(Ball.Of(low), open);
        var close = (Ball.Of(input) + open + upper + lower).Divide(4);
        var first = _first.Next(close, final); var second = _second.Next(first, final);
        var filtered = _third.Next(first.Times(2) - second, final);
        // Recompute finite moments from bounded samples instead of accumulating
        // interval cancellation across an unbounded history.
        var values = (_window.Count == _band ? _window.Skip(1) : _window).Concat(new[] { filtered }).ToArray();
        Ball sum = default, squares = default, weighted = default, maximum = default;
        var weight = _band - values.Length + 1L;
        foreach (var value in values)
        { sum += value; squares += value.Square(); weighted += value.Times(weight++); maximum = Ball.Maximum(maximum, value.Absolute()); }
        if (values.Length == _band)
        {
            var variance = (squares.Times(values.Length) - sum.Square()).Divide((long)values.Length * values.Length);
            var threshold = maximum * Ball.Of(1.4210854715202004e-14);
            var decision = variance - threshold.Square();
            if (decision.Low.Sign > 0)
            {
                if (variance.Low.Sign <= 0) return false;
                var center = weighted.Divide((long)_band * (_band + 1L) / 2);
                var numerator = (filtered - center).Times(25);
                var lo = numerator.Low; var hi = numerator.High;
                // x/sqrt(v) increases in x; its v monotonicity follows x's sign.
                try
                {
                    var a = VervoortModifiedWindow.OffsetRoot(Exact(lo), Exact(lo.Sign < 0 ? variance.Low : variance.High));
                    var b = VervoortModifiedWindow.OffsetRoot(Exact(hi), Exact(hi.Sign < 0 ? variance.High : variance.Low));
                    if (BitConverter.DoubleToInt64Bits(a) != BitConverter.DoubleToInt64Bits(b)) return false;
                    percent = a;
                }
                catch (OverflowException) { return false; }
            }
            else if (decision.High.Sign > 0) return false;
        }
        if (final)
        { if (_window.Count == _band) _window.Dequeue(); _window.Enqueue(filtered); _open = open; _previous = input; }
        return true;
    }
}
