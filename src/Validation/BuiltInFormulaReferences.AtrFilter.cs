using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) AtrFilterValues(IReadOnlyList<Bar> bars, int length, int atrLength = 20, int deviationLength = 10, int lookback = 20, double cap = 5, int kind = 1, IReadOnlyList<double>? externalRanges = null, IReadOnlyList<double>? externalSquares = null)
    {
        if (double.IsNaN(cap) || double.IsInfinity(cap)) throw new ArgumentOutOfRangeException(nameof(cap));
        length = Math.Max(1, length); atrLength = Math.Max(1, atrLength); deviationLength = Math.Max(1, deviationLength); lookback = Math.Max(1, lookback);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Root(ReferenceFraction variance)
        {
            if (variance.Sign <= 0) return R(0); var scale = R(1);
            while (true) { var value = (variance / (scale * scale)).SqrtToDouble(); if (!double.IsInfinity(value)) return R(value) * scale; scale *= R(4294967296d); }
        }
        ReferenceFraction[] Smooth(ReferenceFraction[] input, int count) => kind is 1 or 2 or 3 or 6 ? SmoothRocBankStage(input, count, kind) : Average(input.Select(v => v.ToDouble()).ToArray(), count, kind).Select(R).ToArray();
        var normalized = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var p = R(bars[i].Close); var h = R(bars[i].High); var l = R(bars[i].Low); var before = R(i == 0 ? bars[i].Close : bars[i - 1].Close);
            var range = new[] { h - l, (h - before).Abs(), (l - before).Abs() }.Aggregate((a, b) => a.CompareTo(b) >= 0 ? a : b);
            normalized[i] = RoundRocBankStage(p.Sign == 0 ? range : range / p);
        }
        var average = externalRanges is null ? Smooth(normalized, atrLength) : externalRanges.Select(R).ToArray();
        var meanSquares = externalSquares is null ? Smooth(average.Select(v => RoundRocBankStage(v * v)).ToArray(), deviationLength) : externalSquares.Select(R).ToArray();
        var deviations = new ReferenceFraction[bars.Count]; var estimates = new ReferenceFraction[bars.Count]; var output = new double[bars.Count]; var signals = new Signal[bars.Count]; var previousDifference = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var start = Math.Max(0, i - deviationLength + 1); var sum = R(0); for (var j = start; j <= i; j++) sum += average[j]; var mean = sum / R(deviationLength); ReferenceFraction variance;
            if (kind == 1 && externalSquares is null)
            {
                variance = R(0);
                if (i + 1 >= deviationLength) { for (var j = start; j <= i; j++) { var residual = average[j] - mean; variance += residual * residual; } variance /= R(deviationLength); }
            }
            else variance = meanSquares[i] - mean * mean;
            var deviation = Root(variance); deviations[i] = deviation; var lowest = deviation;
            for (var j = Math.Max(0, i - lookback + 1); j < i; j++) if (deviations[j].CompareTo(lowest) < 0) lowest = deviations[j];
            var factor = deviation.Sign == 0 ? 1 : (lowest / deviation).ToDouble(); var gain = (R(2) * R(Math.Min(factor, cap)) / R(length + 1d)).ToDouble();
            var price = R(bars[i].Close); var previous = i == 0 ? price : estimates[i - 1]; var estimate = RoundRocBankStage(previous + R(gain) * (price - previous)); estimates[i] = estimate; output[i] = estimate.ToDouble(); var difference = price - estimate;
            signals[i] = difference.Sign > 0 && difference.CompareTo(previousDifference) > 0 ? Signal.StrongBuy : difference.Sign < 0 && difference.CompareTo(previousDifference) < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None; previousDifference = difference;
        }
        return (new Dictionary<string, double[]> { { "Afp", output } }, signals);
    }
}
