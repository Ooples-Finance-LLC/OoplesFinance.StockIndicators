using System.Collections.Concurrent;
using System.Numerics;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Helpers;

// Linear combinations of exact sine/cosine values at rational degree angles.
// The sparse algebraic zero test terminates true ties; rational Taylor bounds
// certify the sign of every nonzero combination without a tolerance shortcut.
internal sealed class ConfluenceTrigonometry
{
    private sealed class FractionComparer : IEqualityComparer<F>
    {
        public bool Equals(F a, F b) => a.CompareTo(b) == 0;
        public int GetHashCode(F a) => a.Numerator.GetHashCode() ^ a.Denominator.GetHashCode();
    }
    private static readonly FractionComparer Comparer = new();
    private static readonly ConcurrentDictionary<int, (F Low, F High)> Pi = new();
    private static readonly ConcurrentDictionary<(int Angle, int Bits), (F SinLow, F SinHigh, F CosLow, F CosHigh)> Basis = new();
    private readonly F _constant;
    private readonly Dictionary<F, (F Sine, F Cosine)> _terms;
    internal static ConfluenceTrigonometry Zero { get; } = new(0, new(Comparer));
    private ConfluenceTrigonometry(F constant, Dictionary<F, (F Sine, F Cosine)> terms) { _constant = constant; _terms = terms; }
    internal static ConfluenceTrigonometry Constant(F value) => new(value, new(Comparer));
    internal static ConfluenceTrigonometry Wave(F degrees, F sine, F cosine)
    {
        var turns = (degrees / 360).Floor();
        var angle = degrees - new F(360 * turns, BigInteger.One);
        if (angle > (F)180) { angle = 360 - angle; sine = -sine; }
        if (angle > (F)90) { angle = 180 - angle; cosine = -cosine; }
        if (angle.Sign == 0) return Constant(cosine);
        if (angle.CompareTo((F)90) == 0) return Constant(sine);
        if (sine.Sign == 0 && cosine.Sign == 0) return Zero;
        return new(0, new(Comparer) { [angle] = (sine, cosine) });
    }
    internal ConfluenceTrigonometry Times(F factor)
    {
        if (factor.Sign == 0) return Zero;
        return new(_constant * factor, _terms.ToDictionary(t => t.Key, t => (t.Value.Sine * factor, t.Value.Cosine * factor), Comparer));
    }
    public static ConfluenceTrigonometry operator +(ConfluenceTrigonometry a, ConfluenceTrigonometry b)
    {
        var terms = new Dictionary<F, (F Sine, F Cosine)>(a._terms, Comparer);
        foreach (var term in b._terms)
        {
            terms.TryGetValue(term.Key, out var before);
            var sine = before.Sine + term.Value.Sine; var cosine = before.Cosine + term.Value.Cosine;
            if (sine.Sign == 0 && cosine.Sign == 0) terms.Remove(term.Key); else terms[term.Key] = (sine, cosine);
        }
        return new(a._constant + b._constant, terms);
    }
    public static ConfluenceTrigonometry operator -(ConfluenceTrigonometry a, ConfluenceTrigonometry b) => a + b.Times(-1);
    internal int Sign(IReadOnlyList<long> primes)
    {
        if (_terms.Count == 0) return _constant.Sign;
        if (ConfluenceRootRelations.IsZero(_constant, _terms.Select(t => (t.Key, t.Value.Sine, t.Value.Cosine)), primes)) return 0;
        // Nearby angles share an integer center. Combining their Taylor coefficients
        // before evaluation preserves tiny residuals and exact low-order cancellation.
        var centers = _terms.Keys.Select(a => (int)Math.Round(a.Publish())).ToArray();
        var deltas = _terms.Keys.Select((a, i) => (a - (F)centers[i]) / 180).ToArray();
        var coefficients = _terms.Values.ToArray();
        for (var bits = 128; ; bits = checked(bits * 2))
        {
            var pi = Pi.GetOrAdd(bits, PiBounds);
            var powers = deltas.Select(_ => (F)1).ToArray();
            F low = 0, high = 0, lowPower = 1, highPower = 1, factorial = 1;
            for (var degree = 0; ; degree++)
            {
                if (degree > 0)
                {
                    lowPower *= pi.Low; highPower *= pi.High; factorial *= (F)degree;
                    for (var i = 0; i < powers.Length; i++) powers[i] *= deltas[i];
                }
                var groups = new Dictionary<int, (F Sine, F Cosine)>(); F absoluteRemainder = 0;
                for (var i = 0; i < powers.Length; i++)
                {
                    var sine = (degree % 2 == 0 ? coefficients[i].Sine : -coefficients[i].Cosine) * powers[i];
                    var cosine = (degree % 2 == 0 ? coefficients[i].Cosine : coefficients[i].Sine) * powers[i];
                    if (degree % 4 is 2 or 3) { sine = -sine; cosine = -cosine; }
                    groups.TryGetValue(centers[i], out var previous);
                    sine += previous.Sine; cosine += previous.Cosine;
                    if (sine.Sign == 0 && cosine.Sign == 0) groups.Remove(centers[i]); else groups[centers[i]] = (sine, cosine);
                    absoluteRemainder += (coefficients[i].Sine.Abs() + coefficients[i].Cosine.Abs()) * (powers[i] * deltas[i]).Abs();
                }
                var term = BasisCombination(degree == 0 ? _constant : (F)0, groups, bits, primes);
                low += term.Low * (term.Low.Sign >= 0 ? lowPower : highPower) / factorial;
                high += term.High * (term.High.Sign >= 0 ? highPower : lowPower) / factorial;
                var error = absoluteRemainder * highPower * pi.High / (factorial * new F((long)degree + 1, BigInteger.One));
                if ((low - error).Sign > 0) return 1;
                if ((high + error).Sign < 0) return -1;
                // Refine the transcendental bounds once their uncertainty dominates.
                if (error.Sign == 0 || degree >= 8 && error < (high - low) / 8) break;
            }
        }
    }
    private static (F Low, F High) BasisCombination(F constant, Dictionary<int, (F Sine, F Cosine)> groups, int bits, IReadOnlyList<long> primes)
    {
        if (groups.Count == 0) return (constant, constant);
        if (ConfluenceRootRelations.IsZero(constant, groups.Select(t => ((F)t.Key, t.Value.Sine, t.Value.Cosine)), primes)) return ((F)0, (F)0);
        var low = constant; var high = constant;
        foreach (var group in groups)
        {
            var basis = Basis.GetOrAdd((group.Key, bits), key => BasisBounds(key.Angle, key.Bits));
            var sine = group.Value.Sine; var cosine = group.Value.Cosine;
            low += sine * (sine.Sign >= 0 ? basis.SinLow : basis.SinHigh) + cosine * (cosine.Sign >= 0 ? basis.CosLow : basis.CosHigh);
            high += sine * (sine.Sign >= 0 ? basis.SinHigh : basis.SinLow) + cosine * (cosine.Sign >= 0 ? basis.CosHigh : basis.CosLow);
        }
        return (low, high);
    }
    private static (F SinLow, F SinHigh, F CosLow, F CosHigh) BasisBounds(int degrees, int bits)
    {
        if (degrees == 0) return ((F)0, (F)0, (F)1, (F)1);
        if (degrees == 90) return ((F)1, (F)1, (F)0, (F)0);
        var pi = Pi.GetOrAdd(bits + 16, PiBounds); var unit = new F(degrees, 180);
        var xLow = pi.Low * unit; var xHigh = pi.High * unit;
        F sinLow = 0, sinHigh = 0, cosLow = 1, cosHigh = 1, powerLow = 1, powerHigh = 1, factorial = 1;
        var target = F.Grid(bits + 4); var grid = F.Grid(bits);
        F Down(F value) => new F((value / grid).Floor(), BigInteger.One) * grid;
        F Up(F value) => new F((value / grid).Ceiling(), BigInteger.One) * grid;
        for (var degree = 1; ; degree++)
        {
            powerLow *= xLow; powerHigh *= xHigh; factorial *= (F)degree;
            var positive = degree % 4 is 0 or 1;
            var low = (positive ? powerLow : -powerHigh) / factorial;
            var high = (positive ? powerHigh : -powerLow) / factorial;
            if (degree % 2 == 0) { cosLow += low; cosHigh += high; } else { sinLow += low; sinHigh += high; }
            var error = powerHigh * xHigh / (factorial * new F((long)degree + 1, BigInteger.One));
            if (sinHigh - sinLow + 2 * error < target && cosHigh - cosLow + 2 * error < target)
                return (Down(sinLow - error), Up(sinHigh + error), Down(cosLow - error), Up(cosHigh + error));
        }
    }
    private static (F Low, F High) PiBounds(int bits)
    {
        var first = ArcTangent(5, bits + 8); var second = ArcTangent(239, bits + 8);
        var low = 16 * first.Low - 4 * second.High; var high = 16 * first.High - 4 * second.Low;
        var grid = F.Grid(bits);
        return (new F((low / grid).Floor(), BigInteger.One) * grid, new F((high / grid).Ceiling(), BigInteger.One) * grid);
    }
    private static (F Low, F High) ArcTangent(int inverse, int bits)
    {
        var argument = new F(1, inverse); var square = argument * argument;
        var power = argument; F sum = 0; var epsilon = F.Grid(bits);
        for (var index = 0; ; index++)
        {
            var term = power / new F(2L * index + 1, BigInteger.One);
            sum += index % 2 == 0 ? term : -term;
            power *= square;
            var next = power / new F(2L * index + 3, BigInteger.One);
            if (next < epsilon) return index % 2 == 0 ? (sum - next, sum) : (sum, sum + next);
        }
    }
}
