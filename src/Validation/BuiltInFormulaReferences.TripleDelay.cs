using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> TripleDelayOutputs(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var first = new ReferenceFraction[bars.Count]; var second = new ReferenceFraction[bars.Count]; var raw = new ReferenceFraction[bars.Count];
        ReferenceFraction At(ReferenceFraction[] values, int i) => i < 0 ? new ReferenceFraction(0) : values[i];
        ReferenceFraction Round(ReferenceFraction value) => RoundRocBankStage(value);
        ReferenceFraction Product(ReferenceFraction value, double coefficient) => Round(value * ReferenceFraction.FromDouble(coefficient));
        for (var i = 0; i < bars.Count; i++)
        {
            first[i] = Round(ReferenceFraction.FromDouble(bars[i].Close) + Product(At(first, i - 6), .088));
            second[i] = Round(Round(Round(first[i] - At(first, i - 6)) + Product(At(second, i - 6), 1.2)) - Product(At(second, i - 12), .7));
            raw[i] = Round(Round(At(second, i - 12) - Product(At(second, i - 6), 2)) + second[i]);
        }
        ReferenceFraction[] Smooth(ReferenceFraction[] values)
        {
            if (kind != 7) return SmoothRocBankStage(values, Math.Max(1, length), kind);
            var result = new ReferenceFraction[values.Length];
            ReferenceFraction Price(int i) => values[Math.Max(0, i)];
            ReferenceFraction Lead(int i) => Round(new ReferenceFraction(2) * Price(i) - Price(i - 1));
            for (var i = 0; i < values.Length; i++)
            {
                var previous = i == 0 ? values[0] : result[i - 1]; var older = i < 2 ? previous : result[i - 2];
                result[i] = Round(Lead(i) * ReferenceFraction.FromDouble(.13785) + Lead(i - 1) * ReferenceFraction.FromDouble(.0007)
                    + Lead(i - 2) * ReferenceFraction.FromDouble(.13785) + previous * ReferenceFraction.FromDouble(1.2103) - older * ReferenceFraction.FromDouble(.4867));
            }
            return result;
        }
        var line = Smooth(raw); var signal = Smooth(line);
        return new() { { "Etdld", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
