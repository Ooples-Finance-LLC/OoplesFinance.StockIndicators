namespace OoplesFinance.StockIndicators.Helpers;

internal static class GuppyRibbonArithmetic
{
    internal static double Distance(double first, double second, double third, double fourth, double fifth, double sixth)
    {
        ReadOnlySpan<double> values = stackalloc double[] { first, second, third, fourth, fifth, sixth };
        var sum = new ExactMeanAccumulator();
        for (var i = 1; i < values.Length; i++)
        {
            var sign = values[i] >= values[i - 1] ? 1 : -1;
            sum.Add(values[i], sign); sum.Add(values[i - 1], -sign);
        }
        return sum.Mean(1);
    }

    internal static double Mean(ReadOnlySpan<double> values)
    {
        var sum = new ExactMeanAccumulator();
        foreach (var value in values) sum.Add(value);
        return sum.Mean(values.Length);
    }

    // Both ribbons have already been rounded. Preserve their difference before division.
    internal static double Percent(double fast, double slow)
    {
        if (slow == 0) return 0;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(fast, 100); numerator.Add(slow, -100);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(slow);
        return numerator.Ratio(denominator);
    }
}
