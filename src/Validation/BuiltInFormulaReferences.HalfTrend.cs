using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HalfTrendOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return new Dictionary<string, double[]> { ["Ht"] = HalfTrendValues(bars, Integer(options, "Length", 2), 100, AverageKind(options, 1)).Values }; }
    internal static (double[] Values, Signal[] Signals) HalfTrendValues(IReadOnlyList<Bar> bars, int length, int atrLength, int kind, double[][]? external = null)
    {
        length = Math.Max(1, length); atrLength = Math.Max(1, atrLength); ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var highs = bars.Select(b => b.High).ToArray(); var lows = bars.Select(b => b.Low).ToArray();
        var ceilings = bars.Select((_, i) => Window(highs, i, length).Max()).ToArray(); var floors = bars.Select((_, i) => Window(lows, i, length).Min()).ToArray();
        var highMeans = external is null ? SmoothRocBankStage(highs.Select(R).ToArray(), length, kind) : external[1].Select(R).ToArray();
        var lowMeans = external is null ? SmoothRocBankStage(lows.Select(R).ToArray(), length, kind) : external[2].Select(R).ToArray();
        var ranges = bars.Select((b, i) => { var previous = R(i == 0 ? b.Close : bars[i - 1].Close); return new[] { R(b.High) - R(b.Low), (R(b.High) - previous).Abs(), (R(b.Low) - previous).Abs() }.Max().RoundExtendedBinary64(); }).ToArray();
        var atr = external is null ? SmoothRocBankStage(ranges, atrLength, kind) : external[0].Select(R).ToArray();
        var result = new double[bars.Count]; var signals = new Signal[bars.Count]; var seekingFall = false; var bullish = true; var segment = 0;
        for (var i = 0; i < bars.Count; i++)
        {
            var ceiling = ceilings.Skip(segment).Take(i - segment + 1).Min(); var floor = floors.Skip(segment).Take(i - segment + 1).Max(); var wasBullish = bullish;
            if (seekingFall && highMeans[i].CompareTo(R(floor)) < 0 && bars[i].Close < lows[i - 1])
            { bullish = false; seekingFall = false; segment = i; ceiling = ceilings[i]; }
            else if (!seekingFall && lowMeans[i].CompareTo(R(ceiling)) > 0 && bars[i].Close > (i == 0 ? highs[i] : highs[i - 1]))
            { bullish = true; seekingFall = true; segment = i; floor = floors[i]; }
            if (bullish && !seekingFall) floor = floors[0];
            var previous = i == 0 ? floors[i] : result[i - 1]; result[i] = bullish != wasBullish ? previous : bullish ? Math.Max(previous, floor) : Math.Min(previous, ceiling);
            var halfRange = (atr[i] / R(2)).RoundExtendedBinary64(); var arrow = R(result[i]) + R(bullish ? -1 : 1) * halfRange;
            signals[i] = bullish != wasBullish && arrow.Sign != 0 ? bullish ? Signal.Buy : Signal.Sell : Signal.None;
        }
        return (result, signals);
    }
}
