using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> GrandForecastOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return GrandForecastValues(bars, Integer(options, "Length", 100), Integer(options, "ForecastLength", 200), Number(options, 2, "Mult")).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) GrandForecastValues(IReadOnlyList<Bar> bars, int length, int horizon, double mult)
    {
        length = Math.Max(1, length); horizon = Math.Max(1, horizon);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var trend = new ReferenceFraction[bars.Count]; var change = new ReferenceFraction[bars.Count]; var forecast = new ReferenceFraction[bars.Count]; var errors = new ReferenceFraction[bars.Count];
        var means = new double[bars.Count]; var upper = new double[bars.Count]; var middle = new double[bars.Count]; var lower = new double[bars.Count]; var signals = new Signal[bars.Count]; var oldBull = R(0); var oldBear = R(0);
        ReferenceFraction Mean(ReferenceFraction[] values, int i, int period)
        { var total = R(0); var start = Math.Max(0, i - period + 1); for (var j = start; j <= i; j++) total += values[j]; return (total / new ReferenceFraction(i - start + 1)).RoundExtendedBinary64(); }
        for (var i = 0; i < bars.Count; i++)
        {
            var price = R(bars[i].Close); var prevT = i < length ? price : trend[i - length]; var prevChange = i < length ? price : change[i - length];
            change[i] = (R(.9) * prevT).RoundExtendedBinary64();
            trend[i] = (change[i] + R(.1) * price + change[i] - prevChange).RoundExtendedBinary64();
            var mean = Mean(trend, i, length); forecast[i] = (new ReferenceFraction(2) * trend[i] - (i < horizon ? R(0) : trend[i - horizon])).RoundExtendedBinary64();
            errors[i] = (price - (i < horizon ? R(0) : forecast[i - horizon])).RoundExtendedBinary64(); if (errors[i].Sign < 0) errors[i] = R(0) - errors[i];
            var errorMean = Mean(errors, i, horizon); means[i] = mean.ToDouble(); middle[i] = forecast[i].ToDouble(); upper[i] = (forecast[i] + R(mult) * errorMean).ToDouble(); lower[i] = (forecast[i] - R(mult) * errorMean).ToDouble();
            var maximum = forecast[i]; var minimum = forecast[i]; foreach (var value in new[] { trend[i], mean }) { if (value.CompareTo(maximum) > 0) maximum = value; if (value.CompareTo(minimum) < 0) minimum = value; }
            var bull = price - maximum; var bear = price - minimum;
            signals[i] = bull.Sign > 0 && bull.CompareTo(oldBull) > 0 ? Signal.StrongBuy : bear.Sign < 0 && bear.CompareTo(oldBear) < 0 ? Signal.StrongSell : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
            oldBull = bull; oldBear = bear;
        }
        return (new Dictionary<string, double[]> { ["Gtf"] = means, ["UpperBand"] = upper, ["MiddleBand"] = middle, ["LowerBand"] = lower }, signals);
    }
}
