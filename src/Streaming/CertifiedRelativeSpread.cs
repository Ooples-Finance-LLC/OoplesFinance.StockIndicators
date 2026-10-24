using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Streaming;

// Positive smoothing coefficients propagate enclosures without the unstable
// absolute transition matrices required by higher-order recursive filters.
internal sealed class CertifiedRelativeSpread
{
    private const int Precision = 160;
    private static readonly Number Padding = Number.Of(Math.Pow(2, -78)).Round(1);
    private readonly MovingAvgType _kind;
    private readonly int _fastLength, _slowLength, _period, _smoothLength;
    private AverageState _fast, _slow, _signal;
    private Ball _spread, _gain, _loss;
    private long _count;
    private double _rsi, _firstPrice;
    private bool _constant = true;

    internal CertifiedRelativeSpread(MovingAvgType kind, int fast, int slow, int period, int smooth)
    {
        if (!Supports(kind)) throw new ArgumentException("Only convex recursive averages are certified.", nameof(kind));
        _kind = kind; _fastLength = Math.Max(1, fast); _slowLength = Math.Max(1, slow);
        _period = Math.Max(1, period); _smoothLength = Math.Max(1, smooth);
    }
    internal static bool Supports(MovingAvgType kind) => kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod;
    private static Number Abs(Number value) => value.Sign < 0 ? default(Number) - value : value;
    private static Number Nonnegative(Number value) => value.Sign < 0 ? default : value;
    // Round-to-nearest at 80 bits has relative error below 2^-80. The larger
    // positive padding makes this an upper bound, without exponent underflow.
    private static Number Upper(Number value)
    { var rounded = value.Round(80); return rounded + rounded * Padding; }

    private readonly struct Ball
    {
        internal readonly Number Center, Radius;
        private Ball(Number center, Number radius) { Center = center; Radius = radius; }
        private static Ball Rounded(Number center, Number radius)
        {
            var rounded = center.Round(Precision);
            return new(rounded, Upper(radius + Abs(center - rounded)));
        }
        internal static Ball Exact(double value) => Rounded(Number.Of(value), default);
        internal bool IsZero => Center.Sign == 0 && Radius.Sign == 0;
        internal Number Lower => Center - Radius;
        internal Number Higher => Center + Radius;
        public static Ball operator +(Ball a, Ball b) => Rounded(a.Center + b.Center, a.Radius + b.Radius);
        public static Ball operator -(Ball a, Ball b) => Rounded(a.Center - b.Center, a.Radius + b.Radius);
        internal Ball Times(long value) => Rounded(Center.Times(value), Radius.Times(value));
        internal Ball Divide(long value) => Rounded(Center.Divide(value), Radius.Divide(value));
        internal bool TrySign(out int sign)
        {
            sign = Center.Sign;
            return Radius.Sign == 0 || (Abs(Center) - Radius).Sign > 0;
        }
        internal bool TryPublish(out double value)
        {
            value = Lower.Publish();
            return BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(Higher.Publish());
        }
    }
    private struct AverageState { internal Ball Sum, Previous; }
    private Ball Average(ref AverageState state, Ball value, int length)
    {
        if (length == 1) return value;
        Ball result;
        if (_kind == MovingAvgType.ExponentialMovingAverage && _count < length)
        { state.Sum += value; result = state.Sum.Divide(_count + 1); }
        else
        {
            var ema = _kind == MovingAvgType.ExponentialMovingAverage;
            result = (state.Previous.Times(length - 1L) + value.Times(ema ? 2 : 1)).Divide(ema ? length + 1L : length);
        }
        state.Previous = result;
        return result;
    }

    internal bool TryNext(double price, bool final, out double value)
    {
        value = 0;
        var fast = _fast; var slow = _slow; var signal = _signal;
        var input = Ball.Exact(price);
        var fastValue = Average(ref fast, input, _fastLength);
        var slowValue = Average(ref slow, input, _slowLength);
        var constant = _count == 0 || _constant && price == _firstPrice; // NOSONAR: S1244 - Exact constant input certifies correlated rounding; a tolerance would accept changing prices.
        // These averages are identically equal, including correlated rounding
        // uncertainty. Subtracting independent balls would lose that fact.
        var equal = _fastLength == _slowLength || _kind == MovingAvgType.ExponentialMovingAverage &&
            (_count < Math.Min(_fastLength, _slowLength) || constant);
        var spread = equal ? default : fastValue - slowValue;
        var change = _count == 0 ? default : spread - _spread;
        if (!change.TrySign(out var direction)) return false;
        var gain = (_gain.Times(_period - 1L) + (direction > 0 ? change : default)).Divide(_period);
        var loss = (_loss.Times(_period - 1L) + (direction < 0 ? default(Ball) - change : default)).Divide(_period);
        double rsi;
        if (direction == 0 && _count != 0 && _period > 1) rsi = _rsi;
        else if (loss.IsZero) rsi = 100;
        else
        {
            // g/(g+l) increases with g and decreases with l. Both true
            // moments are nonnegative, so intersect their enclosures with [0,+inf).
            var gl = Nonnegative(gain.Lower); var gh = Nonnegative(gain.Higher);
            var ll = Nonnegative(loss.Lower); var lh = Nonnegative(loss.Higher);
            var lowerDenominator = gl + lh; var upperDenominator = gh + ll;
            if (lowerDenominator.Sign <= 0 || upperDenominator.Sign <= 0) return false;
            var lower = gl.Times(100).Divide(lowerDenominator).Publish();
            var upper = gh.Times(100).Divide(upperDenominator).Publish();
            if (BitConverter.DoubleToInt64Bits(lower) != BitConverter.DoubleToInt64Bits(upper)) return false;
            rsi = lower;
        }
        // The original signal recurrence consumes the rounded binary64 RSI,
        // rather than its unpublished rational value.
        if (!Average(ref signal, Ball.Exact(rsi), _smoothLength).TryPublish(out value)) return false;
        if (final)
        {
            _fast = fast; _slow = slow; _signal = signal; _spread = spread; _gain = gain; _loss = loss;
            _rsi = rsi; _constant = constant; if (_count == 0) _firstPrice = price; _count++;
        }
        return true;
    }
}
