using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AutoDispersionOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return AutoDispersionOutputs(bars, Integer(o, "Length", 90), Integer(o, "SmoothLength", 140), AverageKind(o, 2)); }
    internal static IReadOnlyDictionary<string, double[]> AutoDispersionOutputs(IReadOnlyList<Bar> bars, int length, int smoothLength, int kind, double[][]? external = null)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var squares = prices.Select((v,i) => i < length ? new ReferenceFraction(0) : (v - prices[i - length]) * (v - prices[i - length])).ToArray(); var upperEnvelope = new ReferenceFraction[bars.Count]; var lowerEnvelope = new ReferenceFraction[bars.Count]; var maxima = new ReferenceFraction[bars.Count]; var minima = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var start = Math.Max(0, i + 1 - length); var meanSquare = squares.Skip(start).Take(i + 1 - start).Aggregate(new ReferenceFraction(0), (a,b) => a + b) / new ReferenceFraction(i + 1 - start); var root = meanSquare.SqrtToDouble(); var width = double.IsInfinity(root) ? ReferenceFraction.FromDouble((meanSquare / new ReferenceFraction(BigInteger.One << 2048)).SqrtToDouble()) * new ReferenceFraction(BigInteger.One << 1024) : ReferenceFraction.FromDouble(root);
            upperEnvelope[i] = (prices[i] + width).RoundExtendedBinary64(); lowerEnvelope[i] = (prices[i] - width).RoundExtendedBinary64(); maxima[i] = upperEnvelope.Skip(start).Take(i + 1 - start).Max(); minima[i] = lowerEnvelope.Skip(start).Take(i + 1 - start).Min();
        }
        var firstUpper = external is null ? SmoothRocBankStage(maxima, length, kind) : external[0].Select(ReferenceFraction.FromDouble).ToArray(); var upper = external is null ? SmoothRocBankStage(firstUpper, smoothLength, kind) : external[1].Select(ReferenceFraction.FromDouble).ToArray(); var firstLower = external is null ? SmoothRocBankStage(minima, length, kind) : external[2].Select(ReferenceFraction.FromDouble).ToArray(); var lower = external is null ? SmoothRocBankStage(firstLower, smoothLength, kind) : external[3].Select(ReferenceFraction.FromDouble).ToArray();
        return Outputs(("UpperBand", upper.Select(v => v.ToDouble()).ToArray()), ("MiddleBand", upper.Select((v,i) => ((v + lower[i]) / new ReferenceFraction(2)).ToDouble()).ToArray()), ("LowerBand", lower.Select(v => v.ToDouble()).ToArray()), ("Maxima", maxima.Select(v => v.ToDouble()).ToArray()), ("Minima", minima.Select(v => v.ToDouble()).ToArray()));
    }
}
