using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DirectionalIndexOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return DirectionalIndexOutputs(bars, Math.Max(1, Integer(options, "Length", 14)), AverageKind(options, 6));
    }
    internal static IReadOnlyDictionary<string, double[]> DirectionalIndexOutputs(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var zero = new ReferenceFraction(0); var hundred = new ReferenceFraction(100);
        var positive = new ReferenceFraction[bars.Count]; var negative = new ReferenceFraction[bars.Count]; var ranges = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var high = ReferenceFraction.FromDouble(bars[i].High); var low = ReferenceFraction.FromDouble(bars[i].Low);
            var previousHigh = ReferenceFraction.FromDouble(bars[i == 0 ? i : i - 1].High); var previousLow = ReferenceFraction.FromDouble(bars[i == 0 ? i : i - 1].Low);
            var previousClose = ReferenceFraction.FromDouble(bars[i == 0 ? i : i - 1].Close);
            var up = high - previousHigh; var down = previousLow - low;
            positive[i] = up.Sign > 0 && up.CompareTo(down) > 0 ? up.RoundExtendedBinary64() : zero;
            negative[i] = down.Sign > 0 && down.CompareTo(up) > 0 ? down.RoundExtendedBinary64() : zero;
            ranges[i] = new[] { high - low, (high - previousClose).Abs(), (low - previousClose).Abs() }.Max().RoundExtendedBinary64();
        }
        var plusMean = SmoothRocBankStage(positive, length, kind); var minusMean = SmoothRocBankStage(negative, length, kind); var rangeMean = SmoothRocBankStage(ranges, length, kind);
        var plus = new double[bars.Count]; var minus = new double[bars.Count]; var dx = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            plus[i] = rangeMean[i].Sign == 0 ? 0 : Math.Max(0, Math.Min(100, (hundred * plusMean[i] / rangeMean[i]).ToDouble()));
            minus[i] = rangeMean[i].Sign == 0 ? 0 : Math.Max(0, Math.Min(100, (hundred * minusMean[i] / rangeMean[i]).ToDouble()));
            var p = ReferenceFraction.FromDouble(plus[i]); var m = ReferenceFraction.FromDouble(minus[i]);
            dx[i] = ReferenceFraction.FromDouble((p + m).Sign == 0 ? 0 : Math.Min(100, ((p - m).Abs() / (p + m)).ToDouble() * 100));
        }
        return Outputs(("DiPlus", plus), ("DiMinus", minus), ("Adx", SmoothRocBankStage(dx, length, kind).Select(v => v.ToDouble()).ToArray()));
    }
}
