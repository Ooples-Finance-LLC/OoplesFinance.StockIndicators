using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// Certify both clamps, published bits and slope/acceleration signs before commit.
internal sealed class CertifiedSwami
{
    private static readonly Number Padding = Number.Of(Math.Pow(2, -78));
    private Ball _numerator, _denominator, _previous, _previousSlope;
    private static Number Abs(Number n) => n.Sign < 0 ? default(Number) - n : n;
    private static Number Upper(Number n) { var rounded = n.Round(80); return rounded + rounded * Padding; }
    private readonly struct Ball
    {
        private readonly Number _center, _radius;
        private Ball(Number center, Number radius) { _center = center; _radius = radius; }
        internal Number Lower => _center - _radius;
        internal Number Higher => _center + _radius;
        private static Ball Rounded(Number center, Number radius)
        {
            var rounded = center.Round(160);
            return new(rounded, Upper(radius + Abs(center - rounded)));
        }
        internal static Ball Exact(Number value) => Rounded(value, default);
        internal static Ball Bounds(Number lower, Number higher) => Rounded((lower + higher).Divide(2), (higher - lower).Divide(2));
        public static Ball operator +(Ball a, Ball b) => Rounded(a._center + b._center, a._radius + b._radius);
        public static Ball operator -(Ball a, Ball b) => Rounded(a._center - b._center, a._radius + b._radius);
        internal Ball Times(long value) => Rounded(_center.Times(value), _radius.Times(value));
        internal Ball Divide(long value) => Rounded(_center.Divide(value), _radius.Divide(value));
        internal bool TryDivide(Ball divisor, out Ball result)
        {
            result = default;
            if (!divisor.TrySign(out var sign) || sign == 0) return false;
            var corners = new[] { Lower.Divide(divisor.Lower), Lower.Divide(divisor.Higher), Higher.Divide(divisor.Lower), Higher.Divide(divisor.Higher) };
            var lower = corners[0]; var higher = lower;
            foreach (var corner in corners) { if ((corner - lower).Sign < 0) lower = corner; if ((corner - higher).Sign > 0) higher = corner; }
            result = Bounds(lower, higher); return true;
        }
        internal bool TrySign(out int sign)
        { sign = _center.Sign; return _radius.Sign == 0 || (Abs(_center) - _radius).Sign > 0; }
        internal bool TryPublish(out double value)
        {
            value = 0;
            try
            {
                value = Lower.Publish();
                return BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(Higher.Publish());
            }
            catch (OverflowException) { return false; }
        }
    }

    internal bool TryNext(double price, double highest, double lowest, bool final, out (double Line, Signal Trade) result)
    {
        result = default;
        var numerator = (Ball.Exact(Number.Of(price) - Number.Of(lowest)) + _numerator).Divide(2);
        var denominator = (Ball.Exact(Number.Of(highest) - Number.Of(lowest)) + _denominator).Divide(2);
        if (!denominator.TrySign(out var denominatorSign)) return false;
        Ball line = default;
        if (denominatorSign != 0)
        {
            if (!numerator.TryDivide(denominator, out var ratio)) return false;
            line = (ratio + _previous.Times(4)).Divide(5);
        }
        if (!line.TrySign(out var lineSign)) return false;
        if (lineSign < 0) line = default;
        var one = Ball.Exact(Number.Of(1));
        if (!(line - one).TrySign(out var upperSign)) return false;
        if (upperSign > 0) line = one;
        if (!line.TryPublish(out var value)) return false;
        var slope = line - _previous;
        if (!slope.TrySign(out var direction) || !(slope - _previousSlope).TrySign(out var change)) return false;
        var trade = direction > 0 && change > 0 ? Signal.StrongBuy : direction < 0 && change < 0 ? Signal.StrongSell
            : direction > 0 ? Signal.Buy : direction < 0 ? Signal.Sell : Signal.None;
        if (final) { _numerator = numerator; _denominator = denominator; _previous = line; _previousSlope = slope; }
        result = (value, trade); return true;
    }
}
