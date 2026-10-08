using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TrendTraderOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return TrendTraderOutputs(bars, Math.Max(1, Integer(options, "Length", 21)), AverageKind(options, 2), Number(options, 3, "Mult"), Number(options, 20, "BandStep")); }
    internal static IReadOnlyDictionary<string, double[]> TrendTraderOutputs(IReadOnlyList<Bar> bars, int length, int kind, double mult, double step, double[]? externalAtr = null, double[]? externalAverage = null)
    {
        length = Math.Max(1, length); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var zero = new ReferenceFraction(0);
        var ranges = bars.Select((b, i) => { var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = prices[i == 0 ? 0 : i - 1]; return new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max().RoundExtendedBinary64(); }).ToArray();
        var atr = externalAtr is null ? SmoothRocBankStage(ranges, length, kind) : externalAtr.Select(ReferenceFraction.FromDouble).ToArray(); var raw = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = bars.Skip(Math.Max(0, i - length)).Take(Math.Min(i, length)); var highest = i == 0 ? zero : ReferenceFraction.FromDouble(previous.Max(b => b.Close)); var lowest = i == 0 ? zero : ReferenceFraction.FromDouble(previous.Min(b => b.Close));
            var width = (ReferenceFraction.FromDouble(mult) * (i == 0 ? zero : atr[i - 1])).RoundExtendedBinary64(); var highLimit = (highest - width).RoundExtendedBinary64(); var lowLimit = (lowest + width).RoundExtendedBinary64();
            raw[i] = prices[i].CompareTo(highLimit) > 0 && prices[i].CompareTo(lowLimit) > 0 ? highLimit : prices[i].CompareTo(highLimit) < 0 && prices[i].CompareTo(lowLimit) < 0 ? lowLimit : i == 0 ? zero : raw[i - 1];
        }
        var middle = externalAverage is null ? SmoothRocBankStage(raw, length, kind) : externalAverage.Select(ReferenceFraction.FromDouble).ToArray(); var offset = ReferenceFraction.FromDouble(step);
        return Outputs(("UpperBand", middle.Select(v => (v + offset).ToDouble()).ToArray()), ("MiddleBand", middle.Select(v => v.ToDouble()).ToArray()), ("LowerBand", middle.Select(v => (v - offset).ToDouble()).ToArray()), ("Raw", raw.Select(v => v.ToDouble()).ToArray()));
    }
}
