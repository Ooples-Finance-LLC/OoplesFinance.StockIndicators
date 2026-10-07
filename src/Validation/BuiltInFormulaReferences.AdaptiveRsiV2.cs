using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) AdaptiveRsiV2Values(IReadOnlyList<Bar> bars, int upper, int lower, int firstLag, int kind = 3, bool fisher = false, IReadOnlyList<double>? externalAverage = null)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        upper = Math.Max(1, upper); lower = Math.Max(1, lower); firstLag = Math.Max(1, firstLag); var roof = new ReferenceFraction[bars.Count]; RoofingValues(bars, upper, lower, false, roof); var cycles = AutocorrelationSpectrumValues(bars, upper, lower, firstLag).Outputs["Eacp"]; var values = new double[bars.Count]; var ratios = new double[bars.Count]; var valid = new bool[bars.Count];
        var angle = 1.414 * Math.PI / lower; var radius = Math.Exp(-angle); var feedback = R(2 * radius * Math.Cos(Math.Min(angle, .99))); var decay = R(-radius * radius); var gap = R(2 * Math.Exp(-angle / 2) * Math.Sinh(angle / 2)); var sine = R(Math.Sin(Math.Min(angle, .99) / 2)); var drive = R((gap * gap + R(4) * R(radius) * sine * sine).ToDouble() / 2);
        for (var i = 0; i < bars.Count; i++)
        {
            var cycle = Math.Min(upper, Math.Max(lower, cycles[i])) / 2; var nearest = Math.Round(cycle); var count = (int)(Math.Abs(cycle - nearest) <= 1e-9 * Math.Max(1, Math.Abs(cycle)) ? nearest : Math.Ceiling(cycle)); var up = R(0); var total = R(0);
            for (var j = Math.Max(0, i - count + 1); j <= i; j++) { var change = roof[j] - (j == 0 ? R(0) : roof[j - 1]); if (change.Sign > 0) up += change; total += change.Abs(); }
            valid[i] = total.Sign != 0; ratios[i] = valid[i] ? (up / total).ToDouble() : 0;
            if (i > 0 && valid[i] && valid[i - 1]) values[i] = (drive * R(ratios[i]) + drive * R(ratios[i - 1]) + feedback * R(values[i - 1]) + decay * R(i < 2 ? 0 : values[i - 2])).ToDouble();
        }
        var means = externalAverage is not null ? externalAverage.ToArray() : kind is 1 or 2 or 3 or 6 ? SmoothStrengthStage(values.Select(R).ToArray(), lower, kind).Select(v => v.ToDouble()).ToArray() : Average(values, lower, kind); var result = fisher ? values.Select(value => { var x = Math.Max(-.999, Math.Min(.999, 1.5 * (2 * (value - .5)))); return .5 * Math.Log((1 + x) / (1 - x)); }).ToArray() : values; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i == 0 ? 0 : result[i - 1]; var now = fisher ? result[i] - previous : result[i] - means[i]; var before = fisher ? previous - (i < 2 ? 0 : result[i - 2]) : i == 0 ? 0 : result[i - 1] - means[i - 1]; var low = fisher ? -2 : .3; var high = fisher ? 2 : .7;
            signals[i] = now > 0 && now > before ? Signal.StrongBuy : now < 0 && now < before ? Signal.StrongSell : now > 0 || previous < low && result[i] > low ? Signal.Buy : now < 0 || previous > high && result[i] < high ? Signal.Sell : Signal.None;
        }
        return (fisher ? new Dictionary<string, double[]> { { "Earsift", result } } : new Dictionary<string, double[]> { { "Earsi", result }, { "Signal", means } }, signals);
    }
}
