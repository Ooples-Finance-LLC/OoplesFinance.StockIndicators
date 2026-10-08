using System.Numerics;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Helpers;

// Exact zero test for rational combinations of sines/cosines at rational degree angles.
// A sparse cyclotomic tower avoids constructing a polynomial of astronomic degree.
internal static class ConfluenceRootRelations
{
    internal static long[] PrimeFactors(IEnumerable<long> values)
    {
        var factors = new HashSet<long> { 2, 3, 5 };
        foreach (var input in values)
        {
            var value = input;
            for (long prime = 2; prime <= value / prime; prime += prime == 2 ? 1 : 2)
            {
                if (value % prime != 0) continue;
                factors.Add(prime); while (value % prime == 0) value /= prime;
            }
            if (value > 1) factors.Add(value);
        }
        return factors.OrderByDescending(p => p).ToArray();
    }
    private static BigInteger Mod(BigInteger value, BigInteger modulus)
    { var result = value % modulus; return result.Sign < 0 ? result + modulus : result; }
    private static BigInteger Inverse(BigInteger value, BigInteger modulus)
    {
        if (modulus.IsOne) return BigInteger.Zero;
        var a = value; var b = modulus; var x = BigInteger.One; var y = BigInteger.Zero;
        while (!b.IsZero) { var q = a / b; (a, b) = (b, a - q * b); (x, y) = (y, x - q * y); }
        if (!a.IsOne) throw new InvalidOperationException("Cyclotomic factors must be coprime.");
        return Mod(x, modulus);
    }
    private static void Add(Dictionary<BigInteger, F> terms, BigInteger exponent, F coefficient)
    {
        if (terms.TryGetValue(exponent, out var previous)) coefficient += previous;
        if (coefficient.Sign == 0) terms.Remove(exponent); else terms[exponent] = coefficient;
    }
    internal static bool IsZero(F constant, IEnumerable<(F Degrees, F Sine, F Cosine)> source, IReadOnlyList<long> primes)
    {
        var terms = source.ToArray(); var order = new BigInteger(360);
        foreach (var term in terms)
        {
            var required = 360 * term.Degrees.Denominator;
            order = order / BigInteger.GreatestCommonDivisor(order, required) * required;
        }
        var polynomial = new Dictionary<BigInteger, F>(); Add(polynomial, 0, constant);
        foreach (var term in terms)
        {
            var exponent = term.Degrees.Numerator * (order / (360 * term.Degrees.Denominator));
            Add(polynomial, Mod(exponent, order), term.Cosine / 2);
            Add(polynomial, Mod(-exponent, order), term.Cosine / 2);
            // 1/i = -i = zeta^(3*order/4).
            Add(polynomial, Mod(exponent + 3 * order / 4, order), term.Sine / 2);
            Add(polynomial, Mod(-exponent + 3 * order / 4, order), -term.Sine / 2);
        }
        return Vanishes(polynomial, order, primes);
    }
    private static bool Vanishes(Dictionary<BigInteger, F> polynomial, BigInteger order, IReadOnlyList<long> primes)
    {
        if (polynomial.Count == 0) return true;
        if (order.IsOne) return false;
        var prime = primes.FirstOrDefault(p => order % p == 0);
        if (prime == 0) throw new InvalidOperationException("An angle denominator has an undeclared prime factor.");
        var power = BigInteger.One; var remainder = order;
        while (remainder % prime == 0) { power *= prime; remainder /= prime; }
        var stride = power / prime;
        var leftInverse = Inverse(remainder, power); var rightInverse = Inverse(power, remainder);
        var groups = new Dictionary<BigInteger, Dictionary<BigInteger, Dictionary<BigInteger, F>>>();
        foreach (var term in polynomial)
        {
            var left = Mod(term.Key * leftInverse, power); var right = Mod(term.Key * rightInverse, remainder);
            var row = left % stride; var column = left / stride;
            if (!groups.TryGetValue(row, out var columns)) groups[row] = columns = new();
            if (!columns.TryGetValue(column, out var coefficients)) columns[column] = coefficients = new();
            Add(coefficients, right, term.Value);
        }
        foreach (var group in groups.Values)
        {
            // Phi_(p^k)(x) = 1 + x^(p^(k-1)) + ... + x^((p-1)*p^(k-1)).
            // Each coefficient row must therefore agree across all p columns.
            if (group.Count < prime)
            {
                foreach (var coefficients in group.Values) if (!Vanishes(coefficients, remainder, primes)) return false;
            }
            else
            {
                var reference = group.Values.First();
                foreach (var coefficients in group.Values.Skip(1))
                {
                    var difference = new Dictionary<BigInteger, F>(coefficients);
                    foreach (var term in reference) Add(difference, term.Key, -term.Value);
                    if (!Vanishes(difference, remainder, primes)) return false;
                }
            }
        }
        return true;
    }
}
