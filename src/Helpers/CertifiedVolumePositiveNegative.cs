using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

// Outward enclosures certify the exact smoother outputs and vote/trade decisions.
// A failed proposal leaves all state untouched for exact replay by the caller.
internal sealed class CertifiedVolumePositiveNegative(MovingAvgType kind, int length, int smooth)
{
    private static readonly Number Padding = Number.Of(Math.Pow(2, -78));
    private readonly Queue<Number> _votes = new();
    private Average _atr, _volume, _signal;
    private Ball _previousSignal;
    private Number _sum, _previousPrice;
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

    internal bool TryNext(double high, double low, double close, double volume, bool final, bool selected,
        bool requireTrade, out (double Line, double Signal, Signal Trade) result, Number? typicalOverride = null)
    {
        result = default;
        var h = Number.Of(high); var l = Number.Of(low); var p = Number.Of(close);
        var size = Number.Of(volume);
        var typical = typicalOverride ?? (selected ? p : (h + l + p).Divide(3));
        var previous = Number.Of(_started ? _previous : close);
        var tr = Max(h - l, Max(Abs(h - previous), Abs(l - previous)));
        var atrState = _atr; var volumeState = _volume; var signalState = _signal;
        var atr = atrState.Next(Ball.Exact(tr), kind, length);
        var average = volumeState.Next(Ball.Exact(size), kind, length);
        var movement = Ball.Exact((typical - _previousPrice).Times(10));
        if (!(movement - atr).TrySign(out var positive) || !(movement + atr).TrySign(out var negative)) return false;
        var vote = (positive > 0 ? size : default) - (negative < 0 ? size : default);
        var sum = _sum + vote - (_votes.Count == length ? _votes.Peek() : default);
        if (!average.TrySign(out var averageSign)) return false;
        if (averageSign <= 0) average = Ball.Exact(Number.Integer(1));
        Ball line = default;
        if (sum.Sign != 0)
        {
            var numerator = sum.Times(100).Divide(length);
            var a = numerator.Divide(average.Lower); var b = numerator.Divide(average.Higher);
            line = numerator.Sign > 0 ? Ball.Bounds(b, a) : Ball.Bounds(a, b);
        }
        if (!line.TryPublish(out var lineValue)) return false;
        var signal = signalState.Next(line, kind, smooth);
        if (!signal.TryPublish(out var signalValue)) return false;
        var trade = Signal.None;
        if (requireTrade)
        {
            if (!signal.TrySign(out var direction) || !(signal - _previousSignal).TrySign(out var change)) return false;
            trade = direction > 0 && change > 0 ? Signal.StrongBuy : direction < 0 && change < 0 ? Signal.StrongSell
                : direction > 0 ? Signal.Buy : direction < 0 ? Signal.Sell : Signal.None;
        }
        if (final)
        {
            _atr = atrState; _volume = volumeState; _signal = signalState; _previousSignal = signal;
            if (_votes.Count == length) _votes.Dequeue();
            _votes.Enqueue(vote); _sum = sum; _previousPrice = typical; _previous = close; _started = true;
        }
        result = (lineValue, signalValue, trade); return true;
    }
}
