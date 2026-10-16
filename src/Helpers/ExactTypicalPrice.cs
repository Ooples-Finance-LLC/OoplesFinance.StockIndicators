namespace OoplesFinance.StockIndicators.Helpers;

// A single correctly rounded HLC/3, including cancellation and extreme exponents.
internal static class ExactTypicalPrice
{
    internal static double Calculate(double high, double low, double close)
    {
        double pair = high + low;
        double sum = pair + close;
        if (!double.IsNaN(sum) && !double.IsInfinity(sum) && ExactAddition(high, low, pair) && ExactAddition(pair, close, sum))
            return sum == 0 ? 0 : sum / 3;
        var exact = new ExactMeanAccumulator();
        exact.Add(high); exact.Add(low); exact.Add(close);
        return exact.Mean(3);
    }

    private static bool ExactAddition(double a, double b, double sum)
    {
        // TwoSum recovers the addition residual. A zero residual certifies that
        // hardware division acts on the exact numerator, with only one rounding.
        double virtualB = sum - a;
        return (a - (sum - virtualB)) + (b - virtualB) == 0;
    }
}
