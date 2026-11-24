using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// Bounded outward arithmetic shared only by the approved RMO/QQE smoother paths.
internal static class CertifiedRmoQqe
{
    private static readonly Number Padding = Number.Of(Math.Pow(2, -78));
    private static Number Abs(Number n) => n.Sign < 0 ? default(Number) - n : n;
    private static Number Upper(Number n) { var rounded = n.Round(80); return rounded + rounded * Padding; }
    internal readonly struct Ball
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

    internal struct Average
    {
        private Ball _sum, _previous;
        private long _count;
        internal Ball Next(Ball value, MovingAvgType kind, long length)
        {
            Ball result;
            if (length == 1) result = value;
            else if (kind == MovingAvgType.ExponentialMovingAverage && _count < length)
            { _sum += value; result = _sum.Divide(_count + 1); }
            else
            {
                var ema = kind == MovingAvgType.ExponentialMovingAverage;
                result = (_previous.Times(length - 1L) + value.Times(ema ? 2 : 1)).Divide(ema ? length + 1L : length);
            }
            _previous = result; _count++; return result;
        }
    }

    internal sealed class Rmo(int swing, int output)
    {
        private Average _second, _third, _rmo;
        private Ball _previous;
        internal bool TryNext(Number first, bool final, out (double Rmo, double Swing1, double Swing2, double Swing3, Signal Trade) result)
        {
            result = default;
            var secondState = _second; var thirdState = _third; var rmoState = _rmo;
            var input = Ball.Exact(first);
            var second = secondState.Next(input, MovingAvgType.ExponentialMovingAverage, swing);
            var third = thirdState.Next(second, MovingAvgType.ExponentialMovingAverage, swing);
            var rmo = rmoState.Next(input, MovingAvgType.ExponentialMovingAverage, output);
            if (!second.TryPublish(out var two) || !third.TryPublish(out var three) || !rmo.TryPublish(out var value)) return false;
            if (!rmo.TrySign(out var direction) || !(rmo - _previous).TrySign(out var change)) return false;
            var trade = direction > 0 && change > 0 ? Signal.StrongBuy : direction < 0 && change < 0 ? Signal.StrongSell
                : direction > 0 ? Signal.Buy : direction < 0 ? Signal.Sell : Signal.None;
            var one = first.Publish();
            if (final) { _second = secondState; _third = thirdState; _rmo = rmoState; _previous = rmo; }
            result = (value, one, two, three, trade); return true;
        }
    }
    internal sealed class Qqe(MovingAvgType kind, int smooth, long widthPeriod, double fast, double slow)
    {
        private Average _signal, _first, _second;
        private Ball _previousSignal, _bull, _bear;
        internal bool TryNext(double rsi, bool final, out (double Fast, double Slow, Signal Trade) result)
        {
            result = default;
            var signalState = _signal; var firstState = _first; var secondState = _second;
            var signal = signalState.Next(Ball.Exact(Number.Of(rsi)), kind, smooth);
            var movement = (signal - _previousSignal).Absolute();
            var width = secondState.Next(firstState.Next(movement, kind, widthPeriod), kind, widthPeriod);
            if (!width.TrySign(out var widthSign) || widthSign < 0) return false;
            var first = width * Ball.Exact(Number.Of(fast)); var second = width * Ball.Exact(Number.Of(slow));
            // The width is nonnegative, so factor order exactly determines band order.
            var bull = width * Ball.Exact(Number.Of(1) - Number.Of(Math.Max(fast, slow)));
            var bear = width * Ball.Exact(Number.Of(1) - Number.Of(Math.Min(fast, slow)));
            if (!first.TryPublish(out var a) || !second.TryPublish(out var b)) return false;
            if (!bull.TrySign(out var bullSign) || !bear.TrySign(out var bearSign)) return false;
            var trade = Signal.None;
            if (bullSign > 0)
            {
                if (!(bull - _bull).TrySign(out var change)) return false;
                trade = change > 0 ? Signal.StrongBuy : Signal.Buy;
            }
            else if (bearSign < 0)
            {
                if (!(bear - _bear).TrySign(out var change)) return false;
                trade = change < 0 ? Signal.StrongSell : Signal.Sell;
            }
            if (final) { _signal = signalState; _first = firstState; _second = secondState; _previousSignal = signal; _bull = bull; _bear = bear; }
            result = (a, b, trade); return true;
        }
    }
}
