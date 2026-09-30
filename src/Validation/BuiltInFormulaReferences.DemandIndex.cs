using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) DemandIndexValues(IReadOnlyList<Bar> bars)
    {
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var values = new double[bars.Count]; var signals = new Signal[bars.Count];
        var previous = R(0); var priorSlope = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i]; var line = R(0);
            if (i > 0)
            {
                var range = (R(bar.High) - R(bar.Low)).RoundExtendedBinary64();
                var buying = (R(bar.Close) - R(bar.Low)).RoundExtendedBinary64();
                var selling = (R(bar.High) - R(bar.Close)).RoundExtendedBinary64();
                var buyShare = range.Sign == 0 ? R(0) : (buying / range).RoundExtendedBinary64();
                var sellShare = range.Sign == 0 ? R(0) : (selling / range).RoundExtendedBinary64();
                var buys = (R(bar.Volume) * buyShare).RoundExtendedBinary64();
                var sells = (R(bar.Volume) * sellShare).RoundExtendedBinary64();
                if (sells.Sign != 0) line = ((buys / sells).RoundExtendedBinary64() - R(1)).RoundExtendedBinary64();
            }
            values[i] = line.ToDouble(); var slope = line - previous;
            signals[i] = slope.Sign > 0 && slope.CompareTo(priorSlope) > 0 ? Signal.StrongBuy
                : slope.Sign < 0 && slope.CompareTo(priorSlope) < 0 ? Signal.StrongSell
                : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
            previous = line; priorSlope = slope;
        }
        return (new Dictionary<string, double[]> { ["Di"] = values }, signals);
    }
}
