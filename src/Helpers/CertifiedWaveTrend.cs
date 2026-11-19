using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Helpers;

// All centers are rounded with an outward error bound. Only certified binary64
// outputs and exact signal comparisons are published; the owner replays on doubt.
internal sealed class CertifiedWaveTrend
{
    private static readonly Number Padding = Number.Of(Math.Pow(2, -78));
    private readonly MovingAvgType _kind;
    private Average _basis, _deviation, _line, _signal;
    private Ball _previousLine, _previousMargin;
    private Number _first;
    private bool _constant = true;
    private long _count;

    internal CertifiedWaveTrend(MovingAvgType kind, int channel, int average, int signal)
    {
        _kind = kind;
        _basis = new(kind, channel); _deviation = new(kind, channel);
        _line = new(kind, average); _signal = new(kind, signal);
    }

    internal static bool Supports(MovingAvgType kind, int channel, int average, int signal)
        => kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod
            && channel > 1 && average > 1 && signal > 1;

    private static Number Abs(Number n) => n.Sign < 0 ? default(Number) - n : n;
    private static Number Max(Number a, Number b) => (a - b).Sign >= 0 ? a : b;
    private static Number Upper(Number n) { var rounded = n.Round(80); return rounded + rounded * Padding; }

    private readonly struct Ball
    {
        private readonly Number _center, _radius;
        private Ball(Number center, Number radius) { _center = center; _radius = radius; }
        internal Number Lower => _center - _radius;
        private Number Higher => _center + _radius;
        internal bool IsZero => _center.Sign == 0 && _radius.Sign == 0;
        private static Ball Rounded(Number center, Number radius)
        {
            var rounded = center.Round(160);
            return new(rounded, Upper(radius + Abs(center - rounded)));
        }
        internal static Ball Exact(Number n) => Rounded(n, default);
        public static Ball operator +(Ball a, Ball b) => Rounded(a._center + b._center, a._radius + b._radius);
        public static Ball operator -(Ball a, Ball b) => Rounded(a._center - b._center, a._radius + b._radius);
        internal Ball Times(long n) => Rounded(_center.Times(n), _radius.Times(n));
        internal Ball TimesPositive(Number n) => Rounded(_center * n, _radius * n);
        internal Ball Divide(long n) => Rounded(_center.Divide(n), _radius.Divide(n));
        internal Ball Absolute()
        {
            if (Lower.Sign >= 0) return this;
            if (Higher.Sign <= 0) return new(default(Number) - _center, _radius);
            var high = Max(Abs(Lower), Abs(Higher));
            return Rounded(high.Divide(2), high.Divide(2));
        }
        // Caller proves that the entire denominator is strictly positive.
        internal Ball DividePositive(Ball denominator)
        {
            var center = _center.Divide(denominator._center);
            var radius = (_radius + Abs(center) * denominator._radius).Divide(denominator.Lower);
            return Rounded(center, radius);
        }
        internal bool TrySign(out int sign)
        { sign = _center.Sign; return _radius.Sign == 0 || (Abs(_center) - _radius).Sign > 0; }
        internal bool TryPublish(out double value)
        { value = Lower.Publish(); return BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(Higher.Publish()); }
    }

    private struct Average(MovingAvgType kind, int length)
    {
        private Ball _sum, _previous;
        private long _count;
        internal Ball Next(Ball value, out Average next)
        {
            next = this;
            Ball result;
            if (kind == MovingAvgType.ExponentialMovingAverage && _count < length)
            { next._sum = _sum + value; result = next._sum.Divide(_count + 1); }
            else result = (_previous.Times(length - 1L) + value.Times(kind == MovingAvgType.ExponentialMovingAverage ? 2 : 1))
                .Divide(kind == MovingAvgType.ExponentialMovingAverage ? length + 1L : length);
            next._previous = result; next._count++;
            return result;
        }
    }

    private static bool TryCross(Ball previous, Ball current, int threshold, out bool crosses)
    {
        crosses = false;
        if (!(previous - Ball.Exact(Number.Of(threshold))).TrySign(out var before)) return false;
        if (before >= 0) return true;
        if (!(current - Ball.Exact(Number.Of(threshold))).TrySign(out var after)) return false;
        crosses = after > 0; return true;
    }

    internal bool TryNext(Number price, bool final, out (double Line, double SignalLine, Signal Trade) result)
    {
        result = default;
        var constant = _count == 0 || _constant && (price - _first).Sign == 0;
        var basis = _basis.Next(Ball.Exact(price), out var nextBasis);
        // An EMA initialized with a constant prefix equals that price exactly,
        // including non-dyadic HLC prices; avoid losing this zero correlation.
        var residual = _kind == MovingAvgType.ExponentialMovingAverage && constant ? default : Ball.Exact(price) - basis;
        var deviation = _deviation.Next(residual.Absolute(), out var nextDeviation);
        Ball channel;
        if (deviation.IsZero) channel = default;
        else
        {
            if (deviation.Lower.Sign <= 0) return false;
            var denominator = deviation.TimesPositive(Number.Of(.015));
            if (denominator.Lower.Sign <= 0) return false;
            channel = residual.DividePositive(denominator);
        }
        var line = _line.Next(channel, out var nextLine);
        var signal = _signal.Next(line, out var nextSignal);
        var margin = line - signal;
        if (!line.TryPublish(out var value) || !signal.TryPublish(out var signalValue)
            || !margin.TrySign(out var direction)) return false;
        Signal trade;
        if (direction != 0)
        {
            if (!(margin - _previousMargin).TrySign(out var change)) return false;
            if (direction > 0 && change > 0) trade = Signal.StrongBuy;
            else if (direction < 0 && change < 0) trade = Signal.StrongSell;
            else if (direction > 0) trade = Signal.Buy;
            else
            {
                if (!TryCross(_previousLine, line, -53, out var buy)) return false;
                trade = buy ? Signal.Buy : Signal.Sell;
            }
        }
        else
        {
            if (!TryCross(_previousLine, line, -53, out var buy)) return false;
            if (buy) trade = Signal.Buy;
            else
            {
                if (!TryCross(line, _previousLine, 53, out var sell)) return false;
                trade = sell ? Signal.Sell : Signal.None;
            }
        }
        if (final)
        {
            _basis = nextBasis; _deviation = nextDeviation; _line = nextLine; _signal = nextSignal;
            _previousLine = line; _previousMargin = margin;
            if (_count == 0) _first = price;
            _constant = constant; _count++;
        }
        result = (value, signalValue, trade); return true;
    }
}
