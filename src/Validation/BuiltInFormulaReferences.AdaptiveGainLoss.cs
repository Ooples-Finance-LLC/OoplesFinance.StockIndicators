using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AdaptiveGainLossOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var length = Integer(options, "UpLength", Integer(options, "Length", 14));
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var zero = new ReferenceFraction(0);
        var rapid = indicator.BatchName == IndicatorName.RapidRelativeStrengthIndex;
        var changes = prices.Select((v, i) => i == 0 || !rapid && prices[i - 1].Sign == 0 ? zero : rapid
            ? RoundStrengthStage(v - prices[i - 1]) : RoundRocBankStage((v - prices[i - 1]) * new ReferenceFraction(100) / prices[i - 1])).ToArray();
        var up = zero; var down = zero;
        var line = new double[bars.Count];
        for (var i = 0; i < line.Length; i++)
        {
            var window = changes.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).ToArray();
            if (rapid)
            {
                up = zero; down = zero;
                foreach (var change in window) { if (change.Sign > 0) up += change; else down -= change; }
            }
            else
            {
                var count = window.Count(v => v.Sign >= 0);
                if (count > 0) up = RoundRocBankStage((up * new ReferenceFraction(count - 1) + (changes[i].Sign > 0 ? changes[i] : zero)) / new ReferenceFraction(count));
                count = length - count;
                if (count > 0) down = RoundRocBankStage((down * new ReferenceFraction(count - 1) + (changes[i].Sign < 0 ? zero - changes[i] : zero)) / new ReferenceFraction(count));
            }
            line[i] = (up + down).Sign == 0 ? 100 : (new ReferenceFraction(100) * up / (up + down)).ToDouble();
        }
        if (!rapid) return Outputs(("Arsi", line));
        var signal = SmoothStrengthStage(line.Select(ReferenceFraction.FromDouble).ToArray(), length, AverageKind(options, 3)).Select(v => v.ToDouble()).ToArray();
        return Outputs(("Rrsi", line), ("Signal", signal));
    }
}
