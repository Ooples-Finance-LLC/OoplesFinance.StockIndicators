using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Local arithmetic for UMA's fractional weights. All intervals are outward
// rounded; their precision is chosen by the weighted-mean caller.
internal static class UltimatePowerWeights
{
    internal readonly struct Fraction : IComparable<Fraction>
    {
        internal readonly BigInteger Numerator;
        private readonly BigInteger _denominator;
        internal BigInteger Denominator => _denominator.IsZero ? BigInteger.One : _denominator;
        internal int Sign => Numerator.Sign;
        internal Fraction(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero) throw new DivideByZeroException();
            if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            Numerator = numerator / gcd; _denominator = denominator / gcd;
        }
        internal static Fraction Of(double value)
        {
            Streaming.StreamingInputValidation.Finite(value, nameof(value));
            return new(ExactVarianceWindow.Units(value), BigInteger.One << 1074);
        }
        internal static Fraction Grid(int bits) => new(BigInteger.One, BigInteger.One << bits);
        internal Fraction Abs() => new(BigInteger.Abs(Numerator), Denominator);
        internal Fraction Pow(int power) => new(BigInteger.Pow(Numerator, power), BigInteger.Pow(Denominator, power));
        internal BigInteger Floor() { var q = BigInteger.DivRem(Numerator, Denominator, out var r); return r.Sign < 0 ? q - 1 : q; }
        internal BigInteger Ceiling() => -(-this).Floor();
        internal double Publish() => MacZWindow.Number.Integer(Numerator).Divide(MacZWindow.Number.Integer(Denominator)).Publish();
        public int CompareTo(Fraction other) => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
        public static implicit operator Fraction(int value) => new(value, BigInteger.One);
        public static implicit operator Fraction(long value) => new(value, BigInteger.One);
        public static Fraction operator -(Fraction a) => new(-a.Numerator, a.Denominator);
        public static Fraction operator +(Fraction a, Fraction b)
        {
            var gcd = BigInteger.GreatestCommonDivisor(a.Denominator, b.Denominator);
            var left = b.Denominator / gcd; var right = a.Denominator / gcd;
            return new(a.Numerator * left + b.Numerator * right, a.Denominator * left);
        }
        public static Fraction operator -(Fraction a, Fraction b) => a + -b;
        public static Fraction operator *(Fraction a, Fraction b)
        {
            var left = BigInteger.GreatestCommonDivisor(BigInteger.Abs(a.Numerator), b.Denominator);
            var right = BigInteger.GreatestCommonDivisor(BigInteger.Abs(b.Numerator), a.Denominator);
            return new((a.Numerator / left) * (b.Numerator / right), (a.Denominator / right) * (b.Denominator / left));
        }
        public static Fraction operator /(Fraction a, Fraction b) => a * new Fraction(b.Denominator, b.Numerator);
        public static bool operator <(Fraction a, Fraction b) => a.CompareTo(b) < 0;
        public static bool operator >(Fraction a, Fraction b) => a.CompareTo(b) > 0;
        public static bool operator <=(Fraction a, Fraction b) => a.CompareTo(b) <= 0;
        public static bool operator >=(Fraction a, Fraction b) => a.CompareTo(b) >= 0;
    }

    internal readonly struct Bounds
    {
        internal readonly Fraction Lower, Upper;
        internal Bounds(Fraction lower, Fraction upper) { Lower = lower; Upper = upper; }
        internal Bounds Round(int bits)
        {
            var grid = Fraction.Grid(bits);
            return new(new Fraction((Lower / grid).Floor(), BigInteger.One) * grid,
                new Fraction((Upper / grid).Ceiling(), BigInteger.One) * grid);
        }
    }

    private static int Bits(BigInteger value)
    {
        var bytes = BigInteger.Abs(value).ToByteArray(); var last = bytes.Length - 1;
        while (last > 0 && bytes[last] == 0) last--;
        var bits = last * 8; for (var b = bytes[last]; b != 0; b >>= 1) bits++;
        return bits;
    }

    private static Bounds SmallLog(Fraction value, int bits)
    {
        var z = (value - 1) / (value + 1); var square = z * z;
        var term = z; Fraction sum = 0; var index = 0; var tolerance = Fraction.Grid(bits);
        while (true)
        {
            sum += 2 * term / (2 * index + 1); term *= square; index++;
            var tail = 2 * term / ((2 * index + 1) * (1 - square));
            if (tail <= tolerance) return new(sum, sum + tail);
        }
    }

    internal static Bounds Log(Fraction value, int bits)
    {
        if (value.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        var exponent = Bits(value.Numerator) - Bits(value.Denominator);
        var scale = exponent >= 0 ? new Fraction(BigInteger.One << exponent, BigInteger.One)
            : Fraction.Grid(-exponent);
        if (value < scale) { exponent--; scale /= 2; }
        var precision = checked(bits + Bits(Math.Abs(exponent)) + 2);
        var core = SmallLog(value / scale, precision); var two = SmallLog(2, precision);
        return exponent >= 0 ? new(core.Lower + exponent * two.Lower, core.Upper + exponent * two.Upper)
            : new(core.Lower + exponent * two.Upper, core.Upper + exponent * two.Lower);
    }

    internal static Bounds NegativeExp(Fraction value, int bits)
    {
        if (value.Sign > 0) throw new ArgumentOutOfRangeException(nameof(value));
        if (value.Sign == 0) return new(1, 1);
        // e > 2 gives a conservative tail without exponent-sized allocation.
        if (-value >= bits) return new(0, Fraction.Grid(bits));
        var y = -value; var squares = 0;
        while (y > new Fraction(1, 2)) { y /= 2; squares++; }
        // A bounded dyadic grid avoids raising a large rational denominator to
        // every Taylor degree. Each multiplication and division rounds outward.
        var precision = checked(bits + squares + Bits(bits) + 16);
        var scale = BigInteger.One << precision;
        var yLow = y.Numerator * scale / y.Denominator;
        var yHigh = CeilingPositive(y.Numerator * scale, y.Denominator);
        var totalLow = scale; var totalHigh = scale;
        var termLow = scale; var termHigh = scale; BigInteger tail; var index = 0;
        do
        {
            index++;
            termLow = (termLow * yLow >> precision) / index;
            termHigh = CeilingPositive((termHigh * yHigh + scale - 1) >> precision, index);
            totalLow += termLow; totalHigh += termHigh;
            var first = CeilingPositive((termHigh * yHigh + scale - 1) >> precision, index + 1);
            tail = CeilingPositive(first * scale * (index + 2), scale * (index + 2) - yHigh);
        } while (tail > 2);
        var lower = scale * scale / (totalHigh + tail);
        var upper = CeilingPositive(scale * scale, totalLow);
        for (var i = 0; i < squares; i++)
        { lower = lower * lower >> precision; upper = (upper * upper + scale - 1) >> precision; }
        return new(new Fraction(lower, scale), new Fraction(upper, scale));
    }

    private static BigInteger CeilingPositive(BigInteger numerator, BigInteger denominator)
        => (numerator + denominator - 1) / denominator;

    internal static Bounds Power(Fraction value, Fraction exponent, int bits)
    {
        if (value.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        if (exponent.Sign == 0 || value.CompareTo(1) == 0) return new(1, 1);
        if (value.CompareTo(1) * exponent.Sign > 0) throw new ArgumentOutOfRangeException(nameof(exponent));
        if (exponent.Denominator.IsOne && BigInteger.Abs(exponent.Numerator) <= 32)
        {
            var exact = exponent.Sign > 0 ? value.Pow((int)exponent.Numerator) : (1 / value).Pow((int)-exponent.Numerator);
            return new(exact, exact);
        }
        if (TryRationalPower(value, exponent, out var rational)) return new(rational, rational);
        var extra = Math.Max(0, Bits(exponent.Numerator) - Bits(exponent.Denominator)) + 8;
        var log = Log(value, checked(bits + extra));
        var lower = exponent * (exponent.Sign > 0 ? log.Lower : log.Upper);
        var upper = exponent * (exponent.Sign > 0 ? log.Upper : log.Lower);
        return new(NegativeExp(lower, bits).Lower, NegativeExp(upper > 0 ? 0 : upper, bits).Upper);
    }

    private static bool TryRoot(BigInteger value, int degree, out BigInteger root)
    {
        if (value.IsOne) { root = BigInteger.One; return true; }
        BigInteger left = 0, right = BigInteger.One << ((Bits(value) + degree - 1) / degree);
        while (right - left > 1)
        {
            var middle = (left + right) / 2;
            if (BigInteger.Pow(middle, degree) <= value) left = middle; else right = middle;
        }
        root = BigInteger.Pow(right, degree) == value ? right : left;
        return BigInteger.Pow(root, degree) == value;
    }

    private static bool TryRationalPower(Fraction value, Fraction exponent, out Fraction result)
    {
        result = default;
        // This exact path avoids needless refinement at rational midpoint ties.
        // Larger powers retain the interval path rather than allocating huge integers.
        if (exponent.Denominator > 32 || BigInteger.Abs(exponent.Numerator) > 4096) return false;
        var degree = (int)exponent.Denominator;
        if (!TryRoot(value.Numerator, degree, out var numerator) || !TryRoot(value.Denominator, degree, out var denominator)) return false;
        var root = new Fraction(numerator, denominator);
        result = exponent.Sign >= 0 ? root.Pow((int)exponent.Numerator) : (1 / root).Pow((int)-exponent.Numerator);
        return true;
    }

    private static BigInteger Choose(int n, int k)
    {
        BigInteger result = 1;
        for (var i = 1; i <= k; i++) result = result * (n - i + 1) / i;
        return result;
    }

    private static Bounds Block(long first, long last, int length, Fraction exponent, int bits)
    {
        var count = last - first + 1; var center = new Fraction(first + last, 2);
        var scale = Power(exponent.Sign > 0 ? center / length : center, exponent, checked(bits + 16));
        if (first == last) return scale;
        var radius = new Fraction(last - first, last + first);
        var moments = new List<BigInteger> { count }; Fraction coefficient = 1, series = count, tail;
        var degree = 0; var tolerance = Fraction.Grid(checked(bits + 16));
        while (true)
        {
            coefficient *= (exponent - degree) / (degree + 1); degree++;
            var ratio = radius + exponent.Abs() * radius / (degree + 1);
            tail = count * coefficient.Abs() * radius.Pow(degree) / (1 - ratio);
            if (ratio < 1 && tail <= tolerance) break;
            if (degree % 2 != 0) continue;
            var moment = BigInteger.Pow(count, degree + 1);
            for (var j = 3; j <= degree + 1; j += 2) moment -= Choose(degree + 1, j) * moments[(degree + 1 - j) / 2];
            moment /= degree + 1; moments.Add(moment);
            series += coefficient * new Fraction(moment, BigInteger.Pow(first + last, degree));
        }
        var lower = (series - tail) * scale.Lower;
        return new(lower.Sign < 0 ? 0 : lower, (series + tail) * scale.Upper);
    }

    internal static Bounds Sum(int length, Fraction exponent, int bits)
    {
        if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
        if (exponent.Sign == 0) return new(length, length);
        if (exponent.Denominator.IsOne && exponent.Sign > 0 && exponent.Numerator <= 32)
        {
            var degree = (int)exponent.Numerator; var sums = new List<BigInteger>();
            for (var m = 0; m <= degree; m++)
            {
                var sum = BigInteger.Pow((BigInteger)length + 1, m + 1) - 1;
                for (var j = 0; j < m; j++) sum -= Choose(m + 1, j) * sums[j];
                sums.Add(sum / (m + 1));
            }
            var exact = new Fraction(sums[degree], BigInteger.Pow(length, degree));
            return new(exact, exact);
        }
        var precision = checked(bits + 16); var threshold = precision + Bits(length);
        long first = 1, last = length;
        if (exponent.Sign > 0)
        {
            var width = ((Fraction)length * threshold / exponent).Ceiling();
            if (width < 1) width = 1;
            if (width < length) first = length - (long)width + 1;
        }
        else if (exponent <= -2 * threshold) last = 1;
        var truncated = first > 1 || last < length;
        var blocks = new List<(long First, long Last)>();
        var twice = 2 * exponent.Abs(); var divisor = twice > 1 ? twice : 1;
        while (first <= last)
        {
            var width = ((Fraction)first / divisor).Floor();
            if (width < 1) width = 1;
            if (width > first) width = first;
            var end = Math.Min(last, first + (long)width - 1);
            blocks.Add((first, end)); first = end + 1;
        }
        precision = checked(bits + Bits(blocks.Count) + 24);
        var result = new Bounds(0, 0);
        foreach (var block in blocks)
        {
            var value = Block(block.First, block.Last, length, exponent, precision);
            result = new Bounds(result.Lower + value.Lower, result.Upper + value.Upper).Round(precision);
        }
        return new(result.Lower, result.Upper + (truncated ? Fraction.Grid(checked(bits + 16)) : 0));
    }

    internal static Bounds Mean(IReadOnlyList<Fraction> newestFirst, int length, Fraction exponent, int bits)
    {
        if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
        var available = Math.Min(length, newestFirst.Count); var hasValue = false;
        for (var i = 0; i < available; i++) hasValue |= newestFirst[i].Sign != 0;
        if (!hasValue) return new(0, 0);
        if (available == length)
        {
            var constant = true;
            for (var i = 1; i < available; i++) constant &= newestFirst[i].CompareTo(newestFirst[0]) == 0;
            if (constant) return new(newestFirst[0], newestFirst[0]);
        }
        var denominator = Sum(length, exponent, bits);
        // The dominant endpoint has normalized weight one, even during warmup.
        var divisorLow = denominator.Lower < 1 ? (Fraction)1 : denominator.Lower;
        Fraction lower = 0, upper = 0;
        var groups = new List<(int Index, Fraction Price)>();
        for (var offset = 0; offset < available; offset++)
        {
            var price = newestFirst[offset]; if (price.Sign == 0) continue;
            var index = length - offset; var grouped = false;
            if (exponent.Denominator > 1 && exponent.Denominator <= 31)
            {
                for (var i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    if (!TryRationalPower(new Fraction(index, group.Index), exponent, out var ratio)) continue;
                    groups[i] = (group.Index, group.Price + price * ratio); grouped = true; break;
                }
            }
            if (!grouped) groups.Add((index, price));
        }
        foreach (var group in groups)
        {
            var price = group.Price; if (price.Sign == 0) continue;
            Fraction index = group.Index;
            var weight = Power(exponent.Sign > 0 ? index / length : index, exponent, checked(bits + 16));
            lower += price * (price.Sign > 0 ? weight.Lower : weight.Upper);
            upper += price * (price.Sign > 0 ? weight.Upper : weight.Lower);
        }
        return new(lower / (lower.Sign >= 0 ? denominator.Upper : divisorLow),
            upper / (upper.Sign >= 0 ? divisorLow : denominator.Upper));
    }

    internal static double PublishMean(IReadOnlyList<Fraction> newestFirst, int length, Fraction exponent)
        => PublishMean(bits => Mean(newestFirst, length, exponent, bits));

    internal static double PublishMean(Func<int, Bounds> evaluate)
    {
        for (var bits = 96; ; bits = checked(bits * 2))
        {
            var bounds = evaluate(bits);
            var lower = bounds.Lower.Publish(); var upper = bounds.Upper.Publish();
            if (lower == upper) return lower;
            // A binary64 midpoint need not be separable by interval refinement.
            // Once the uncertainty is below 2^-64 of the nonzero result, the
            // rounded midpoint is at most one representable step from the exact
            // rounded answer. Exact rational cases use zero-width bounds above.
            if (bounds.Lower.Sign == bounds.Upper.Sign)
            {
                var middle = (bounds.Lower + bounds.Upper) / 2;
                if (bounds.Upper - bounds.Lower <= middle.Abs() * Fraction.Grid(64)) return middle.Publish();
            }
        }
    }

    internal static Bounds Root(Fraction value, int bits)
    {
        if (value.Sign < 0) throw new ArgumentOutOfRangeException(nameof(value));
        if (value.Sign == 0) return new(0, 0);
        var numeratorRoot = ExactPopulationDeviation.IntegerRoot(value.Numerator);
        var denominatorRoot = ExactPopulationDeviation.IntegerRoot(value.Denominator);
        if (numeratorRoot * numeratorRoot == value.Numerator && denominatorRoot * denominatorRoot == value.Denominator)
        {
            var exact = new Fraction(numeratorRoot, denominatorRoot); return new(exact, exact);
        }
        // Preserve relative precision for tiny roots as well as the requested
        // absolute precision. The shared integer-root helper requires n > 0.
        var extra = Math.Max(0, (Bits(value.Denominator) - Bits(value.Numerator) + 1) / 2);
        var precision = checked(bits + extra);
        var quotient = (value.Numerator << checked(2 * precision)) / value.Denominator;
        var root = quotient.IsZero ? System.Numerics.BigInteger.Zero : ExactPopulationDeviation.IntegerRoot(quotient);
        var scale = System.Numerics.BigInteger.One << precision;
        return new(new Fraction(root, scale), new Fraction(root + 1, scale));
    }
}
