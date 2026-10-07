using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Only the sign of the cumulative correlations is needed. Distinct rational square
// classes are linearly independent over Q: after grouping, zero is decidable.
// Nonzero sums are separated from zero by successively tighter integer intervals.
internal sealed class PeriodicCorrelationSign
{
    private readonly int _length;
    private readonly Queue<BigInteger> _prices = new();
    private BigInteger _sum, _squares, _weighted;
    private readonly Dictionary<int, List<Term>> _buckets = new();
    private BigInteger _lower, _upper;
    private int _precision = 64, _terms;

    private sealed class Term
    {
        internal BigInteger Kernel, Numerator, Denominator, Lower, Upper;
        internal Term(BigInteger kernel) { Kernel = kernel; Denominator = BigInteger.One; }
    }

    internal PeriodicCorrelationSign(int length) { _length = Math.Max(1, length); }

    internal int Next(double price, bool final)
    {
        var value = ExactVarianceWindow.Units(price);
        var full = _prices.Count == _length;
        var n = full ? _length : _prices.Count + 1;
        var expired = full ? _prices.Peek() : BigInteger.Zero;
        var sum = _sum + value - expired;
        var squares = _squares + value * value - expired * expired;
        var weighted = _weighted + (n - 1) * value - (full ? _sum - expired : BigInteger.Zero);
        var covariance = 2 * weighted - (n - 1) * sum;
        var variance = n * squares - sum * sum;
        var sign = AddCorrelation(covariance, ((BigInteger)n * n - 1) * variance, final);
        if (final)
        {
            if (full) _prices.Dequeue();
            _prices.Enqueue(value); _sum = sum; _squares = squares; _weighted = weighted;
        }
        return sign;
    }

    private int AddCorrelation(BigInteger covariance, BigInteger divisor, bool final)
    {
        if (covariance.IsZero || divisor.IsZero) return BoundSign(null, BigInteger.Zero, BigInteger.One);
        // r^2 = 3*c^2/D. Reduce before forming the radical to cancel input scaling.
        var square = 3 * covariance * covariance;
        var common = BigInteger.GreatestCommonDivisor(square, divisor);
        square /= common; divisor /= common;
        var kernel = square * divisor;
        var numerator = new BigInteger(covariance.Sign);
        var denominator = divisor;
        foreach (var prime in SmallPrimes)
        {
            var factor = prime * prime;
            while (kernel % factor == 0) { kernel /= factor; numerator *= prime; }
        }
        var root = ExactPopulationDeviation.IntegerRoot(kernel);
        if (root * root == kernel) { numerator *= root; kernel = BigInteger.One; }
        var fingerprint = Fingerprint(kernel);
        _buckets.TryGetValue(fingerprint, out var bucket);
        Term? match = null;
        if (bucket is not null)
        {
            foreach (var term in bucket)
            {
                var gcd = BigInteger.GreatestCommonDivisor(kernel, term.Kernel);
                var a = kernel / gcd; var b = term.Kernel / gcd;
                var ar = ExactPopulationDeviation.IntegerRoot(a); var br = ExactPopulationDeviation.IntegerRoot(b);
                if (ar * ar != a || br * br != b) continue;
                match = term; numerator *= ar; denominator *= br; break;
            }
        }
        var target = match ?? new Term(kernel);
        numerator = numerator * target.Denominator + target.Numerator * denominator;
        denominator *= target.Denominator;
        var reduction = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        numerator /= reduction; denominator /= reduction;
        var result = BoundSign(match, numerator, denominator, target.Kernel);
        if (final)
        {
            _lower -= target.Lower; _upper -= target.Upper;
            if (match is not null) _terms--;
            if (numerator.IsZero) bucket?.Remove(target);
            else
            {
                if (match is null)
                {
                    if (bucket is null) { bucket = new(); _buckets.Add(fingerprint, bucket); }
                    bucket.Add(target);
                }
                target.Numerator = numerator; target.Denominator = denominator;
                (target.Lower, target.Upper) = Bounds(numerator, denominator, target.Kernel);
                _lower += target.Lower; _upper += target.Upper; _terms++;
            }
        }
        return result;
    }

    private int BoundSign(Term? replaced, BigInteger numerator, BigInteger denominator, BigInteger kernel = default)
    {
        var count = _terms - (replaced is null ? 0 : 1) + (numerator.IsZero ? 0 : 1);
        if (count == 0) return 0;
        while (true)
        {
            var (low, high) = Bounds(numerator, denominator, kernel);
            low += _lower - (replaced?.Lower ?? BigInteger.Zero);
            high += _upper - (replaced?.Upper ?? BigInteger.Zero);
            if (low.Sign > 0) return 1;
            if (high.Sign < 0) return -1;
            _precision = checked(_precision * 2);
            _lower = _upper = BigInteger.Zero;
            foreach (var term in _buckets.Values.SelectMany(v => v))
            {
                (term.Lower, term.Upper) = Bounds(term.Numerator, term.Denominator, term.Kernel);
                _lower += term.Lower; _upper += term.Upper;
            }
        }
    }

    private (BigInteger Low, BigInteger High) Bounds(BigInteger a, BigInteger b, BigInteger kernel)
    {
        if (a.IsZero) return (BigInteger.Zero, BigInteger.Zero);
        var numerator = (a * a * kernel) << checked(2 * _precision);
        var denominator = b * b;
        var quotient = numerator / denominator;
        var low = quotient.IsZero ? BigInteger.Zero : ExactPopulationDeviation.IntegerRoot(quotient);
        var high = low * low * denominator == numerator ? low : low + 1;
        return a.Sign > 0 ? (low, high) : (-high, -low);
    }

    private static readonly int[] SmallPrimes = { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 };
    private static int Fingerprint(BigInteger kernel)
    {
        // Necessary invariants of a rational square class, only a lookup filter.
        // Every candidate match still needs the exact gcd/perfect-square proof above.
        var key = 0;
        foreach (var prime in SmallPrimes.Skip(1))
        {
            var value = kernel; var parity = 0;
            while (value % prime == 0) { value /= prime; parity ^= 1; }
            var character = BigInteger.ModPow(value % prime, (prime - 1) / 2, prime);
            key = (key << 2) | (parity << 1) | (character.IsOne ? 0 : 1);
        }
        return key;
    }

    internal void Reset()
    {
        _prices.Clear(); _buckets.Clear(); _sum = _squares = _weighted = _lower = _upper = BigInteger.Zero;
        _terms = 0; _precision = 64;
    }
}
