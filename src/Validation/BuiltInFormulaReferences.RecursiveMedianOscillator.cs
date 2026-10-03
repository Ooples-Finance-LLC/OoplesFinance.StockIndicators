using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] RecursiveMedianOscillatorValues(IReadOnlyList<Bar> bars, int length1, int length2 = 12, int length3 = 30)
    {
        var smooth = RecursiveMedianValues(bars, length1, length2).Select(ReferenceFraction.FromDouble).ToArray();
        var angle = Math.Min(.99, Math.Max(.01, 1 / Math.Sqrt(2) * 2 * Math.PI / Math.Max(1, length3)));
        var alpha = (Math.Cos(angle) + Math.Sin(angle) - 1) / Math.Cos(angle);
        var drive = ReferenceFraction.FromDouble(Math.Pow(1 - alpha / 2, 2));
        var feedback = ReferenceFraction.FromDouble(2 * (1 - alpha)); var decay = ReferenceFraction.FromDouble(Math.Pow(1 - alpha, 2));
        var values = new ReferenceFraction[bars.Count]; var result = new double[bars.Count];
        ReferenceFraction Previous(ReferenceFraction[] series, int i) => i < 0 ? new ReferenceFraction(0) : series[i];
        for (var i = 0; i < bars.Count; i++)
        {
            var change = RoundRocBankStage(smooth[i] - new ReferenceFraction(2) * Previous(smooth, i - 1) + Previous(smooth, i - 2));
            values[i] = RoundRocBankStage(RoundRocBankStage(drive * change) + RoundRocBankStage(feedback * Previous(values, i - 1)) - RoundRocBankStage(decay * Previous(values, i - 2)));
            result[i] = values[i].ToDouble();
        }
        return result;
    }
}
