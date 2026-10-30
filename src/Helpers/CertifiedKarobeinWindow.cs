using OoplesFinance.StockIndicators.Streaming;
using System.Numerics;
using B = OoplesFinance.StockIndicators.Helpers.CertifiedKarobeinWindow.Interval;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Helpers;

// Outward dyadic bounds keep recursive averages bounded in size. Publication is
// accepted only when both endpoints round to the same binary64 value and the
// signal comparisons are proved. Ambiguity replays the unchanged exact kernel.
internal sealed class CertifiedKarobeinWindow : IDisposable
{
    private readonly MovingAvgType _kind;
    private readonly int _length;
    private Average _price, _fall, _rise;
    private int _precision;
    private readonly List<double> _observations = new();
    private KarobeinWindow? _exact;
    private int _exactCount;
    private bool _onlyExact;
    private B _previous, _line, _older;
    private B _gap = B.Exact(1), _olderGap = B.Exact(1);
    internal int ExactReplayUpdates { get; private set; }
    internal int PrecisionRefinements { get; private set; }

    internal CertifiedKarobeinWindow(MovingAvgType kind, int length, int precision = 192)
    {
        _kind = kind; _length = Math.Max(1, length); _precision = precision;
        _price = new(kind, _length); _fall = new(kind, _length); _rise = new(kind, _length);
    }

    private static bool Sign(B value, out int sign)
    {
        sign = value.Lower.Sign > 0 ? 1 : value.Upper.Sign < 0 ? -1 : 0;
        return sign != 0 || value.IsZero;
    }

    private static bool Divide(B value, B divisor, out B result)
    {
        result = default;
        if (!Sign(divisor, out var sign) || sign == 0) return false;
        // Reciprocal is decreasing on either side of zero. Multiplication
        // applies the shared interval implementation's outward rounding.
        result = value * new B((F)1 / divisor.Upper, (F)1 / divisor.Lower, divisor.Precision);
        return true;
    }

    private static B Clip(B value)
    {
        F Clamp(F x) => x.Sign < 0 ? 0 : x > (F)1 ? 1 : x;
        return new(Clamp(value.Lower), Clamp(value.Upper), value.Precision);
    }

    private static bool Fold(B ratio, B fall, B rise, out B line, out B gap)
    {
        line = default; gap = B.Exact(1);
        if (ratio.IsZero) return true;
        if (!Sign(ratio, out var ratioSign)) return false;
        var first = ratio + rise;
        if (!Sign(first, out var firstSign) || firstSign == 0) return false;
        if (ratioSign != firstSign) { line = B.Exact(1); gap = default; return true; }
        // With no falling contribution the exact quotient is one, regardless
        // of the width of the certified basis. Keep that algebraic identity so
        // a sustained rise does not replay exact history for every flat signal.
        if (fall.IsZero) { line = B.Exact(1); gap = default; return true; }
        B basis;
        if (firstSign > 0 && rise.Lower.Sign >= 0 || firstSign < 0 && rise.Upper.Sign <= 0) basis = first;
        else if (firstSign > 0 && rise.Upper.Sign <= 0 || firstSign < 0 && rise.Lower.Sign >= 0) basis = ratio;
        else return false;
        var denominator = basis + fall;
        if (!Divide(basis - fall, denominator, out var value)
            || !Divide(fall * B.Exact(2), denominator, out var complement)) return false;
        line = Clip(value); gap = Clip(complement);
        return true;
    }

    private static bool Trade(B slope, B oldSlope, out Signal signal)
    {
        signal = default;
        if (!Sign(slope, out var sign)) return false;
        if (sign == 0) { signal = Signal.None; return true; }
        var acceleration = slope - oldSlope;
        if (sign > 0)
        {
            if (acceleration.Lower.Sign > 0) signal = Signal.StrongBuy;
            else if (acceleration.Upper.Sign <= 0) signal = Signal.Buy;
            else return false;
        }
        else
        {
            if (acceleration.Upper.Sign < 0) signal = Signal.StrongSell;
            else if (acceleration.Lower.Sign >= 0) signal = Signal.Sell;
            else return false;
        }
        return true;
    }

    private bool Publish(B line, B gap, out (double Line, Signal Trade) result)
    {
        result = default;
        var lower = line.Lower.Publish(); var upper = line.Upper.Publish();
        if (BitConverter.DoubleToInt64Bits(lower) != BitConverter.DoubleToInt64Bits(upper))
        {
            lower = ((F)1 - gap.Upper).Publish(); upper = ((F)1 - gap.Lower).Publish();
            if (BitConverter.DoubleToInt64Bits(lower) != BitConverter.DoubleToInt64Bits(upper)) return false;
        }
        // Near one, subtracting line bounds loses a tiny exact slope. The
        // independently bounded complement 2*fall/(basis+fall) preserves it.
        if (!Trade(line - _line, _line - _older, out var signal)
            && !Trade(_gap - gap, _olderGap - _gap, out signal)) return false;
        result = (lower, signal);
        return true;
    }

    private (double Line, Signal Trade) Exact(double price, bool final)
    {
        _exact ??= new KarobeinWindow(_kind, _length, certify: false);
        while (_exactCount < _observations.Count)
        {
            _exact.Next(_observations[_exactCount], true);
            _exactCount++; ExactReplayUpdates++;
        }
        var result = _exact.Next(price, final); ExactReplayUpdates++;
        if (final) _exactCount++;
        return result;
    }

    private (double Line, Signal Trade) Unbounded(double price, bool final)
    {
        // An undecidable branch or a denominator containing zero cannot be
        // represented by these finite bounds. Only a successful final update
        // switches the instance to exact execution; rejected previews are inert.
        var result = Exact(price, final);
        if (final) { _onlyExact = true; _observations.Add(price); }
        return result;
    }

    // Refinement replays only bounded intervals, never growing exact fractions.
    // It is cached independently of commit/preview, so a failed preview cannot
    // advance the observation history or any mathematical state.
    private bool Refine(double price)
    {
        if (_observations.Count == 0) return false;
        var precision = _precision;
        for (var attempt = 0; attempt < 4 && precision < 65536; attempt++)
        {
            precision *= 2;
            using var candidate = new CertifiedKarobeinWindow(_kind, _length, precision);
            var replayed = true;
            foreach (var observation in _observations)
                if (!candidate.BoundedStep(observation, true, false, out _, out _))
                { replayed = false; break; }
            if (!replayed || !candidate.BoundedStep(price, false, true, out _, out _)) continue;
            _precision = precision; PrecisionRefinements++;
            _price = candidate._price; _fall = candidate._fall; _rise = candidate._rise;
            _previous = candidate._previous; _line = candidate._line; _older = candidate._older;
            _gap = candidate._gap; _olderGap = candidate._olderGap;
            return true;
        }
        return false;
    }

    private bool BoundedStep(double price, bool final, bool publish,
        out (double Line, Signal Trade) result, out bool bounded)
    {
        result = default; bounded = false;
        var input = B.Exact(F.Of(price), _precision);
        if (_length == 1)
        {
            // Every supported mean is the current input. On a fall, basis and
            // fall are the same exact ratio, so their difference is zero.
            // Preserve that correlation instead of refining independent bounds.
            var singleLine = B.Exact(_previous.IsZero || input.IsZero || input.Lower < _previous.Lower ? 0 : 1);
            var singleGap = B.Exact(1) - singleLine;
            bounded = true;
            if (publish && !Publish(singleLine, singleGap, out result)) return false;
            if (final)
            {
                _previous = input; _older = _line; _line = singleLine;
                _olderGap = _gap; _gap = singleGap;
            }
            return true;
        }
        var mean = _price.Evaluate(input);
        if (!Sign(mean.Value - _previous, out var direction)) return false;
        B ratio = default;
        if (!_previous.IsZero && !Divide(mean.Value, _previous, out ratio)) return false;
        var falling = direction < 0 ? ratio : default;
        var rising = direction > 0 ? ratio : default;
        var fall = _fall.Evaluate(falling); var rise = _rise.Evaluate(rising);
        if (!Fold(ratio, fall.Value, rise.Value, out var line, out var gap)) return false;
        bounded = true;
        if (publish && !Publish(line, gap, out result)) return false;
        if (final)
        {
            _price.Commit(input, mean); _fall.Commit(falling, fall); _rise.Commit(rising, rise);
            _previous = mean.Value; _older = _line; _line = line;
            _olderGap = _gap; _gap = gap;
        }
        return true;
    }

    internal (double Line, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        if (_onlyExact) return Unbounded(price, final);
        if (BoundedStep(price, final, true, out var result, out var bounded))
        {
            if (final) _observations.Add(price);
            return result;
        }
        if (Refine(price)) return Next(price, final);
        result = Exact(price, final);
        if (final)
        {
            if (bounded) BoundedStep(price, true, false, out _, out _);
            else _onlyExact = true;
            _observations.Add(price);
        }
        return result;
    }

    internal void Reset()
    {
        _price.Reset(); _fall.Reset(); _rise.Reset(); _observations.Clear(); _exact?.Reset();
        _exactCount = ExactReplayUpdates = PrecisionRefinements = 0; _onlyExact = false; _previous = _line = _older = default;
        _gap = _olderGap = B.Exact(1); _precision = 192;
    }
    public void Dispose() => _exact?.Dispose();

    internal readonly struct Interval
    {
        internal readonly F Lower, Upper;
        private readonly int _precision;
        internal int Precision => _precision;
        internal Interval(F lower, F upper, int precision = 0)
        { Lower = lower; Upper = upper; _precision = precision; }
        internal static Interval Exact(F value, int precision = 0) => new(value, value, precision);
        internal bool IsZero => Lower.Sign == 0 && Upper.Sign == 0;
        private static int Bits(BigInteger value)
        {
            var bytes = BigInteger.Abs(value).ToByteArray(); var last = bytes.Length - 1;
            while (last > 0 && bytes[last] == 0) last--;
            var bits = last * 8;
            for (var valueByte = bytes[last]; valueByte != 0; valueByte >>= 1) bits++;
            return bits;
        }
        private static F Round(F value, bool upper, int precision)
        {
            if (value.Sign == 0) return value;
            var exponent = Bits(value.Numerator) - Bits(value.Denominator) - precision;
            var grid = exponent >= 0 ? new F(BigInteger.One << exponent, BigInteger.One)
                : new F(BigInteger.One, BigInteger.One << -exponent);
            var scaled = value / grid;
            return new F(upper ? scaled.Ceiling() : scaled.Floor(), BigInteger.One) * grid;
        }
        private static Interval Bound(F lower, F upper, int precision) => precision == 0
            ? new(lower, upper) : new(Round(lower, false, precision), Round(upper, true, precision), precision);
        public static Interval operator +(Interval a, Interval b) => Bound(a.Lower + b.Lower, a.Upper + b.Upper, Math.Max(a.Precision, b.Precision));
        public static Interval operator -(Interval a, Interval b) => Bound(a.Lower - b.Upper, a.Upper - b.Lower, Math.Max(a.Precision, b.Precision));
        public static Interval operator *(Interval a, Interval b)
        {
            var values = new[] { a.Lower * b.Lower, a.Lower * b.Upper, a.Upper * b.Lower, a.Upper * b.Upper };
            var lower = values[0]; var upper = values[0];
            foreach (var value in values)
            { if (value.CompareTo(lower) < 0) lower = value; if (value.CompareTo(upper) > 0) upper = value; }
            return Bound(lower, upper, Math.Max(a.Precision, b.Precision));
        }
        internal Interval Divide(int divisor) => Bound(Lower / divisor, Upper / divisor, Precision);
    }

    private sealed class Average(MovingAvgType kind, int length)
    {
        internal readonly record struct Point(B Sum, B Weighted, B Value);
        private readonly Queue<B> _history = new();
        private B _sum, _weighted, _previous;
        private long _count;
        private bool Finite => kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
        internal Point Evaluate(B value)
        {
            if (length == 1) return new(default, default, value);
            var sum = _sum; var weighted = _weighted; B result;
            if (Finite)
            {
                if (kind == MovingAvgType.WeightedMovingAverage) weighted = weighted - sum + value * B.Exact(length);
                if (_history.Count == length) sum -= _history.Peek();
                sum += value;
                if (kind == MovingAvgType.WeightedMovingAverage)
                {
                    Divide(weighted, B.Exact((F)((long)length * (length + 1L) / 2)), out result);
                }
                else result = _count + 1 < length ? default : sum.Divide(length);
            }
            else if (kind == MovingAvgType.ExponentialMovingAverage && _count < length)
            { sum += value; result = sum.Divide((int)(_count + 1)); }
            else
            {
                var ema = kind == MovingAvgType.ExponentialMovingAverage;
                Divide(_previous * B.Exact(length - 1L) + value * B.Exact(ema ? 2 : 1),
                    B.Exact(ema ? length + 1L : length), out result);
            }
            return new(sum, weighted, result);
        }
        internal void Commit(B value, Point point)
        {
            if (Finite && length != 1)
            {
                if (_history.Count == length) _history.Dequeue();
                _history.Enqueue(value);
            }
            _sum = point.Sum; _weighted = point.Weighted; _previous = point.Value; _count++;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; }
    }
}
