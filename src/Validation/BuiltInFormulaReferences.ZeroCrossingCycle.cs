using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) ZeroCrossingCycleValues(IReadOnlyList<Bar> bars, int length, double bandwidth = .7)
    {
        var band = ClampedBandPassOutputs(bars, length, bandwidth, 0); var line = band["Ebpf"]; var trigger = band["Signal"]; var values = new double[bars.Count]; var signals = new Signal[bars.Count]; var crossing = -1;
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i == 0 ? 0 : line[i - 1]; var previousCycle = i == 0 ? 0 : values[i - 1];
            if (line[i] != 0 && Math.Sign(line[i]) != Math.Sign(previous)) { values[i] = Math.Min(previousCycle * 1.25, Math.Max(previousCycle * .8, 2d * (i - crossing))); crossing = i; }
            else values[i] = Math.Max(6, previousCycle);
            var distance = line[i] - trigger[i]; var older = previous - (i == 0 ? 0 : trigger[i - 1]); signals[i] = distance > 0 ? distance > older ? Signal.StrongBuy : Signal.Buy : distance < 0 ? distance < older ? Signal.StrongSell : Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { { "Ezcdc", values } }, signals);
    }
}
