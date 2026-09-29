using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? OptimizedTrendFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not OptimizedTrendTrackerSpecOptions options) return null;
        var kind = AverageKind(options, 0); if (kind == 0 && options.MaType != MovingAvgType.VariableIndexDynamicAverage) return null;
        return new("Ott", new[] { "Ott" }, bars => OptimizedTrendOutputs(bars, indicator));
    }
    internal static IReadOnlyDictionary<string, double[]> OptimizedTrendOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = (OptimizedTrendTrackerSpecOptions)indicator.CreateOptions(); return new Dictionary<string, double[]> { ["Ott"] = OptimizedTrendValues(bars, options.Length, options.MaType == MovingAvgType.VariableIndexDynamicAverage ? 19 : AverageKind(options, 1), options.Percent).Values }; }
    internal static (double[] Values, Signal[] Signals) OptimizedTrendValues(IReadOnlyList<Bar> bars, int length, int kind, double percent, double[]? external = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var average = external is not null ? external.Select(R).ToArray() : kind == 19 ? RoundedVidya(bars, length).Select(R).ToArray() : SmoothRocBankStage(bars.Select(b => R(b.Close)).ToArray(), length, kind);
        var result = new double[bars.Count]; var signals = new Signal[bars.Count]; var before = R(0);
        var lowerCandidates = new List<ReferenceFraction>(); var upperCandidates = new List<ReferenceFraction>(); var events = new List<bool> { true };
        for (var i = 0; i < bars.Count; i++)
        {
            var value = average[i];
            if (i > 0)
            {
                var lower = lowerCandidates.Max(); var upper = upperCandidates.Min();
                if (events.Last() && value.CompareTo(lower) < 0) events.Add(false); else if (!events.Last() && value.CompareTo(upper) > 0) events.Add(true);
                if (value.CompareTo(lower) <= 0) lowerCandidates.Clear(); if (value.CompareTo(upper) >= 0) upperCandidates.Clear();
            }
            var distance = (value.Abs() * R(percent) / R(100)).RoundExtendedBinary64(); lowerCandidates.Add((value - distance).RoundExtendedBinary64()); upperCandidates.Add((value + distance).RoundExtendedBinary64());
            var stop = events.Last() ? lowerCandidates.Max() : upperCandidates.Min(); var line = (stop * (R(200) + R(value.CompareTo(stop) > 0 ? 1 : -1) * R(percent)) / R(200)).RoundExtendedBinary64();
            result[i] = line.ToDouble(); var difference = R(bars[i].Close) - line;
            signals[i] = difference.Sign > 0 && difference.CompareTo(before) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(before) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None; before = difference;
        }
        return (result, signals);
    }
}
