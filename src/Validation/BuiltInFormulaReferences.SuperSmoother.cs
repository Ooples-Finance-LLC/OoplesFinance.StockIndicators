using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) SuperSmootherValues(IReadOnlyList<Bar> bars, int length)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var angle = Math.Max(.01, Math.Min(.99, Math.Sqrt(2) * Math.PI / Math.Max(1, length))); var radius = R(Math.Exp(-angle)); var cosine = R(Math.Cos(angle));
        var feedback = R(2) * radius * cosine; var decay = R(0) - radius * radius; var gain = R(1) - feedback - decay;
        var output = new double[bars.Count]; var signals = new Signal[bars.Count]; var previous = R(0); var older = R(0); var before = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var value = R(bars[i].Close); var forcing = gain * (value + (i == 0 ? R(0) : R(bars[i - 1].Close))) / R(2);
            var current = RoundRocBankStage(forcing + feedback * previous + decay * older); output[i] = current.ToDouble(); var difference = value - current; var direction = difference.CompareTo(before);
            signals[i] = difference.Sign > 0 && direction > 0 ? Signal.StrongBuy : difference.Sign < 0 && direction < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
            older = previous; previous = current; before = difference;
        }
        return (new Dictionary<string, double[]> { { "Essf", output } }, signals);
    }
}
