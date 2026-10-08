using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ParametricKalmanOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length);
        var output = new double[bars.Count];
        var error = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close);
            var previousPrice = i == 0 ? price : ReferenceFraction.FromDouble(bars[i - 1].Close);
            var previousEstimate = i == 0 ? price : ReferenceFraction.FromDouble(output[i - 1]);
            var baseline = i < length ? previousPrice : ReferenceFraction.FromDouble(output[i - length]);
            var distance = (price - baseline).Abs();
            var sum = distance + error;
            var gain = sum.Sign == 0 ? new ReferenceFraction(1) : error / sum;
            output[i] = (previousEstimate + gain * (price - previousEstimate)).ToDouble();
            error = ((new ReferenceFraction(1) - gain) * (price - previousPrice).Abs()).RoundExtendedBinary64();
        }
        return Outputs(("Pkf", output));
    }
}
