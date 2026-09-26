namespace OoplesFinance.StockIndicators.Helpers;

internal static class PrimeNumberSearch
{
    internal static bool IsPrime(long candidate)
    {
        if (candidate < 2) return false;
        if (candidate % 2 == 0) return candidate == 2;
        if (candidate % 3 == 0) return candidate == 3;
        for (long divisor = 5; divisor <= candidate / divisor; divisor += 6)
            if (candidate % divisor == 0 || candidate % (divisor + 2) == 0) return false;
        return true;
    }

    internal static (long Upper, long Lower) Find(double value, int tolerance)
    {
        var center = (long)Math.Round(value);
        var radius = value * Math.Max(1, tolerance) / 100;
        var high = (long)Math.Round(value + radius);
        var low = Math.Max(2, (long)Math.Round(value - radius));
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
