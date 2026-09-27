namespace OoplesFinance.StockIndicators.Helpers;

internal static class CctRsiRatio
{
    internal static double Percent(double value, double numeratorLow, double denominatorLow, double denominatorHigh)
    {
#pragma warning disable S1244 // Only an exactly zero range uses the degenerate result; nonzero subnormal ranges remain meaningful.
        if (denominatorHigh == denominatorLow) return 0;
#pragma warning restore S1244
        var top = new ExactMeanAccumulator(); top.Add(value, 100); top.Add(numeratorLow, -100);
        var bottom = new ExactMeanAccumulator(); bottom.Add(denominatorHigh); bottom.Add(denominatorLow, -1);
        return top.Ratio(bottom);
    }
    internal static List<double> Smooth(List<double> values, MovingAvgType kind, int length)
    {
        using var mean = new StrengthAverage(kind, length, values.Count);
        return values.Select(v => mean.Next(new StrengthValue(v), true).Mantissa).ToList();
    }
}
