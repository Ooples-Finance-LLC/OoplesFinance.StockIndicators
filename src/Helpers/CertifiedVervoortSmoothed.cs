using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
using Rational = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// Keep the finite rainbow cascade exact. Enclose only the recursive EMA stages
// and accept the radical band output when both exact endpoints round identically.
internal sealed class CertifiedVervoortSmoothed(int band, int smooth, double multiplier)
{
    private readonly int _band = Math.Max(1, band), _smooth = Math.Max(1, smooth);
    private readonly double _multiplier = multiplier;
    private static readonly Number Padding = Number.Of(Math.Pow(2, -78));
    private Average[] _averages = new Average[5];
    private readonly Queue<Ball> _window = new();
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
        internal Ball Divide(Number divisor) => Rounded(_center.Divide(divisor), _radius.Divide(Abs(divisor)));
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
    internal bool TryNext(Rational input, bool final, out double percent)
    {
        percent = 0;
        var exact = Number.Integer(input.Numerator).Divide(Number.Integer(input.Denominator));
        var rainbow = Ball.Bounds(exact, exact);
        var proposal = (Average[])_averages.Clone();
        var first = proposal[0].Next(rainbow, MovingAvgType.ExponentialMovingAverage, _smooth, true);
        var second = proposal[1].Next(first, MovingAvgType.ExponentialMovingAverage, _smooth, true);
        var dema = first.Times(2) - second;
        var a = proposal[2].Next(dema, MovingAvgType.ExponentialMovingAverage, _smooth, true);
        var b = proposal[3].Next(a, MovingAvgType.ExponentialMovingAverage, _smooth, true);
        var c = proposal[4].Next(b, MovingAvgType.ExponentialMovingAverage, _smooth, true);
        var filtered = a.Times(3) - b.Times(3) + c;
        // Recompute finite moments from bounded samples instead of accumulating
        // interval cancellation across an unbounded history.
        var values = (_window.Count == _band ? _window.Skip(1) : _window).Concat(new[] { filtered }).ToArray();
        Ball sum = default, squares = default, weighted = default, maximum = default;
        var weight = _band - values.Length + 1L;
        foreach (var value in values)
        { sum += value; squares += value.Square(); weighted += value.Times(weight++); maximum = Ball.Maximum(maximum, value.Absolute()); }
        if (values.Length == _band && _multiplier != 0)
        {
            var variance = (squares.Times(values.Length) - sum.Square()).Divide((long)values.Length * values.Length);
            var threshold = maximum * Ball.Of(1.4210854715202004e-14);
            var decision = variance - threshold.Square();
            if (decision.Low.Sign > 0)
            {
                if (variance.Low.Sign <= 0) return false;
                var center = weighted.Divide((long)_band * (_band + 1L) / 2);
                var numerator = (filtered - center).Times(50).Divide(Number.Of(_multiplier));
                var lo = numerator.Low; var hi = numerator.High;
                // x/sqrt(v) increases in x; its v monotonicity follows x's sign.
                try
                {
                    var lowerOutput = VervoortModifiedWindow.OffsetRoot(Exact(lo), Exact(lo.Sign < 0 ? variance.Low : variance.High));
                    var upperOutput = VervoortModifiedWindow.OffsetRoot(Exact(hi), Exact(hi.Sign < 0 ? variance.High : variance.Low));
                    if (BitConverter.DoubleToInt64Bits(lowerOutput) != BitConverter.DoubleToInt64Bits(upperOutput)) return false;
                    percent = lowerOutput;
                }
                catch (OverflowException) { return false; }
            }
            else if (decision.High.Sign > 0) return false;
        }
        if (final)
        {
            _averages = proposal;
            if (_window.Count == _band) _window.Dequeue(); _window.Enqueue(filtered);
        }
        return true;
    }
}
