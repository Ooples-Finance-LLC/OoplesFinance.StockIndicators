using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> WellRoundedOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Math.Max(1, Integer(indicator.CreateOptions(), "Length", 14)); var gain = 2d / (length + 1d);
        var alpha = ReferenceFraction.FromDouble(gain); var beta = ReferenceFraction.FromDouble(.99);
        var gamma = ReferenceFraction.FromDouble(Math.Max(.01, Math.Min(.99, gain))); var one = new ReferenceFraction(1);
        var a = new ReferenceFraction(0); var b = a; var y = a; var mean = a; var residualY = a; var residualMean = a;
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            a = (a + alpha * residualY).RoundExtendedBinary64(); b = (b + alpha * residualMean).RoundExtendedBinary64();
            var drive = (a + b).RoundExtendedBinary64();
            y = (beta * drive + (one - beta) * y).RoundExtendedBinary64();
            mean = (gamma * y + (one - gamma) * mean).RoundExtendedBinary64();
            var price = ReferenceFraction.FromDouble(bars[i].Close); residualY = (price - y).RoundExtendedBinary64(); residualMean = (price - mean).RoundExtendedBinary64();
            output[i] = y.ToDouble();
        }
        return Outputs(("Wrma", output));
    }
}
