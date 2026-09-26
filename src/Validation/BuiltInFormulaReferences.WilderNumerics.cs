namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedWilderTrajectory(IReadOnlyList<double> values, int length)
    {
        var result = new double[values.Count];
        var previous = new ReferenceFraction(0);
        for (var i = 0; i < values.Count; i++)
        {
            var correction = (ReferenceFraction.FromDouble(values[i]) - previous) / new ReferenceFraction(length);
            result[i] = (previous + correction).ToDouble();
            previous = ReferenceFraction.FromDouble(result[i]);
        }
        return result;
    }
}
