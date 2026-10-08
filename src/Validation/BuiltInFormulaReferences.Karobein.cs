using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> KarobeinOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return KarobeinValues(bars, Integer(options, "Length", 50), AverageKind(options, 3));
    }
    internal static Dictionary<string, double[]> KarobeinValues(IReadOnlyList<Bar> bars, int length, int kind = 3)
    {
        length = Math.Max(1, length);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period) => WindowedRationalAverage(values, period, kind);
        var mean = Mean(bars.Select(b => R(b.Close)).ToArray(), length);
        var ratio = mean.Select((value, i) => i == 0 || mean[i - 1].Sign == 0 ? R(0) : value / mean[i - 1]).ToArray();
        var fall = Mean(ratio.Select((value, i) => i > 0 && mean[i].CompareTo(mean[i - 1]) < 0 ? value : R(0)).ToArray(), length);
        var rise = Mean(ratio.Select((value, i) => i > 0 && mean[i].CompareTo(mean[i - 1]) > 0 ? value : R(0)).ToArray(), length);
        ReferenceFraction Clip(ReferenceFraction value) => value.Sign < 0 ? R(0) : value.CompareTo(R(1)) > 0 ? R(1) : value;
        var result = new double[bars.Count];
        for (var i = 0; i < result.Length; i++)
        {
            if (ratio[i].Sign == 0) continue;
            var sum = ratio[i] + rise[i];
            if (sum.Sign == 0) throw new ArgumentException("Independent Karobein rising denominator is zero.");
            var correction = Clip(ratio[i] / sum) * fall[i];
            var denominator = ratio[i] + correction;
            if (denominator.Sign == 0) throw new ArgumentException("Independent Karobein falling denominator is zero.");
            // Independent difference/sum identity for the final 2r/(r+c*a)-1 fold.
            result[i] = Clip((ratio[i] - correction) / denominator).ToDouble();
        }
        return new() { ["Ko"] = result };
    }
}
