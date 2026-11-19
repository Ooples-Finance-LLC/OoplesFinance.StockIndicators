using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// Correlation direction stays exact in the owner. Only cumulative rational
// arithmetic is enclosed; all eight outputs and trade signs must certify.
internal sealed class CertifiedPeriodicChannel
{
    private static readonly Number Padding = Number.Of(Math.Pow(2, -78));
    private Ball _prices, _sines, _sineErrors, _priceErrors, _lineErrors, _previousMargin;
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
        internal Ball Absolute()
        {
            var low = Lower; var high = Higher;
            if (low.Sign >= 0) return this;
            if (high.Sign <= 0) return Bounds(default(Number) - high, default(Number) - low);
            return Bounds(default, (Abs(low) - high).Sign > 0 ? Abs(low) : high);
        }
        public static Ball operator *(Ball a, Ball b)
        {
            var corners = new[] { a.Lower * b.Lower, a.Lower * b.Higher, a.Higher * b.Lower, a.Higher * b.Higher };
            var low = corners[0]; var high = low;
            foreach (var corner in corners) { if ((corner - low).Sign < 0) low = corner; if ((corner - high).Sign > 0) high = corner; }
            return Bounds(low, high);
        }
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

    internal bool TryNext(double price, double sineValue, long count, bool final, out (double[] Values, Signal Trade) result)
    {
        result = default;
        var current = Ball.Exact(Number.Of(price)); var sine = Ball.Exact(Number.Of(sineValue));
        var prices = _prices + current; var sines = _sines + sine;
        var mean = count == 0 ? default : prices.Divide(count);
        var sineOffset = count == 0 ? default : sine - sines.Divide(count);
        var sineErrors = _sineErrors + sineOffset.Absolute();
        if (!sineErrors.TrySign(out var errorSign)) return false;
        Ball sineZ = default;
        if (errorSign != 0 && !sineOffset.Times(count).TryDivide(sineErrors, out sineZ)) return false;
        var priceErrors = _priceErrors + (current - mean).Absolute();
        var line = count == 0 ? default : mean + (Ball.Exact(Number.Integer(count > 1 ? 2 : 0)) + sineZ) * priceErrors.Divide(count);
        var margin = current - line; var lineErrors = _lineErrors + margin.Absolute();
        var width = count == 0 ? default : lineErrors.Divide(count);
        if (!margin.TrySign(out var direction) || !(margin - _previousMargin).TrySign(out var change)) return false;
        var trade = direction > 0 && change > 0 ? Signal.StrongBuy : direction < 0 && change < 0 ? Signal.StrongSell
            : direction > 0 ? Signal.Buy : direction < 0 ? Signal.Sell : Signal.None;
        var bands = new[] { line, width, line + width, line + width.Times(2), line + width.Times(3),
            line - width, line - width.Times(2), line - width.Times(3) };
        var values = new double[8];
        for (var i = 0; i < bands.Length; i++) if (!bands[i].TryPublish(out values[i])) return false;
        if (final)
        {
            _prices = prices; _sines = sines; _sineErrors = sineErrors; _priceErrors = priceErrors;
            _lineErrors = lineErrors; _previousMargin = margin;
        }
        result = (values, trade); return true;
    }
}
