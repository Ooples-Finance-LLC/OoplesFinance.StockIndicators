using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal static class PrimeNumberSearch
{
    internal const double Minimum = -9223372036854775808d;
    internal const double Maximum = 9223372036854774784d;
    internal static void Validate(double value)
    {
        StreamingInputValidation.Finite(value, nameof(value));
        if (value < Minimum || value > Maximum) throw new ArgumentOutOfRangeException(nameof(value), "Prime search prices must fit signed 64-bit integers.");
    }
    internal static bool IsPrime(long candidate)
    {
        if (candidate < 2) return false;
        foreach (var prime in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
            if (candidate % prime == 0) return candidate == prime;
        var odd = candidate - 1; var powers = 0;
        while ((odd & 1) == 0) { odd >>= 1; powers++; }
        // Deterministic strong probable-prime witness set for every unsigned 64-bit integer.
        foreach (var witness in new long[] { 2, 325, 9375, 28178, 450775, 9780504, 1795265022 })
        {
            var basis = witness % candidate;
            if (basis == 0) continue;
            var residue = BigInteger.ModPow(basis, odd, candidate);
            if (residue == 1 || residue == candidate - 1) continue;
            var composite = true;
            for (var j = 1; j < powers; j++)
            {
                residue = residue * residue % candidate;
                if (residue == candidate - 1) { composite = false; break; }
            }
            if (composite) return false;
        }
        return true;
    }
    private static long Endpoint(double value) => value >= 9223372036854775808d ? long.MaxValue
        : value <= Minimum ? long.MinValue : (long)Math.Round(value);
    internal static (long Upper, long Lower) Find(double value, int tolerance)
    {
        Validate(value);
        var center = (long)Math.Round(value);
        var radius = value * Math.Max(1, tolerance) / 100;
        var high = Endpoint(value + radius); var low = Math.Max(2, Endpoint(value - radius));
        long upper = 0, lower = 0;
        for (var candidate = Math.Max(2, center); candidate <= high; candidate++)
        {
            if (IsPrime(candidate)) { upper = candidate; break; }
            if (candidate == long.MaxValue) break;
        }
        for (var candidate = center; candidate >= low; candidate--)
            if (IsPrime(candidate)) { lower = candidate; break; }
        return (upper, lower);
    }
}
