using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) AdaptiveRangeV2Values(IReadOnlyList<Bar> bars, int upper, int lower, int firstLag, int kind = 3, int mode = 0, IReadOnlyList<double>? externalAverage = null)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        upper = Math.Max(1, upper); lower = Math.Max(1, lower); firstLag = Math.Max(1, firstLag); var roof = new ReferenceFraction[bars.Count]; RoofingValues(bars, upper, lower, false, roof); var cycles = AutocorrelationSpectrumValues(bars, upper, lower, firstLag).Outputs["Eacp"]; var values = new double[bars.Count]; var ratios = new double[bars.Count]; var residuals = new ReferenceFraction[bars.Count];
        var angle = 1.414 * Math.PI / lower; var radius = Math.Exp(-angle); var feedback = R(2 * radius * Math.Cos(Math.Min(angle, .99))); var decay = R(-radius * radius); var gap = R(2 * Math.Exp(-angle / 2) * Math.Sinh(angle / 2)); var sine = R(Math.Sin(Math.Min(angle, .99) / 2)); var drive = R((gap * gap + R(4) * R(radius) * sine * sine).ToDouble() / 2);
        for (var i = 0; i < bars.Count; i++)
        {
            var cycle = Math.Min(upper, Math.Max(lower, cycles[i])); var nearest = Math.Round(cycle); var window = (int)(Math.Abs(cycle - nearest) <= 1e-9 * Math.Max(1, Math.Abs(cycle)) ? nearest : Math.Ceiling(cycle)); var start = Math.Max(0, i - window + 1); var count = i - start + 1;
            if (mode == 2)
            {
                var sum = R(0); for (var j = start; j <= i; j++) sum += roof[j]; var mean = RoundRocBankStage(sum / R(count)); residuals[i] = roof[i] - mean; var squares = R(0); for (var j = start; j <= i; j++) squares += residuals[j] * residuals[j];
                ratios[i] = squares.Sign == 0 ? 0 : residuals[i].Sign * ((residuals[i] * residuals[i] * R(count)) / (squares * R(.015) * R(.015))).SqrtToDouble();
            }
            else
            {
                var highest = roof[start]; var lowest = roof[start]; for (var j = start + 1; j <= i; j++) { if (roof[j].CompareTo(highest) > 0) highest = roof[j]; if (roof[j].CompareTo(lowest) < 0) lowest = roof[j]; }
                ratios[i] = (highest - lowest).Sign == 0 ? 0 : ((roof[i] - lowest) / (highest - lowest)).ToDouble();
            }
            values[i] = (drive * R(ratios[i]) + drive * R(i == 0 ? 0 : ratios[i - 1]) + feedback * R(i == 0 ? 0 : values[i - 1]) + decay * R(i < 2 ? 0 : values[i - 2])).ToDouble();
        }
        var means = externalAverage is not null ? externalAverage.ToArray() : kind is 1 or 2 or 3 or 6 ? SmoothStrengthStage(values.Select(R).ToArray(), lower, kind).Select(v => v.ToDouble()).ToArray() : Average(values, lower, kind); var result = mode == 1 ? values.Select(value => Math.Tanh(6 * (value - .5))).ToArray() : values; var line = mode == 1 ? result.Select((_, i) => i == 0 ? 0 : .9 * result[i - 1]).ToArray() : means; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i == 0 ? 0 : result[i - 1]; var now = result[i] - line[i]; var before = i == 0 ? 0 : result[i - 1] - line[i - 1]; var low = mode == 2 ? -100 : .3; var high = mode == 2 ? 100 : .7;
            signals[i] = now > 0 && now > before ? Signal.StrongBuy : now < 0 && now < before ? Signal.StrongSell : now > 0 || mode != 1 && previous < low && result[i] > low ? Signal.Buy : now < 0 || mode != 1 && previous > high && result[i] < high ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { { mode == 2 ? "Eacci" : mode == 1 ? "Easift" : "Easi", result }, { "Signal", line } }, signals);
    }
}
