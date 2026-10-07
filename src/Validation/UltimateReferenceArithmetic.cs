using System.Numerics;

namespace OoplesFinance.StockIndicators.Validation;

// Independent UMA oracle arithmetic: direct logarithm series and a binomial
// product limit for exp. Production uses atanh and Taylor/reciprocal exp.
internal static class UltimateReferenceArithmetic
{
    private static BigInteger SquareRoot(BigInteger value)
    {
        if (value.IsZero) return BigInteger.Zero;
        BigInteger low = 0, high = BigInteger.One << ((Bits(value) + 1) / 2);
        if (high * high == value) return high;
        while (high - low > 1)
        { var middle = (low + high) / 2; if (middle * middle <= value) low = middle; else high = middle; }
        return low;
    }
    internal static (ReferenceFraction Low, ReferenceFraction High) Root(ReferenceFraction value, int bits)
    {
        var parts = value.Components;
        if (parts.Numerator.Sign < 0) throw new ArgumentOutOfRangeException(nameof(value));
        var numerator = SquareRoot(parts.Numerator); var denominator = SquareRoot(parts.Denominator);
        if (numerator * numerator == parts.Numerator && denominator * denominator == parts.Denominator)
        { var exact = new ReferenceFraction(numerator) / new ReferenceFraction(denominator); return (exact, exact); }
        var precision = checked(bits + Math.Max(0, (Bits(parts.Denominator) - Bits(parts.Numerator) + 1) / 2));
        var root = SquareRoot((parts.Numerator << checked(2 * precision)) / parts.Denominator);
        var scale = new ReferenceFraction(BigInteger.One << precision);
        return (new ReferenceFraction(root) / scale, new ReferenceFraction(root + 1) / scale);
    }
    private static BigInteger Floor(BigInteger numerator, BigInteger denominator)
    {
        var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
        return remainder.Sign < 0 ? quotient - 1 : quotient;
    }
    private static BigInteger Ceiling(BigInteger numerator, BigInteger denominator) => -Floor(-numerator, denominator);
    private static int Bits(BigInteger value)
    {
        var bytes = BigInteger.Abs(value).ToByteArray(); var last = bytes.Length - 1;
        while (last > 0 && bytes[last] == 0) last--;
        var result = last * 8; for (var b = bytes[last]; b != 0; b >>= 1) result++;
        return result;
    }

    private static (BigInteger Low, BigInteger High) UnitLog(ReferenceFraction x, int bits)
    {
        var scale = BigInteger.One << bits;
        var t = (new ReferenceFraction(1) - x).Components;
        var low = Floor(t.Numerator * scale, t.Denominator); var high = Ceiling(t.Numerator * scale, t.Denominator);
        var powerLow = scale; var powerHigh = scale; BigInteger sumLow = 0, sumHigh = 0; var index = 0;
        while (true)
        {
            index++; powerLow = powerLow * low >> bits; powerHigh = (powerHigh * high + scale - 1) >> bits;
            sumLow += powerLow / index; sumHigh += Ceiling(powerHigh, index);
            var next = (powerHigh * high + scale - 1) >> bits;
            var tail = Ceiling(next * scale, (index + 1) * (scale - high));
            if (tail <= 2) return (-sumHigh - tail, -sumLow);
        }
    }

    private static (BigInteger Low, BigInteger High) Log(ReferenceFraction x, int bits)
    {
        var half = new ReferenceFraction(1) / new ReferenceFraction(2); var one = new ReferenceFraction(1);
        var two = new ReferenceFraction(2); var power = 0;
        while (x.CompareTo(half) < 0) { x *= two; power--; }
        while (x.CompareTo(one) > 0) { x /= two; power++; }
        var unit = UnitLog(x, bits); var negativeLogTwo = UnitLog(half, bits);
        return power >= 0 ? (unit.Low - power * negativeLogTwo.High, unit.High - power * negativeLogTwo.Low)
            : (unit.Low - power * negativeLogTwo.Low, unit.High - power * negativeLogTwo.High);
    }

    private static (BigInteger Low, BigInteger High) Exp(BigInteger argument, int bits)
    {
        if (argument.Sign > 0) throw new ArgumentOutOfRangeException(nameof(argument));
        var unit = BigInteger.One << bits;
        if (argument.IsZero) return (unit, unit);
        var magnitude = -argument;
        if (magnitude >= (bits + 8) * unit) return (BigInteger.Zero, BigInteger.One);
        var exponent = checked(bits + 2 * Math.Max(0, Bits(magnitude) - bits) + 16);
        var precision = checked(bits + exponent + 16); var scale = BigInteger.One << precision;
        var count = BigInteger.One << exponent;
        var low = scale - Ceiling(magnitude * scale, unit * count);
        var high = scale - Floor(magnitude * scale, unit * count);
        for (var i = 0; i < exponent; i++) { low = low * low >> precision; high = (high * high + scale - 1) >> precision; }
        // For u <= 1/2, -u-u² <= log(1-u) <= -u. The product
        // underestimates exp; exp(delta) <= 1/(1-delta) bounds its error.
        var delta = Ceiling(magnitude * magnitude * scale, unit * unit * count);
        high = Ceiling(high * scale, scale - delta);
        var shift = precision - bits;
        return (low >> shift, (high + (BigInteger.One << shift) - 1) >> shift);
    }

    internal static (ReferenceFraction Low, ReferenceFraction High) Weight(int index, int length, ReferenceFraction exponent, int bits)
    {
        if (index < 1 || index > length) throw new ArgumentOutOfRangeException(nameof(index));
        var p = exponent.Components; var one = new ReferenceFraction(1);
        if (p.Numerator.IsZero) return (one, one);
        var value = new ReferenceFraction(index) / new ReferenceFraction(exponent.Sign > 0 ? length : 1);
        if (p.Denominator.IsOne && BigInteger.Abs(p.Numerator) <= 32)
        {
            var parts = value.Components; var degree = (int)BigInteger.Abs(p.Numerator);
            var exact = new ReferenceFraction(BigInteger.Pow(parts.Numerator, degree)) / new ReferenceFraction(BigInteger.Pow(parts.Denominator, degree));
            if (exponent.Sign < 0) exact = one / exact;
            return (exact, exact);
        }
        var log = Log(value, bits);
        var lower = Floor((exponent.Sign > 0 ? log.Low : log.High) * p.Numerator, p.Denominator);
        var upper = Ceiling((exponent.Sign > 0 ? log.High : log.Low) * p.Numerator, p.Denominator);
        var low = Exp(lower, bits).Low; var high = Exp(BigInteger.Min(0, upper), bits).High;
        var scale = new ReferenceFraction(BigInteger.One << bits);
        return (new ReferenceFraction(low) / scale, new ReferenceFraction(high) / scale);
    }

    internal static (ReferenceFraction Low, ReferenceFraction High) PublicWeightSum(int length, ReferenceFraction exponent, int bits)
    {
        var one = new ReferenceFraction(1); var zero = new ReferenceFraction(0);
        if (length < 1 || exponent.CompareTo(one) < 0 || exponent.CompareTo(new ReferenceFraction(5)) > 0)
            throw new ArgumentOutOfRangeException(nameof(exponent));
        var p = exponent.Components; BigInteger n = length;
        if (p.Denominator.IsOne)
        {
            var degree = (int)p.Numerator;
            var exact = degree switch
            {
                1 => new ReferenceFraction(n * (n + 1)) / new ReferenceFraction(2),
                2 => new ReferenceFraction(n * (n + 1) * (2 * n + 1)) / new ReferenceFraction(6),
                3 => new ReferenceFraction(n * n * (n + 1) * (n + 1)) / new ReferenceFraction(4),
                4 => new ReferenceFraction(n * (n + 1) * (2 * n + 1) * (3 * n * n + 3 * n - 1)) / new ReferenceFraction(30),
                _ => new ReferenceFraction(n * n * (n + 1) * (n + 1) * (2 * n * n + 2 * n - 1)) / new ReferenceFraction(12)
            };
            exact /= new ReferenceFraction(BigInteger.Pow(n, degree)); return (exact, exact);
        }
        var precision = checked(bits + Bits(n) + 40);
        var head = Math.Min(length, Math.Max(256, bits));
        var lower = zero; var upper = zero;
        for (var k = 1; k < head; k++) { var weight = Weight(k, length, exponent, precision); lower += weight.Low; upper += weight.High; }
        var endpoint = Weight(head, length, exponent, precision);
        if (head == length) return (lower + endpoint.Low, upper + endpoint.High);

        // Euler-Maclaurin on [head,length]. Derivatives of (x/n)^p share
        // the same endpoint weight. Collect its coefficient before bounding it.
        var constant = new ReferenceFraction(length) / (exponent + one) + one / new ReferenceFraction(2);
        var multiplier = new ReferenceFraction(-head) / (exponent + one) + one / new ReferenceFraction(2);
        var bernoulli = new List<ReferenceFraction> { one };
        BigInteger factorial = 1; var falling = one;
        var tolerance = one / new ReferenceFraction(BigInteger.One << checked(bits + 8));
        for (var order = 1; ; order++)
        {
            // B_m = -sum(C(m+1,j)*B_j)/(m+1), independent of production moments.
            var sum = zero; BigInteger choose = 1;
            for (var j = 0; j < order; j++)
            { sum += new ReferenceFraction(choose) * bernoulli[j]; choose = choose * (order + 1 - j) / (j + 1); }
            bernoulli.Add((zero - sum) / new ReferenceFraction(order + 1));
            factorial *= order; falling *= exponent - new ReferenceFraction(order - 1);
            if (order % 2 != 0) continue;
            var coefficient = bernoulli[order] / new ReferenceFraction(factorial);
            var oddFalling = falling / (exponent - new ReferenceFraction(order - 1));
            var derivative = coefficient * oddFalling;
            constant += derivative / new ReferenceFraction(BigInteger.Pow(n, order - 1));
            multiplier -= derivative / new ReferenceFraction(BigInteger.Pow(head, order - 1));
            if (order < 8) continue;
            var remainder = (coefficient * falling).Abs() * endpoint.High
                / new ReferenceFraction(BigInteger.Pow(head, order - 1)) / (new ReferenceFraction(order - 1) - exponent);
            if (remainder.CompareTo(tolerance) > 0) continue;
            var lowEnd = multiplier * (multiplier.Sign >= 0 ? endpoint.Low : endpoint.High);
            var highEnd = multiplier * (multiplier.Sign >= 0 ? endpoint.High : endpoint.Low);
            return (lower + constant + lowEnd - remainder, upper + constant + highEnd + remainder);
        }
    }
}
