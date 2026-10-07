using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) AutocorrelationReversalValues(IReadOnlyList<Bar> bars, int length, int smoothing, int firstLag, int kind = 3, IReadOnlyList<double>? externalAverage = null)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        length = Math.Max(1, length); smoothing = Math.Max(1, smoothing); firstLag = Math.Max(1, firstLag); var correlation = RoofAutocorrelationValues(bars, length, smoothing).Outputs["Eaci"]; var values = new double[bars.Count]; var signals = new Signal[bars.Count];
        var averages = externalAverage is not null ? externalAverage.Select(R).ToArray() : kind is 1 or 2 or 3 or 6 ? SmoothStrengthStage(bars.Select(b => R(b.Close)).ToArray(), smoothing, kind) : Average(bars.Select(b => b.Close).ToArray(), smoothing, kind).Select(R).ToArray();
        var events = correlation.Select((value, i) => i > 0 && ((value > .5 && correlation[i - 1] < .5) || (value < .5 && correlation[i - 1] > .5)) ? 1 : 0).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            var count = 0; for (var j = Math.Max(0, i - length + 1); j <= i - firstLag + 1; j++) count += events[j];
            values[i] = count > length / 2d ? 1 : 0; var distance = R(bars[i].Close) - averages[i]; signals[i] = values[i] == 0 ? Signal.None : distance.Sign < 0 ? Signal.Buy : distance.Sign > 0 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { { "Eacr", values } }, signals);
    }
}
