using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static double[] VariableAverageReference(double[] values, int length)
    {
        var changes = values.Select((v, i) => i == 0 ? 0m : decimal.Parse((v - values[i - 1]).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        decimal[] Smooth(decimal[] series)
        {
            var result = new decimal[series.Length];
            for (var i = 0; i < series.Length; i++) result[i] = ((i == 0 ? 0 : result[i - 1]) * (length - 1) + series[i]) / length;
            return result;
        }
        var up = Smooth(changes.Select(v => Math.Max(0, v)).ToArray());
        var down = Smooth(changes.Select(v => Math.Max(0, -v)).ToArray());
        var positive = Smooth(up.Select((v, i) => v + down[i] == 0 ? 0 : v / (v + down[i])).ToArray());
        var negative = Smooth(down.Select((v, i) => v + up[i] == 0 ? 0 : v / (v + up[i])).ToArray());
        var index = Smooth(positive.Select((v, i) => v + negative[i] == 0 ? 0 : Math.Abs(v - negative[i]) / (v + negative[i])).ToArray());
        var gains = index.Select((v, i) =>
        {
            var sample = Window(index, i, length).ToArray();
            var low = sample.Min(); var high = sample.Max(); var spread = high - low;
            var uncertainty = (decimal)(32 * Math.Pow(2, -52)) * Math.Max(Math.Abs(low), Math.Abs(high));
            return (double)(spread <= uncertainty || v - low <= uncertainty ? 0 : high - v <= uncertainty ? 1m / length : (v - low) / spread / length);
        }).ToArray();
        return ExpandedGainTrajectory(values, gains);
    }

    private static FormulaDefinition? VariableAverages(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        if (name is not (IndicatorName.VariableMovingAverage or IndicatorName.Svama or IndicatorName.VariableMovingAverageBands)) return null;
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 6);
        if (name == IndicatorName.VariableMovingAverage)
            return new("Vma", new[] { "Vma" }, bars => Outputs(("Vma", VariableAverageReference(Closes(bars), length))));
        if (name == IndicatorName.Svama)
            return new("Svama", new[] { "Svama" }, bars =>
            {
                var gains = bars.Select((b, i) =>
                {
                    var maximum = bars.Take(i + 1).Max(v => v.Volume);
                    return maximum == 0 ? 0 : b.Volume / maximum;
                }).ToArray();
                return Outputs(("Svama", ExpandedGainTrajectory(Closes(bars), gains)));
            });
        var variable = options.GetType().GetProperty("MaType")!.GetValue(options) is MovingAvgType.VariableMovingAverage;
        var kind = AverageKind(options, 0);
        if (!variable && kind == 0) return null;
        return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand" }, bars =>
        {
            var middle = variable ? VariableAverageReference(Closes(bars), length) : Average(Closes(bars), length, kind);
            var atr = variable ? VariableAverageReference(TrueRanges(bars), length) : Average(TrueRanges(bars), length, kind);
            var width = atr.Select(v => v * Number(options, 1.5, "Mult")).ToArray();
            return Outputs(("UpperBand", middle.Select((v, i) => v + width[i]).ToArray()), ("MiddleBand", middle),
                ("LowerBand", middle.Select((v, i) => v - width[i]).ToArray()));
        });
    }
}
