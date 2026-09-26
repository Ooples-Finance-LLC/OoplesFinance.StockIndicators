using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AtrTrailingOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return AtrTrailingOutputs(bars, 63, Math.Max(1, Integer(options, "Length", 14)), AverageKind(options, 3), Number(options, 3, "Multiplier"));
    }
    internal static IReadOnlyDictionary<string, double[]> AtrTrailingOutputs(IReadOnlyList<Bar> bars, int trendLength, int rangeLength, int kind, double multiplier)
    {
        var close = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var ranges = bars.Select((b, i) =>
        {
            var high = ReferenceFraction.FromDouble(b.High); var low = ReferenceFraction.FromDouble(b.Low); var previous = close[i == 0 ? 0 : i - 1];
            return new[] { high - low, (high - previous).Abs(), (low - previous).Abs() }.Max().RoundExtendedBinary64();
        }).ToArray();
        var atr = SmoothRocBankStage(ranges, rangeLength, kind); var trend = SmoothRocBankStage(close, trendLength, kind); var factor = ReferenceFraction.FromDouble(multiplier);
        var result = new double[bars.Count]; var stop = bars.Count == 0 ? new ReferenceFraction(0) : close[0];
        for (var i = 0; i < result.Length; i++)
        {
            var direction = close[i].CompareTo(trend[i]) > 0 ? 1 : -1;
            var boundary = (close[i] - new ReferenceFraction(direction) * factor * atr[i]).RoundExtendedBinary64();
            if (direction * boundary.CompareTo(stop) > 0) stop = boundary;
            result[i] = stop.ToDouble();
        }
        return Outputs(("Atrts", result));
    }
}
