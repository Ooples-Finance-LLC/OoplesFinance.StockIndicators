using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    private static int FireflyKind(object options) => options.GetType().GetProperty("MaType")?.GetValue(options) is MovingAvgType.ZeroLagExponentialMovingAverage ? 4 : AverageKind(options, 4);
    internal static IReadOnlyDictionary<string, double[]> FireflyOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return FireflyValues(bars, Integer(options, "Length", 10), Integer(options, "SmoothLength", 3), FireflyKind(options)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) FireflyValues(IReadOnlyList<Bar> bars, int length, int smooth, int kind)
    {
        length = Math.Max(1, length); smooth = Math.Max(1, smooth);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period)
        {
            if (kind != 4) return SmoothRocBankStage(values, period, kind);
            var first = SmoothRocBankStage(values, period, 3); var second = SmoothRocBankStage(first, period, 3);
            return first.Select((v, i) => (R(2) * v - second[i]).RoundExtendedBinary64()).ToArray();
        }
        var weighted = bars.Select(b => ((R(b.High) + R(b.Low) + R(2) * R(b.Close)) / R(4)).RoundExtendedBinary64()).ToArray();
        var center = Mean(weighted, length);
        var standardized = weighted.Select((v, i) =>
        {
            var variance = R(0);
            if (i + 1 >= length)
            {
                var window = Window(weighted, i, length).ToArray(); var exactMean = window.Aggregate(R(0), (a, b) => a + b) / R(length);
                variance = window.Aggregate(R(0), (a, b) => a + (b - exactMean) * (b - exactMean)) / R(length);
            }
            var offset = (v - center[i]) * R(100);
            if (variance.Sign == 0) return offset.RoundExtendedBinary64();
            var square = offset * offset / variance; var root = square.SqrtToDouble(); var scale = R(1); var factor = new ReferenceFraction(BigInteger.One << 512);
            while (double.IsInfinity(root)) { square /= factor * factor; scale *= factor; root = square.SqrtToDouble(); }
            return R(offset.Sign < 0 ? -root : root) * scale;
        }).ToArray();
        var filtered = Mean(Mean(Mean(standardized, smooth), smooth), length);
        var line = filtered.Select(v => ((v + R(100)) / R(2) - R(4)).RoundExtendedBinary64()).ToArray();
        var maximum = line.Select((_, i) => Window(line, i, smooth).Aggregate((a, b) => a.CompareTo(b) > 0 ? a : b)).ToArray();
        var trades = new Signal[bars.Count]; var previous = R(0); var oldSlope = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var slope = line[i] - previous;
            trades[i] = slope.Sign > 0 && slope.CompareTo(oldSlope) > 0 ? Signal.StrongBuy : slope.Sign < 0 && slope.CompareTo(oldSlope) < 0 ? Signal.StrongSell
                : slope.Sign > 0 || previous.CompareTo(R(20)) < 0 && line[i].CompareTo(R(20)) > 0 ? Signal.Buy
                : slope.Sign < 0 || previous.CompareTo(R(80)) > 0 && line[i].CompareTo(R(80)) < 0 ? Signal.Sell : Signal.None;
            previous = line[i]; oldSlope = slope;
        }
        return (new Dictionary<string, double[]> { ["Fo"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = maximum.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
