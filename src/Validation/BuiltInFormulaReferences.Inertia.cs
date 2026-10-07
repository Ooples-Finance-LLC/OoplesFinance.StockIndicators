using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    // Zero denotes the partial-window least-squares endpoint, not a moving average.
    private static int InertiaReferenceKind(object options)
    {
        if (options.GetType().GetProperty("MaType")?.GetValue(options) is not MovingAvgType kind || kind == MovingAvgType.LinearRegression) return 0;
        var average = AverageKind(options, -1); return average == 0 ? -1 : average;
    }
    internal static IReadOnlyDictionary<string, double[]> InertiaOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return InertiaValues(bars, Integer(options, "SmoothLength", Integer(options, "Length", 20)), Integer(options, "RviLength", 14), InertiaReferenceKind(options)).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Rvi) InertiaValues(IReadOnlyList<Bar> bars, int length, int rviLength, int kind,
        double[][]? externalRvi = null, double[]? externalOutput = null)
    {
        length = Math.Max(1, length);
        var index = RelativeVolatilityOutputs(bars, true, 10, Math.Max(1, rviLength), 6, externalRvi)["Rvi"];
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var values = index.Select(R).ToArray(); double[] line;
        if (externalOutput is not null) line = externalOutput;
        else if (kind != 0) line = SmoothRocBankStage(values, length, kind).Select(v => v.ToDouble()).ToArray();
        else
        {
            // Prefix moments in absolute coordinates are independent of the rolling production recurrence.
            var sums = new ReferenceFraction[values.Length + 1]; var moments = new ReferenceFraction[values.Length + 1]; sums[0] = moments[0] = R(0);
            for (var i = 0; i < values.Length; i++) { sums[i + 1] = sums[i] + values[i]; moments[i + 1] = moments[i] + R(i) * values[i]; }
            line = values.Select((_, i) =>
            {
                var start = (int)Math.Max(0L, i - (long)length + 1); var count = i - start + 1; var n = R(count);
                var total = sums[i + 1] - sums[start]; var moment = moments[i + 1] - moments[start] - R(start) * total;
                var x = n * R(count - 1) / R(2); var xx = n * R(count - 1) * R(2L * count - 1) / R(6);
                var denominator = n * xx - x * x;
                var slope = denominator.Sign == 0 ? R(0) : (n * moment - x * total) / denominator;
                return (total / n + R(count - 1) * slope / R(2)).ToDouble();
            }).ToArray();
        }
        var signals = line.Select((v, i) =>
        {
            var change = R(v) - R(i == 0 ? 0 : line[i - 1]); var prior = R(i == 0 ? 0 : line[i - 1]) - R(i < 2 ? 0 : line[i - 2]);
            return change.Sign > 0 && change.CompareTo(prior) > 0 ? Signal.StrongBuy : change.Sign < 0 && change.CompareTo(prior) < 0 ? Signal.StrongSell
                : change.Sign > 0 ? Signal.Buy : change.Sign < 0 ? Signal.Sell : Signal.None;
        }).ToArray();
        return (new Dictionary<string, double[]> { ["Inertia"] = line }, signals, index);
    }
}
