using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Helpers;

// Bounded significands with unbounded exponents enclose the original rational
// recurrence. Neither a rounded output nor a trade is accepted without proof.
internal sealed class CertifiedVolatilityMomentum(MovingAvgType kind, int lag, int range)
{
    private static readonly Number Padding = Number.Of(Math.Pow(2, -78));
    private readonly Queue<double> _prices = new();
    private Average _atr, _signal;
    private Ball _previousMargin;
    private double _previous;
    private bool _started;
    internal static bool Supports(MovingAvgType kind) => kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod;
    private static Number Abs(Number n) => n.Sign < 0 ? default(Number) - n : n;
    private static Number Max(Number a, Number b) => (a - b).Sign >= 0 ? a : b;
    // Round(80) has relative error <= 2^-80, so this padding bounds upward.
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

    private struct Average
    {
        private Ball _sum, _previous;
        private long _count;
        internal Ball Next(Ball value, MovingAvgType kind, int length)
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

    internal bool TryNext(double high, double low, double close, bool final, bool selected,
        bool requireTrade, bool lineOnly, out (double Line, double Signal, Signal Trade) result)
    {
        result = default;
        var previous = _started ? _previous : close;
        if (selected && !CalculationsHelper.IsWithinBarRange(close, low, high))
        { high = Math.Max(close, previous); low = Math.Min(close, previous); }
        var h = Number.Of(high); var l = Number.Of(low); var p = Number.Of(previous);
        var tr = Max(h - l, Max(Abs(h - p), Abs(l - p)));
        // Proposal copies ensure previews and failed certification never commit.
        var atrState = _atr; var signalState = _signal;
        var atr = atrState.Next(Ball.Exact(tr), kind, range);
        var difference = _prices.Count == lag ? Number.Of(close) - Number.Of(_prices.Peek()) : default;
        Ball line = default;
        if (difference.Sign != 0)
        {
            if (atr.Lower.Sign <= 0) return false;
            var a = difference.Divide(atr.Lower); var b = difference.Divide(atr.Higher);
            line = difference.Sign > 0 ? Ball.Bounds(b, a) : Ball.Bounds(a, b);
        }
        if (!line.TryPublish(out var value)) return false;
        var signal = lineOnly ? default : signalState.Next(line, kind, lag);
        if (!signal.TryPublish(out var signalValue)) return false;
        // A period-one signal is identically the line; keep that correlation.
        var margin = lag == 1 ? default : line - signal;
        var trade = Signal.None;
        if (requireTrade)
        {
            if (!margin.TrySign(out var direction) || !(margin - _previousMargin).TrySign(out var change)) return false;
            trade = direction > 0 && change > 0 ? Signal.StrongBuy : direction < 0 && change < 0 ? Signal.StrongSell
                : direction > 0 ? Signal.Buy : direction < 0 ? Signal.Sell : Signal.None;
        }
        if (final)
        {
            _atr = atrState; _signal = signalState; _previousMargin = margin;
            if (_prices.Count == lag) _prices.Dequeue();
            _prices.Enqueue(close); _previous = close; _started = true;
        }
        result = (value, signalValue, trade); return true;
    }
}
