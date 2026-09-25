namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedFractionalEma
{
    internal static double Coefficient(double period, string parameterName)
    {
        if (double.IsNaN(period) || double.IsInfinity(period) || period <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "The fractional period must be finite and positive.");
        return 2 / (1 + period);
    }

    // DiNapoli starts at zero. Round the coefficient first and each complete update once.
    internal static double Next(double value, double previous, double coefficient)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || double.IsNaN(previous) || double.IsInfinity(previous))
            return double.NaN;
        var sum = new ExactMeanAccumulator();
        sum.Add(previous);
        sum.AddProduct(value, coefficient);
        sum.AddProduct(previous, coefficient, -1);
        return sum.Mean(1);
    }

    internal static double Percentage(double fast, double slow)
        => double.IsNaN(fast) || double.IsInfinity(fast) || double.IsNaN(slow) || double.IsInfinity(slow)
            ? double.NaN : RoundedPercentageChange.Of(fast, slow);
}
