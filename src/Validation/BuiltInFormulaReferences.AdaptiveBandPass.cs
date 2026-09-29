using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) AdaptiveBandPassValues(IReadOnlyList<Bar> bars, int upper, int lower, int firstLag, double width)
    {
        if (double.IsNaN(width) || double.IsInfinity(width) || width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        upper = Math.Max(1, upper); lower = Math.Max(1, lower); firstLag = Math.Max(1, firstLag); var roof = new ReferenceFraction[bars.Count]; RoofingValues(bars, upper, lower, false, roof); var cycles = AutocorrelationSpectrumValues(bars, upper, lower, firstLag).Outputs["Eacp"]; var band = new ReferenceFraction[bars.Count]; var peaks = new ReferenceFraction[bars.Count]; var line = new double[bars.Count]; var trigger = new double[bars.Count]; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var cycle = Math.Min(upper, Math.Max(firstLag, cycles[i])); var period = .9 * cycle; var angle = 2 * Math.PI * (Math.IEEERemainder(width, period) / period); var cosine = Math.Cos(angle); var alpha = R(cosine <= 0 ? .01 : Math.Max(.01, Math.Min(.99, cosine / (1 + Math.Abs(Math.Sin(angle)))))); var beta = R(Math.Cos(2 * Math.PI / period));
            band[i] = i < 3 ? R(0) : RoundRocBankStage((R(1) - alpha) * (roof[i] - roof[i - 2]) / R(2) + beta * (R(1) + alpha) * band[i - 1] - alpha * band[i - 2]);
            var decayed = RoundRocBankStage(R(.991) * (i == 0 ? R(0) : peaks[i - 1])); peaks[i] = decayed.CompareTo(band[i].Abs()) > 0 ? decayed : band[i].Abs(); line[i] = peaks[i].Sign == 0 ? 0 : (band[i] / peaks[i]).ToDouble(); trigger[i] = i == 0 ? 0 : .9 * line[i - 1];
            var now = line[i] - trigger[i]; var before = i == 0 ? 0 : line[i - 1] - trigger[i - 1]; var previous = i == 0 ? 0 : line[i - 1]; var threshold = Math.Sqrt(.5);
            signals[i] = now > 0 && now > before ? Signal.StrongBuy : now < 0 && now < before ? Signal.StrongSell : now > 0 || previous < -threshold && line[i] > -threshold ? Signal.Buy : now < 0 || previous > threshold && line[i] < threshold ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { { "Eabpf", line }, { "Signal", trigger } }, signals);
    }
}
