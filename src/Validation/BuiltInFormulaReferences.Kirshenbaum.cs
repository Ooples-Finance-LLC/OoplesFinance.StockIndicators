using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> KirshenbaumOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return KirshenbaumOutputs(bars, Integer(o, "Length1", 30), Integer(o, "Length2", 20), AverageKind(o, 3), Number(o, 1, "StdDevFactor")); }
    internal static IReadOnlyDictionary<string, double[]> KirshenbaumOutputs(IReadOnlyList<Bar> bars, int meanLength, int errorLength, int kind, double factor, double[]? externalMean = null)
    {
        meanLength = Math.Max(1, meanLength); errorLength = Math.Max(1, errorLength); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var mean = externalMean is null ? SmoothRocBankStage(prices, meanLength, kind) : externalMean.Select(ReferenceFraction.FromDouble).ToArray(); var squares = new ReferenceFraction[bars.Count]; var upper = new double[bars.Count]; var lower = new double[bars.Count]; var multiplier = ReferenceFraction.FromDouble(factor);
        for (var i = 0; i < bars.Count; i++)
        {
            var start = Math.Max(0, i + 1 - errorLength); var n = i + 1 - start; var values = prices.Skip(start).Take(n).ToArray(); var average = values.Aggregate(new ReferenceFraction(0), (a,b) => a + b) / new ReferenceFraction(n); var center = new ReferenceFraction(n - 1) / new ReferenceFraction(2); var xx = new ReferenceFraction(0); var xy = new ReferenceFraction(0);
            for (var j = 0; j < n; j++) { var x = new ReferenceFraction(j) - center; xx += x * x; xy += x * (values[j] - average); }
            var fit = (n == 1 ? average : average + xy / xx * center).RoundExtendedBinary64(); var residual = fit - prices[i]; squares[i] = residual * residual; var energy = squares.Skip(start).Take(n).Aggregate(new ReferenceFraction(0), (a,b) => a + b) / new ReferenceFraction(n); var root = energy.SqrtToDouble(); var width = double.IsInfinity(root) ? ReferenceFraction.FromDouble((energy / new ReferenceFraction(BigInteger.One << 2048)).SqrtToDouble()) * new ReferenceFraction(BigInteger.One << 1024) : ReferenceFraction.FromDouble(root);
            upper[i] = (mean[i] + multiplier * width).ToDouble(); lower[i] = (mean[i] - multiplier * width).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", mean.Select(v => v.ToDouble()).ToArray()), ("LowerBand", lower));
    }
}
