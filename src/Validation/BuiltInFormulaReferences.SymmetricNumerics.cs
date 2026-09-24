using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedSymmetricMean(IReadOnlyList<Bar> bars, int length) => RoundedSymmetricStage(Closes(bars), length);

    internal static double[] RoundedSymmetricStage(IReadOnlyList<double> values, int length)
    {
        var ascending = ((long)length + 1) / 2;
        var denominator = new ReferenceFraction(ascending * (length + 1L - ascending));
        var result = new double[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var sum = new ReferenceFraction(0);
            for (var lag = 0; lag < length && lag <= i; lag++)
            {
                var weight = lag < ascending ? lag + 1L : length - (long)lag;
                sum += ReferenceFraction.FromDouble(values[i - lag]) * new ReferenceFraction(weight);
            }
            result[i] = (sum / denominator).ToDouble();
        }
        return result;
    }

    internal static double[] RoundedJsaMean(IReadOnlyList<Bar> bars, int length) =>
        bars.Select((b, i) => ExactPriceMean(b.Close, i >= length ? bars[i - length].Close : 0)).ToArray();
}
