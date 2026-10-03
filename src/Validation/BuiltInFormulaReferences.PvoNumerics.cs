using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RoundedPvo(IReadOnlyList<Bar> bars, object options)
    {
        var normalized = options is PvoSpecOptions alias ? new PpoSpecOptions(alias.Length, 26, alias.MaType, alias.SignalLength)
            : new PpoSpecOptions(Integer(options, "FastLength", 12), Integer(options, "SlowLength", 26),
                ((PercentageVolumeOscillatorSpecOptions)options).MaType, Integer(options, "SignalLength", 9));
        var projected = bars.Select(b => new Bar(b.Time, b.Volume, b.Volume, b.Volume, b.Volume, b.Volume)).ToArray();
        var result = RoundedPpo(projected, normalized);
        return Outputs(("Pvo", result["Ppo"]), ("Signal", result["Signal"]), ("Histogram", result["Histogram"]));
    }
}
