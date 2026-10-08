using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ParabolicSarOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return new Dictionary<string, double[]> { ["Sar"] = ParabolicSarValues(bars, Number(options, .02, "Start"), Number(options, .02, "Increment"), Number(options, .2, "Maximum")).Values }; }
    internal static (double[] Values, Signal[] Signals) ParabolicSarValues(IReadOnlyList<Bar> bars, double initial, double increment, double maximum)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var output = new double[bars.Count]; var signals = new Signal[bars.Count]; var rising = true; var segment = 0; var stop = R(0); var previous = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            if (i == 0) stop = R(bars[i].Low);
            else
            {
                // Reconstruct record-setting observations within the current trend segment.
                var extreme = rising ? bars[segment].High : bars[segment].Low; var acceleration = R(initial);
                for (var j = segment + 1; j < i; j++)
                {
                    var candidate = rising ? bars[j].High : bars[j].Low;
                    if (rising ? candidate > extreme : candidate < extreme)
                    { extreme = candidate; acceleration = (acceleration + R(increment)).RoundExtendedBinary64(); if (acceleration.CompareTo(R(maximum)) > 0) acceleration = R(maximum); }
                }
                stop = (stop + acceleration * (R(extreme) - stop)).RoundExtendedBinary64();
                var prior = bars.Skip(Math.Max(0, i - 2)).Take(Math.Min(2, i)).ToArray(); var bound = R(rising ? prior.Min(b => b.Low) : prior.Max(b => b.High));
                if (rising ? stop.CompareTo(bound) > 0 : stop.CompareTo(bound) < 0) stop = bound;
                if (rising ? R(bars[i].Low).CompareTo(stop) < 0 : R(bars[i].High).CompareTo(stop) > 0)
                { stop = R(rising ? Math.Max(extreme, bars[i].High) : Math.Min(extreme, bars[i].Low)); rising = !rising; segment = i; }
            }
            output[i] = stop.ToDouble(); var spread = R(bars[i].High) - stop;
            signals[i] = spread.Sign > 0 && spread.CompareTo(previous) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(previous) < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None; previous = spread;
        }
        return (output, signals);
    }
}
