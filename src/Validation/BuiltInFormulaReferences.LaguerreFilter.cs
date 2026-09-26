using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> LaguerreFilterOutputs(IReadOnlyList<Bar> bars, double alpha)
    {
        var result = new double[bars.Count]; var a = ReferenceFraction.FromDouble(alpha); var gamma = new ReferenceFraction(1) - a;
        var old = Enumerable.Repeat(new ReferenceFraction(0), 4).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close);
            if (i == 0) old = Enumerable.Repeat(price, 4).ToArray();
            var stages = new ReferenceFraction[4]; stages[0] = (a * price + gamma * old[0]).RoundExtendedBinary64();
            for (var j = 1; j < 4; j++) stages[j] = (old[j - 1] + gamma * old[j] - gamma * stages[j - 1]).RoundExtendedBinary64();
            result[i] = ((stages[0] + new ReferenceFraction(2) * stages[1] + new ReferenceFraction(2) * stages[2] + stages[3]) / new ReferenceFraction(6)).ToDouble();
            old = stages;
        }
        return Outputs(("Elf", result));
    }
}
