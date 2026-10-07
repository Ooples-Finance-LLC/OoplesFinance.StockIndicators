using System.Numerics;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Helpers;

// Outward dyadic bounds certify discrete decisions without retaining growing
// rational denominators. The exact evaluator is replayed only when a comparison
// is ambiguous. Raw observations are retained to make that replay lossless.
internal sealed class CertifiedKaufmanBinaryWindow
{
    private const int Precision = 192;
    private readonly int _length;
    private readonly F _fast, _slow, _filter;
    private readonly Queue<F> _prices = new(), _moves = new();
    private readonly Queue<Interval> _changes = new();
    private readonly List<double> _observations = new();
    private readonly KaufmanBinaryWindow _exact;
    private int _exactCount;
    private F _price, _travel;
    private Interval _average, _low, _high, _sum, _squares;
    private bool _started, _lowIsAverage, _highIsAverage;
    internal int ExactReplayUpdates { get; private set; }

    internal CertifiedKaufmanBinaryWindow(int length, double fast, double slow, double filter)
    {
        _length = Math.Max(1, length); _fast = F.Of(fast); _slow = F.Of(slow); _filter = F.Of(filter) / 100;
        _exact = new KaufmanBinaryWindow(length, fast, slow, filter);
    }

    internal readonly struct Interval
    {
        internal readonly F Lower, Upper;
        internal Interval(F lower, F upper) { Lower = lower; Upper = upper; }
        internal static Interval Exact(F value) => new(value, value);
        internal bool IsZero => Lower.Sign == 0 && Upper.Sign == 0;
        private static int Bits(BigInteger value)
        {
            var bytes = BigInteger.Abs(value).ToByteArray(); var last = bytes.Length - 1;
            while (last > 0 && bytes[last] == 0) last--;
            var bits = last * 8;
            for (var valueByte = bytes[last]; valueByte != 0; valueByte >>= 1) bits++;
            return bits;
        }
        private static F Round(F value, bool upper)
        {
            if (value.Sign == 0) return value;
            var exponent = Bits(value.Numerator) - Bits(value.Denominator) - Precision;
            var grid = exponent >= 0 ? new F(BigInteger.One << exponent, BigInteger.One)
                : new F(BigInteger.One, BigInteger.One << -exponent);
            var scaled = value / grid;
            return new F(upper ? scaled.Ceiling() : scaled.Floor(), BigInteger.One) * grid;
        }
        private static Interval Bound(F lower, F upper) => new(Round(lower, false), Round(upper, true));
        public static Interval operator +(Interval a, Interval b) => Bound(a.Lower + b.Lower, a.Upper + b.Upper);
        public static Interval operator -(Interval a, Interval b) => Bound(a.Lower - b.Upper, a.Upper - b.Lower);
        public static Interval operator *(Interval a, Interval b)
        {
            var values = new[] { a.Lower * b.Lower, a.Lower * b.Upper, a.Upper * b.Lower, a.Upper * b.Upper };
            var lower = values[0]; var upper = values[0];
            foreach (var value in values)
            { if (value.CompareTo(lower) < 0) lower = value; if (value.CompareTo(upper) > 0) upper = value; }
            return Bound(lower, upper);
        }
        internal Interval Square()
        {
            var left = Lower * Lower; var right = Upper * Upper;
            var lower = Lower.Sign <= 0 && Upper.Sign >= 0 ? (F)0 : left.CompareTo(right) < 0 ? left : right;
            return Bound(lower, left.CompareTo(right) > 0 ? left : right);
        }
        internal Interval Divide(int divisor) => Bound(Lower / divisor, Upper / divisor);
        internal static Interval Hull(Interval a, Interval b) => new(
            a.Lower.CompareTo(b.Lower) < 0 ? a.Lower : b.Lower, a.Upper.CompareTo(b.Upper) > 0 ? a.Upper : b.Upper);
    }

    private static bool? Exceeds(Interval distance, F filter, Interval variance)
    {
        if (filter.Sign == 0 || variance.IsZero)
            return distance.Lower.Sign > 0 ? true : distance.Upper.Sign <= 0 ? false : null;
        if (filter.Sign > 0 && distance.Upper.Sign <= 0) return false;
        if (filter.Sign < 0 && (distance.Lower.Sign > 0 || distance.Lower.Sign == 0 && variance.Lower.Sign > 0)) return true;
        var comparison = distance.Square() - Interval.Exact(filter * filter) * variance;
        if (filter.Sign > 0)
            return comparison.Upper.Sign <= 0 ? false : distance.Lower.Sign > 0 && comparison.Lower.Sign > 0 ? true : null;
        return distance.Upper.Sign < 0 && comparison.Lower.Sign >= 0 ? false : comparison.Upper.Sign < 0 ? true : null;
    }

    private double ExactDecision(double price, bool final)
    {
        while (_exactCount < _observations.Count)
        { _exact.Next(_observations[_exactCount++], true); ExactReplayUpdates++; }
        var result = _exact.Next(price, final); ExactReplayUpdates++;
        if (final) _exactCount++;
        return result;
    }

    internal double Next(double price, bool final)
    {
        var value = F.Of(price); var full = _prices.Count == _length;
        var move = _started ? (value - _price).Abs() : (F)0;
        var travel = _travel + move - (full ? _moves.Peek() : (F)0);
        var efficiency = full && travel.Sign != 0 ? (value - _prices.Peek()).Abs() / travel : (F)0;
        var coefficient = efficiency * _fast + _slow;
        var previous = _started ? _average : Interval.Exact(value);
        var change = Interval.Exact(coefficient * coefficient) * (Interval.Exact(value) - previous);
        var average = change.IsZero ? previous : previous + change;
        var expired = full ? _changes.Peek() : default;
        var sum = _sum + change - expired;
        var squares = _squares + change.Square() - expired.Square();
        var variance = _changes.Count < _length - 1 ? default
            : (squares * Interval.Exact(_length) - sum.Square()).Divide(_length).Divide(_length);
        // Population variance is nonnegative, including cancellation at a tie.
        variance = new Interval(variance.Lower.Sign < 0 ? 0 : variance.Lower, variance.Upper.Sign < 0 ? 0 : variance.Upper);
        var falling = change.Upper.Sign < 0; var rising = change.Lower.Sign > 0;
        var lowIsAverage = falling || change.IsZero && _lowIsAverage;
        var highIsAverage = rising || change.IsZero && _highIsAverage;
        var low = falling ? average : change.Lower.Sign >= 0 ? _low : Interval.Hull(_low, average);
        var high = rising ? average : change.Upper.Sign <= 0 ? _high : Interval.Hull(_high, average);
        var buy = Exceeds(lowIsAverage ? default : average - low, _filter, variance);
        var sell = buy == true ? false : Exceeds(highIsAverage ? default : high - average, _filter, variance);
        var wave = buy == true ? 1d : buy == false && sell.HasValue ? sell.Value ? -1d : 0d : ExactDecision(price, final);
        if (final)
        {
            if (full) { _prices.Dequeue(); _moves.Dequeue(); _changes.Dequeue(); }
            _prices.Enqueue(value); _moves.Enqueue(move); _changes.Enqueue(change); _observations.Add(price);
            _price = value; _travel = travel; _average = average; _low = low; _high = high;
            _sum = sum; _squares = squares; _started = true; _lowIsAverage = lowIsAverage; _highIsAverage = highIsAverage;
        }
        return wave;
    }

    internal void Reset()
    {
        _prices.Clear(); _moves.Clear(); _changes.Clear(); _observations.Clear(); _exact.Reset();
        _exactCount = ExactReplayUpdates = 0; _price = _travel = default;
        _average = _low = _high = _sum = _squares = default;
        _started = _lowIsAverage = _highIsAverage = false;
    }
}
