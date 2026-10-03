using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> InstantaneousTrendOutputs(IReadOnlyList<Bar> bars, double alpha)
    {
        ReferenceFraction Round(ReferenceFraction value) => RoundRocBankStage(value);
        var a = ReferenceFraction.FromDouble(alpha); var square = Round(a * a); var retention = ReferenceFraction.FromDouble(1 - alpha);
        var coefficients = new[] { Round(a - Round(square / new ReferenceFraction(4))), Round(square / new ReferenceFraction(2)),
            Round(Round(square * new ReferenceFraction(3) / new ReferenceFraction(4)) - a), Round(retention * new ReferenceFraction(2)), new ReferenceFraction(0) - Round(retention * retention) };
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var line = new ReferenceFraction[bars.Count]; var signal = new double[bars.Count];
        ReferenceFraction At(ReferenceFraction[] values, int i) => i < 0 ? new ReferenceFraction(0) : values[i];
        for (var i = 0; i < bars.Count; i++)
        {
            if (i < 7) line[i] = Round((prices[i] + new ReferenceFraction(2) * At(prices, i - 1) + At(prices, i - 2)) / new ReferenceFraction(4));
            else
            {
                var inputs = new[] { prices[i], prices[i - 1], prices[i - 2], line[i - 1], line[i - 2] };
                var total = new ReferenceFraction(0); for (var j = 0; j < inputs.Length; j++) total += coefficients[j] * inputs[j]; line[i] = Round(total);
            }
            signal[i] = (new ReferenceFraction(2) * line[i] - At(line, i - 2)).ToDouble();
        }
        return new() { { "Eit", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal } };
    }
}
