namespace OoplesFinance.StockIndicators.Helpers;

// Component mean and deviation are rounded once; subsequent operations are exact until output.
internal static class BollingerArithmetic
{
    internal static void Mean(ReadOnlySpan<double> input, Span<double> output, int length)
    {
        using var mean = new Streaming.RoundedSimpleMovingAverageSmoother(length);
        for (var i = 0; i < input.Length; i++) output[i] = mean.Next(input[i], true);
    }
    internal static List<double> Mean(List<double> input, int length)
    {
        var result = new List<double>(input.Count);
        using var mean = new Streaming.RoundedSimpleMovingAverageSmoother(length);
        foreach (var value in input) result.Add(mean.Next(value, true));
        return result;
    }
    internal static double Band(double middle, double deviation, double multiplier)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(middle); sum.AddProduct(deviation, multiplier);
        return sum.Mean(1);
    }
    internal static double Width(double middle, double deviation, double multiplier)
    {
        var numerator = new ExactMeanAccumulator(); numerator.AddProduct(deviation, multiplier, 2);
        var denominator = new ExactMeanAccumulator(); denominator.Add(middle);
        return numerator.Ratio(denominator);
    }
    internal static double Percent(double value, double middle, double deviation, double multiplier)
    {
        if (deviation == 0 || multiplier == 0) return 0;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(value, 100); numerator.Add(middle, -100); numerator.AddProduct(deviation, multiplier, 100);
        var denominator = new ExactMeanAccumulator(); denominator.AddProduct(deviation, multiplier, 2);
        return numerator.Ratio(denominator);
    }
}
