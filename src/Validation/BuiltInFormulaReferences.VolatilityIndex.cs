using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VolatilityIndexOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions(); var prefix = indicator.BatchName == IndicatorName.ChandeVolatilityIndexDynamicAverageIndicator ? "Cvida" : "Vida";
        return VolatilityIndexValues(bars, Integer(o, "Length", 20), AverageKind(o, 3), Number(o, .2, "Alpha1"), Number(o, .04, "Alpha2"), prefix).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, double[] Deviation, Signal[] Signals) VolatilityIndexValues(IReadOnlyList<Bar> bars, int length, int kind, double alpha1, double alpha2, string prefix = "Vida", double[]? external = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v); var prices = bars.Select(b => R(b.Close)).ToArray(); var deviation = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (i < length - 1) continue; var window = Window(prices, i, length).ToArray(); var mean = window.Aggregate(R(0), (a, b) => a + b) / new ReferenceFraction(length);
            deviation[i] = (window.Aggregate(R(0), (a, b) => a + (b - mean) * (b - mean)) / new ReferenceFraction(length)).SqrtToDouble();
        }
        var averages = external is null ? SmoothRocBankStage(deviation.Select(R).ToArray(), length, kind) : external.Select(R).ToArray();
        var first = new double[bars.Count]; var second = new double[bars.Count]; var signals = new Signal[bars.Count]; var level1 = R(0); var level2 = R(0); var previousBull = R(0); var previousBear = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            if (i == 0) level1 = level2 = prices[i]; var ratio = averages[i].Sign == 0 ? R(0) : R(deviation[i]) / averages[i];
            level1 = (level1 + R(alpha1) * ratio * (prices[i] - level1)).RoundExtendedBinary64(); level2 = (level2 + R(alpha2) * ratio * (prices[i] - level2)).RoundExtendedBinary64();
            first[i] = level1.ToDouble(); second[i] = level2.ToDouble(); var bull = prices[i] - (level1.CompareTo(level2) > 0 ? level1 : level2); var bear = prices[i] - (level1.CompareTo(level2) < 0 ? level1 : level2);
            signals[i] = bull.Sign > 0 && bull.CompareTo(previousBull) > 0 ? Signal.StrongBuy : bear.Sign < 0 && bear.CompareTo(previousBear) < 0 ? Signal.StrongSell : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
            previousBull = bull; previousBear = bear;
        }
        return (new Dictionary<string, double[]> { [prefix + "1"] = first, [prefix + "2"] = second }, deviation, signals);
    }
}
