using System.Numerics;
namespace OoplesFinance.StockIndicators.Validation;

// Independent oracle: ReferenceFraction arithmetic, prime-by-prime polynomial
// reduction, and Taylor expansions about multiples of 30 degrees in Q(sqrt(3)).
internal sealed class ConfluenceReferenceWave
{
    internal readonly struct R : IComparable<R>
    {
        private readonly ReferenceFraction _value;
        private ReferenceFraction Value => _value.Components.Denominator.IsZero ? new(0) : _value;
        internal R(ReferenceFraction value) => _value = value;
        internal BigInteger N => Value.Components.Numerator;
        internal BigInteger D => Value.Components.Denominator;
        internal int Sign => Value.Sign;
        internal double Publish() => Value.ToDouble();
        internal static R Of(double value) => new(ReferenceFraction.FromDouble(value));
        internal static R Ratio(BigInteger n, BigInteger d) => new(new ReferenceFraction(n) / new ReferenceFraction(d));
        internal R Abs() => new(Value.Abs());
        internal BigInteger Floor() { var q = BigInteger.DivRem(N, D, out var rem); return rem.Sign < 0 ? q - 1 : q; }
        public int CompareTo(R other) => Value.CompareTo(other.Value);
        public static implicit operator R(long n) => new(new ReferenceFraction(n));
        public static R operator +(R a, R b) => new(a.Value + b.Value);
        public static R operator -(R a, R b) => new(a.Value - b.Value);
        public static R operator -(R a) => (R)0 - a;
        public static R operator *(R a, R b) => new(a.Value * b.Value);
        public static R operator /(R a, R b) => new(a.Value / b.Value);
    }
    private readonly List<(R Angle, R Sin, R Cos)> _terms;
    private readonly R _constant;
    internal static ConfluenceReferenceWave Zero => new(0, new());
    private ConfluenceReferenceWave(R constant, List<(R, R, R)> terms) { _constant = constant; _terms = terms; }
    internal static ConfluenceReferenceWave Wave(R angle) => new(0, new() { (angle, 1, 1) });
    internal static ConfluenceReferenceWave Term(R angle, R sine, R cosine) => new(0, new() { (angle, sine, cosine) });
    internal static ConfluenceReferenceWave Constant(R value) => new(value, new());
    internal ConfluenceReferenceWave Times(R factor) => new(_constant * factor, _terms.Select(t => (t.Angle, t.Sin * factor, t.Cos * factor)).ToList());
    public static ConfluenceReferenceWave operator +(ConfluenceReferenceWave a, ConfluenceReferenceWave b)
        => new(a._constant + b._constant, a._terms.Concat(b._terms).ToList());
    public static ConfluenceReferenceWave operator -(ConfluenceReferenceWave a, ConfluenceReferenceWave b) => a + b.Times(-1);
    internal static long[] Factors(IEnumerable<long> values)
    {
        var result = new SortedSet<long> { 2, 3, 5 };
        foreach (var input in values)
        {
            var remaining = input;
            for (long divisor = 2; divisor * divisor <= remaining; divisor++)
                if (remaining % divisor == 0) { result.Add(divisor); do { remaining /= divisor; } while (remaining % divisor == 0); }
            if (remaining > 1) result.Add(remaining);
        }
        return result.ToArray();
    }
    private static BigInteger Mod(BigInteger a, BigInteger n) => (a % n + n) % n;
    private static void Add(Dictionary<BigInteger, R> poly, BigInteger exponent, R value)
    {
        poly.TryGetValue(exponent, out var old); value += old;
        if (value.Sign == 0) poly.Remove(exponent); else poly[exponent] = value;
    }
    private bool IsZero(long[] primes)
    {
        BigInteger order = 4;
        foreach (var t in _terms) { var d = 360 * t.Angle.D; order = order / BigInteger.GreatestCommonDivisor(order, d) * d; }
        var poly = new Dictionary<BigInteger, R>(); Add(poly, 0, _constant);
        foreach (var t in _terms)
        {
            var e = t.Angle.N * (order / (360 * t.Angle.D));
            Add(poly, Mod(e, order), t.Cos / 2); Add(poly, Mod(-e, order), t.Cos / 2);
            Add(poly, Mod(e - order / 4, order), t.Sin / 2); Add(poly, Mod(-e - order / 4, order), -t.Sin / 2);
        }
        var pending = new Stack<(Dictionary<BigInteger, R> Poly, BigInteger Order)>(); pending.Push((poly, order));
        while (pending.Count > 0)
        {
            (poly, order) = pending.Pop(); if (poly.Count == 0) continue;
            // Multiplication by a root and a common exponent divisor do not change zero.
            var offset = poly.Keys.Min(); var divisor = order;
            foreach (var e in poly.Keys) divisor = BigInteger.GreatestCommonDivisor(divisor, e - offset);
            poly = poly.ToDictionary(t => (t.Key - offset) / divisor, t => t.Value); order /= divisor;
            if (order.IsOne) return false;
            var p = primes.FirstOrDefault(p => order % p == 0);
            if (p == 0) throw new InvalidOperationException("Reference factor pool is incomplete.");
            var reduced = order / p;
            if (reduced % p == 0)
            {
                foreach (var group in poly.GroupBy(t => t.Key % p))
                    pending.Push((group.ToDictionary(t => t.Key / p, t => t.Value), reduced));
                continue;
            }
            // zeta_order = zeta_p^a * zeta_reduced^b; find a from the small prime,
            // then b=(1-a*reduced)/p, avoiding production's extended Euclidean tower.
            var a = BigInteger.ModPow(reduced % p, p - 2, p); var b = (1 - a * reduced) / p;
            var columns = new Dictionary<BigInteger, Dictionary<BigInteger, R>>();
            foreach (var t in poly)
            {
                var column = Mod(t.Key * a, p);
                if (!columns.TryGetValue(column, out var coefficients)) columns[column] = coefficients = new();
                Add(coefficients, Mod(t.Key * b, reduced), t.Value);
            }
            if (columns.Count < p) { foreach (var c in columns.Values) pending.Push((c, reduced)); }
            else
            {
                var last = columns[p - 1];
                foreach (var c in columns.Where(c => c.Key != p - 1))
                {
                    var difference = new Dictionary<BigInteger, R>(c.Value);
                    foreach (var t in last) Add(difference, t.Key, -t.Value);
                    pending.Push((difference, reduced));
                }
            }
        }
        return true;
    }
    private static (R A, R B) SinBasis(int index) => (((index % 12) + 12) % 12) switch
    {
        0 or 6 => ((R)0, (R)0), 1 or 5 => ((R)1 / 2, (R)0), 2 or 4 => ((R)0, (R)1 / 2),
        3 => ((R)1, (R)0), 7 or 11 => ((R)(-1) / 2, (R)0), 8 or 10 => ((R)0, (R)(-1) / 2), _ => ((R)(-1), (R)0)
    };
    private static (R Low, R High) Atan(int inverse, int bits)
    {
        R power = (R)1 / inverse, sum = 0; var square = power * power; var epsilon = R.Ratio(1, BigInteger.One << bits);
        for (var k = 0; ; k++)
        {
            var value = power / (2L * k + 1); sum += k % 2 == 0 ? value : -value; power *= square;
            var next = power / (2L * k + 3);
            if (next.CompareTo(epsilon) < 0) return k % 2 == 0 ? (sum - next, sum) : (sum, sum + next);
        }
    }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, (R PiLow, R PiHigh, R RootLow, R RootHigh)> Bounds = new();
    private static (R, R, R, R) Constants(int bits)
    {
        var a = Atan(2, bits + 8); var b = Atan(3, bits + 8); var scale = BigInteger.One << bits;
        R RoundDown(R r) => R.Ratio((r * R.Ratio(scale, 1)).Floor(), scale);
        R RoundUp(R r) => -RoundDown(-r);
        BigInteger low = scale, high = 2 * scale, target = 3 * scale * scale;
        while (high - low > 1) { var mid = (low + high) / 2; if (mid * mid <= target) low = mid; else high = mid; }
        return (RoundDown(4 * (a.Low + b.Low)), RoundUp(4 * (a.High + b.High)), R.Ratio(low, scale), R.Ratio(high, scale));
    }
    internal int Sign(long[] primes)
    {
        if (IsZero(primes)) return 0;
        var terms = _terms.Select(t =>
        {
            var angle = t.Angle - R.Ratio((t.Angle / 360).Floor() * 360, 1);
            var center = (int)((angle + 15) / 30).Floor();
            return (Delta: (angle - 30 * center) / 180, t.Sin, t.Cos, Center: center);
        }).ToArray();
        for (var bits = 128; ; bits = checked(bits * 2))
        {
            var c = Bounds.GetOrAdd(bits, Constants); var powers = terms.Select(_ => (R)1).ToArray();
            R low = 0, high = 0, piLowPower = 1, piHighPower = 1, factorial = 1;
            for (var k = 0; ; k++)
            {
                if (k > 0) { piLowPower *= c.PiLow; piHighPower *= c.PiHigh; factorial *= k; for (var i = 0; i < powers.Length; i++) powers[i] *= terms[i].Delta; }
                R rational = k == 0 ? _constant : (R)0, radical = 0, remainder = 0;
                for (var i = 0; i < terms.Length; i++)
                {
                    var t = terms[i]; var sin = SinBasis(t.Center + 3 * k); var cos = SinBasis(t.Center + 3 * k + 3);
                    rational += powers[i] * (t.Sin * sin.A + t.Cos * cos.A);
                    radical += powers[i] * (t.Sin * sin.B + t.Cos * cos.B);
                    remainder += (t.Sin.Abs() + t.Cos.Abs()) * (powers[i] * t.Delta).Abs();
                }
                var termLow = rational + radical * (radical.Sign >= 0 ? c.RootLow : c.RootHigh);
                var termHigh = rational + radical * (radical.Sign >= 0 ? c.RootHigh : c.RootLow);
                low += termLow * (termLow.Sign >= 0 ? piLowPower : piHighPower) / factorial;
                high += termHigh * (termHigh.Sign >= 0 ? piHighPower : piLowPower) / factorial;
                var error = remainder * piHighPower * c.PiHigh / (factorial * (k + 1));
                if ((low - error).Sign > 0) return 1;
                if ((high + error).Sign < 0) return -1;
                if (error.Sign == 0 || k > 10 && error.CompareTo((high - low) / 4) < 0) break;
            }
        }
    }
}
