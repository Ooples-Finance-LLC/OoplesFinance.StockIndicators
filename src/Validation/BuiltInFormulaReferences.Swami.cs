using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SwamiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return new Dictionary<string, double[]> { ["Ss"] = SwamiValues(bars, Integer(options, "FastLength", 12), Integer(options, "SlowLength", 48), includeSignals: false).Line };
    }
    internal static (double[] Line, Signal[] Signals) SwamiValues(IReadOnlyList<Bar> bars, int fastLength, int slowLength, bool includeSignals = true)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var width = Math.Max(1L, (long)Math.Max(1, slowLength) - Math.Max(1, fastLength));
        var zero = R(0); var one = R(1); var values = new ReferenceFraction[bars.Count]; var trades = includeSignals ? new Signal[bars.Count] : Array.Empty<Signal>();
        var weightedNumerator = zero; var weightedDenominator = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var start = (int)Math.Max(0, i - width + 1); var high = bars[i].High; var low = bars[i].Low;
            for (var j = start; j < i; j++) { high = Math.Max(high, bars[j].High); low = Math.Min(low, bars[j].Low); }
            // Unroll the two half-decay recurrences: their common 2^-(i+1) cancels in the ratio.
            var weight = new ReferenceFraction(BigInteger.One << i);
            weightedNumerator += weight * (R(bars[i].Close) - R(low)); weightedDenominator += weight * (R(high) - R(low));
            var previous = i == 0 ? zero : values[i - 1];
            var value = weightedDenominator.Sign == 0 ? zero : weightedNumerator / (R(5) * weightedDenominator) + R(4) * previous / R(5);
            values[i] = value.Sign < 0 ? zero : value.CompareTo(one) > 0 ? one : value;
            if (!includeSignals) continue;
            var older = i < 2 ? zero : values[i - 2];
            var slope = values[i] - previous; var priorSlope = previous - older;
            trades[i] = slope.Sign > 0 && slope.CompareTo(priorSlope) > 0 ? Signal.StrongBuy
                : slope.Sign < 0 && slope.CompareTo(priorSlope) < 0 ? Signal.StrongSell
                : slope.Sign > 0 || previous.CompareTo(R(.2)) < 0 && values[i].CompareTo(R(.2)) > 0 ? Signal.Buy
                : slope.Sign < 0 || previous.CompareTo(R(.8)) > 0 && values[i].CompareTo(R(.8)) < 0 ? Signal.Sell : Signal.None;
        }
        return (values.Select(v => v.ToDouble()).ToArray(), trades);
    }
}
