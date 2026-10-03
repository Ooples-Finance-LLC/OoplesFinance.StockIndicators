using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) RoofAutocorrelationValues(IReadOnlyList<Bar> bars, int length, int smoothing)
    {
        length = Math.Max(1, length); var roof = new ReferenceFraction[bars.Count]; RoofingValues(bars, length, smoothing, false, roof); var values = new double[bars.Count]; var signals = new Signal[bars.Count]; var zero = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(length, i + 1); var x = new ReferenceFraction[count]; var y = new ReferenceFraction[count]; var sx = zero; var sy = zero;
            for (var j = 0; j < count; j++) { x[j] = roof[i - j]; y[j] = i - j < length ? zero : roof[i - j - length]; sx += x[j]; sy += y[j]; }
            var mx = sx / new ReferenceFraction(count); var my = sy / new ReferenceFraction(count); var vx = zero; var vy = zero; var cross = zero;
            for (var j = 0; j < count; j++) { var dx = x[j] - mx; var dy = y[j] - my; vx += dx * dx; vy += dy * dy; cross += dx * dy; }
            values[i] = count < 2 || vx.Sign == 0 || vy.Sign == 0 ? 0 : .5 * (1 + cross.Sign * ((cross * cross) / (vx * vy)).SqrtToDouble());
            var difference = values[i] - (i == 0 ? 0 : values[i - 1]); var previous = (i == 0 ? 0 : values[i - 1]) - (i < 2 ? 0 : values[i - 2]); signals[i] = difference > 0 ? difference > previous ? Signal.StrongBuy : Signal.Buy : difference < 0 ? difference < previous ? Signal.StrongSell : Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { { "Eaci", values } }, signals);
    }
}
