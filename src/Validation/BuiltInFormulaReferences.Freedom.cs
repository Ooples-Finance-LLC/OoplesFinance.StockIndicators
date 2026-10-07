using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FreedomOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return FreedomValues(bars, Integer(options, "Length", 60), AverageKind(options, 1)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) FreedomValues(IReadOnlyList<Bar> bars, int length, int kind, double[]? externalMeans = null)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var prices = bars.Select(b => R(b.Close)).ToArray(); var volumes = bars.Select(b => R(b.Volume)).ToArray();
        var means = externalMeans is null ? SmoothRocBankStage(volumes, length, kind) : externalMeans.Select(R).ToArray();
        ReferenceFraction Standardize(ReferenceFraction[] values, int index, ReferenceFraction? center = null)
        {
            if (index + 1 < length) return R(0);
            var sample = Window(values, index, length).ToArray(); var mean = sample.Aggregate(R(0), (a, b) => a + b) / R(sample.Length);
            var variance = sample.Aggregate(R(0), (a, b) => a + (b - mean) * (b - mean)) / R(sample.Length); if (variance.Sign == 0) return R(0);
            var residual = values[index] - (center ?? mean); var square = residual * residual / variance; var root = square.SqrtToDouble(); var scale = R(1); var step = new ReferenceFraction(BigInteger.One << 512);
            while (double.IsInfinity(root)) { square /= step * step; scale *= step; root = square.SqrtToDouble(); }
            return R(residual.Sign < 0 ? -root : root) * scale;
        }
        var relative = volumes.Select((_, i) => Standardize(volumes, i, kind == 1 && externalMeans is null ? null : means[i])).ToArray();
        var movement = prices.Select((v, i) =>
        {
            if (i == 0 || prices[i - 1].Sign == 0) return R(0);
            var change = ((v - prices[i - 1]) / prices[i - 1]).RoundExtendedBinary64(); return change.Sign < 0 ? R(0) - change : change;
        }).ToArray();
        ReferenceFraction Rank(ReferenceFraction[] values, int index)
        {
            var sample = Window(values, index, length).ToArray(); var low = sample.Aggregate((a, b) => a.CompareTo(b) < 0 ? a : b); var high = sample.Aggregate((a, b) => a.CompareTo(b) > 0 ? a : b);
            return high.CompareTo(low) == 0 ? R(0) : (R(1) + R(9) * (values[index] - low) / (high - low)).RoundExtendedBinary64();
        }
        var ratios = prices.Select((_, i) => { var move = Rank(movement, i); return move.Sign == 0 ? R(0) : (Rank(relative, i) / move).RoundExtendedBinary64(); }).ToArray();
        var scores = ratios.Select((_, i) => Standardize(ratios, i).ToDouble()).ToArray(); var demand = new double[bars.Count]; var trades = new Signal[bars.Count]; var previousMargin = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            demand[i] = scores[i] >= 2 ? i == 0 ? 0 : bars[i - 1].Close : i == 0 ? bars[i].Close : demand[i - 1]; var margin = prices[i] - R(demand[i]);
            trades[i] = margin.Sign > 0 && margin.CompareTo(previousMargin) > 0 ? Signal.StrongBuy : margin.Sign < 0 && margin.CompareTo(previousMargin) < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            previousMargin = margin;
        }
        return (new Dictionary<string, double[]> { ["Fom"] = scores, ["Dpl"] = demand }, trades);
    }
}
