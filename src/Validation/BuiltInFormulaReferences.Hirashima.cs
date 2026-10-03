using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HirashimaOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return HirashimaValues(bars, Integer(options, "Length", 1000), AverageKind(options, 2)).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Ema, double[] Residual, double[] Width) HirashimaValues(IReadOnlyList<Bar> bars, int length, int kind,
        IReadOnlyList<double>? externalEma = null, IReadOnlyList<double>? externalWidth = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        ReferenceFraction Round(ReferenceFraction value) => value.RoundExtendedBinary64();
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var ema = externalEma is null ? SmoothRocBankStage(prices, length, 3, Round) : externalEma.Select(R).ToArray();
        var residual = prices.Select((v, i) => Round(v - ema[i])).ToArray();
        var magnitude = residual.Select(v => v.Sign < 0 ? R(0) - v : v).ToArray();
        var width = externalWidth is null ? SmoothRocBankStage(magnitude, length, kind, Round) : externalWidth.Select(R).ToArray();
        ReferenceFraction[] Fit(ReferenceFraction[] values)
        {
            // Independently fit from prefix moments in global bar coordinates.
            // This avoids rescanning every 1000-bar window for every output rule.
            var sums = new ReferenceFraction[values.Length + 1]; var moments = new ReferenceFraction[values.Length + 1];
            sums[0] = moments[0] = R(0);
            for (var i = 0; i < values.Length; i++) { sums[i + 1] = sums[i] + values[i]; moments[i + 1] = moments[i] + R(i) * values[i]; }
            return values.Select((_, i) =>
            {
                var start = (int)Math.Max(0L, i - (long)length + 1); var n = R(i - start + 1);
                var sum = sums[i + 1] - sums[start]; var xy = moments[i + 1] - moments[start] - R(start) * sum;
                var x = n * (n - R(1)) / R(2); var xx = n * (n - R(1)) * (R(2) * n - R(1)) / R(6);
                var denominator = n * xx - x * x; var slope = denominator.Sign == 0 ? R(0) : (n * xy - x * sum) / denominator;
                return Round(sum / n + (n - R(1)) * slope / R(2));
            }).ToArray();
        }
        var first = Fit(residual); var remaining = prices.Select((v, i) => Round(v - ema[i] - first[i])).ToArray(); var second = Fit(remaining);
        var basis = ema.Select((v, i) => Round(v + first[i] + second[i] - (i == 0 ? R(0) : second[i - 1]))).ToArray();
        double[] Band(int step) => basis.Select((v, i) => (v + R(step) * width[i]).ToDouble()).ToArray();
        var trades = new Signal[bars.Count]; var previous = R(0);
        for (var i = 0; i < bars.Count; i++) { var margin = prices[i] - basis[i]; trades[i] = margin.Sign > 0 && margin.CompareTo(previous) > 0 ? Signal.StrongBuy : margin.Sign < 0 && margin.CompareTo(previous) < 0 ? Signal.StrongSell : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None; previous = margin; }
        return (new Dictionary<string, double[]> { ["UpperBand1"] = Band(1), ["UpperBand2"] = Band(2), ["MiddleBand"] = Band(0), ["LowerBand1"] = Band(-1), ["LowerBand2"] = Band(-2) }, trades,
            ema.Select(v => v.ToDouble()).ToArray(), residual.Select(v => v.ToDouble()).ToArray(), width.Select(v => v.ToDouble()).ToArray());
    }
}
