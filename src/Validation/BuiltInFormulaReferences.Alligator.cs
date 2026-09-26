using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AlligatorOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var leg = indicator.BatchOutputKey; var length = Integer(options, "Length", 13); var kind = AverageKind(options, 6);
        var prices = indicator is IIndicator { Source: not null } ? bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray()
            : bars.Select(b => ReferenceFraction.FromDouble(((ReferenceFraction.FromDouble(b.High) + ReferenceFraction.FromDouble(b.Low)) / new ReferenceFraction(2)).ToDouble())).ToArray();
        double[] Line(string name, int fallbackLength, int fallbackOffset)
        {
            var period = Integer(options, name + "Length", leg == (name == "Jaw" ? "Jaws" : name) ? length : fallbackLength);
            var delay = Math.Max(0, Integer(options, name + "Offset", fallbackOffset));
            var average = SmoothRocBankStage(prices, Math.Max(1, period), kind);
            return Enumerable.Range(0, bars.Count).Select(i => i < delay ? 0 : average[i - delay].ToDouble()).ToArray();
        }
        return Outputs(("Jaws", Line("Jaw", 13, 8)), ("Teeth", Line("Teeth", 8, 5)), ("Lips", Line("Lips", 5, 3)));
    }
}
