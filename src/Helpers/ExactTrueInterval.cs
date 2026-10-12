#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Helpers;

internal static class ExactTrueInterval
{
    internal static JurikDyadic Difference(double a, double b)
    {
        var value = JurikDyadic.FromDouble(a);
        value.Subtract(JurikDyadic.FromDouble(b));
        return value;
    }

    internal static JurikDyadic AbsoluteDifference(double a, double b) =>
        a >= b ? Difference(a, b) : Difference(b, a);

    internal static JurikDyadic Range(double high, double low, double previousClose)
    {
        if (high >= low)
            return Difference(Math.Max(high, previousClose), Math.Min(low, previousClose));
        // Preserve max(high-low, abs(high-close), abs(low-close)) even for
        // inverted finite candles. The signed high-low cannot win here.
        var range = AbsoluteDifference(high, previousClose);
        var other = AbsoluteDifference(low, previousClose);
        var comparison = range; comparison.Subtract(other);
        return comparison.Sign < 0 ? other : range;
    }
}
#endif
