using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) HilbertNoiseValues(IReadOnlyList<Bar> bars, int length, int kind = 3, IReadOnlyList<double>? externalAverage = null)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var chunk = new ReferenceFraction(BigInteger.One << 512); var upper = new ReferenceFraction(BigInteger.One << 256); var lower = new ReferenceFraction(1) / upper;
        ReferenceFraction Round(ReferenceFraction value)
        {
            if (value.Sign == 0) return value; var scale = new ReferenceFraction(1);
            while (value.Abs().CompareTo(lower) < 0) { value *= chunk; scale /= chunk; }
            while (value.Abs().CompareTo(upper) >= 0) { value /= chunk; scale *= chunk; }
            return R(value.ToDouble()) * scale;
        }
        double LogRatio(ReferenceFraction ratio)
        {
            if (ratio.Sign <= 0) return 0; var shift = 0;
            while (ratio.CompareTo(lower) < 0) { ratio *= chunk; shift -= 512; }
            while (ratio.CompareTo(upper) >= 0) { ratio /= chunk; shift += 512; }
            return Math.Log10(ratio.ToDouble()) + shift * Math.Log10(2);
        }
        length = Math.Max(1, length); var rawEnergy = new ReferenceFraction[bars.Count]; HilbertPhaseValues(bars, length, .635, .338, 1, false, rawEnergy); var averages = externalAverage is not null ? externalAverage.Select(R).ToArray() : kind is 1 or 2 or 3 or 6 ? SmoothStrengthStage(bars.Select(b => R(b.Close)).ToArray(), length, kind) : Average(bars.Select(b => b.Close).ToArray(), length, kind).Select(R).ToArray();
        var energies = new ReferenceFraction[bars.Count]; var ranges = new ReferenceFraction[bars.Count]; var distances = new ReferenceFraction[bars.Count]; var values = new double[bars.Count]; var signals = new Signal[bars.Count];
        ReferenceFraction At(ReferenceFraction[] array, int i) => i < 0 ? R(0) : array[i];
        for (var i = 0; i < bars.Count; i++)
        {
            energies[i] = Round(R(.2) * Round(rawEnergy[i]) + R(.8) * At(energies, i - 1)); var difference = Round(R(bars[i].High) - R(bars[i].Low)); ranges[i] = Round(R(.2) * difference + R(.8) * At(ranges, i - 1)); var denominator = ranges[i] * ranges[i]; var log = energies[i].Sign <= 0 || denominator.Sign == 0 ? 0 : LogRatio(energies[i] / denominator); var level = 10 * log + 1.9; values[i] = ranges[i].Sign == 0 ? 0 : (R(.25) * R(level) + R(.75) * R(i == 0 ? 0 : values[i - 1])).ToDouble();
            distances[i] = R(bars[i].Close) - averages[i]; var previous = At(distances, i - 1); signals[i] = values[i] < 1.9 ? Signal.None : distances[i].Sign > 0 ? distances[i].CompareTo(previous) > 0 ? Signal.StrongBuy : Signal.Buy : distances[i].Sign < 0 ? distances[i].CompareTo(previous) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { { "Esnr", values } }, signals);
    }
}
