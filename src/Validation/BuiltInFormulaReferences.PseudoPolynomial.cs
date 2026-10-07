using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> PseudoPolynomialOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return PseudoPolynomialOutputs(bars, Integer(o, "Length", 14), AverageKind(o, 1), Number(o, .9, "Morph")); }
    internal static IReadOnlyDictionary<string, double[]> PseudoPolynomialOutputs(IReadOnlyList<Bar> bars, int length, int kind, double morph, double[]? externalMean = null)
    {
        length = Math.Max(1, length); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var m = ReferenceFraction.FromDouble(morph); var one = new ReferenceFraction(1); var k = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var first = i >= length ? k[i - length] : prices[i]; var second = i >= 2L * length ? k[i - 2 * length] : prices[i]; var firstIndex = Math.Max(0, i - (long)length); var secondIndex = Math.Max(0, i - 2L * length); var ky = (m * first + (one - m) * prices[i]).RoundExtendedBinary64(); var ky2 = (m * second + (one - m) * prices[i]).RoundExtendedBinary64();
            k[i] = firstIndex == secondIndex ? new ReferenceFraction(0) : (ky + new ReferenceFraction(i - firstIndex) / new ReferenceFraction(secondIndex - firstIndex) * (ky2 - ky)).RoundExtendedBinary64();
        }
        var mean = externalMean is null ? SmoothRocBankStage(k, length, kind) : externalMean.Select(ReferenceFraction.FromDouble).ToArray(); var errors = prices.Select((v,i) => (v - mean[i]).Abs().RoundExtendedBinary64()).ToArray(); var upper = new double[bars.Count]; var middle = new double[bars.Count]; var lower = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var width = i == 0 ? new ReferenceFraction(0) : (errors.Take(i + 1).Aggregate(new ReferenceFraction(0), (a,b) => a + b) / new ReferenceFraction(i)).RoundExtendedBinary64(); var u = (mean[i] + width).RoundExtendedBinary64(); var l = (mean[i] - width).RoundExtendedBinary64(); upper[i] = u.ToDouble(); middle[i] = ((u + l) / new ReferenceFraction(2)).ToDouble(); lower[i] = l.ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower), ("Raw", k.Select(v => v.ToDouble()).ToArray()));
    }
}
