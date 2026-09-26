using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? RangeMomentum(IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        if (indicator.BatchName == IndicatorName.VolatilityBasedMomentum)
        {
            var kind = AverageKind(options, 6);
            if (kind == 0) return null;
            return new("Vbm", new[] { "Vbm", "Signal" }, bars =>
            {
                var lag = Integer(options, "Length1");
                var range = Average(TrueRanges(bars), Integer(options, "Length2"), kind);
                var line = bars.Select((b, i) => i < lag || range[i] == 0 ? 0
                    : (b.Close - bars[i - lag].Close) / range[i]).ToArray();
                return Outputs(("Vbm", line), ("Signal", Average(line, lag, kind)));
            });
        }
        if (indicator.BatchName == IndicatorName.VolatilityQualityIndex)
        {
            var kind = AverageKind(options, 1);
            if (kind == 0) return null;
            return new("Vqi", new[] { "Vqi", "FastSignal", "SlowSignal" }, bars =>
            {
                var ranges = TrueRanges(bars);
                var contributions = new double[bars.Count];
                double quality = 0;
                for (var i = 0; i < bars.Count; i++)
                {
                    var b = bars[i];
                    var change = i == 0 ? 0 : b.Close - bars[i - 1].Close;
                    var body = b.Close - b.Open;
                    if (ranges[i] != 0 && b.High != b.Low) // NOSONAR: S1244 - Only exactly zero denominators select the degenerate-range branch.
                        quality = (change / ranges[i] + body / (b.High - b.Low)) / 2;
                    contributions[i] = Math.Abs(quality) * (change + body) / 2;
                }
                var line = contributions.Select((_, i) => contributions.Take(i + 1).Sum()).ToArray();
                return Outputs(("Vqi", line), ("FastSignal", Average(line, Integer(options, "FastLength"), kind)),
                    ("SlowSignal", Average(line, Integer(options, "SlowLength"), kind)));
            });
        }
        return null;
    }
}
