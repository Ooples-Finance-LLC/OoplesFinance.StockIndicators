using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EfficientTrendStepOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return EfficientTrendStepOutputs(bars, Integer(o, "Length", 100), Integer(o, "FastLength", 50), Integer(o, "SlowLength", 200)); }
    internal static IReadOnlyDictionary<string, double[]> EfficientTrendStepOutputs(IReadOnlyList<Bar> bars, int length, int fastLength, int slowLength)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var efficiency = RoundedKaufmanTrajectory(bars, Math.Max(1, length))["Er"]; var upper = new double[bars.Count]; var middle = new double[bars.Count]; var lower = new double[bars.Count];
        ReferenceFraction Deviation(int index, int period)
        {
            period = Math.Max(1, period); if (index + 1 < period) return new ReferenceFraction(0); var values = prices.Skip(index + 1 - period).Take(period).Select(v => v * new ReferenceFraction(2)).ToArray(); var mean = values.Aggregate(new ReferenceFraction(0), (a,b) => a + b) / new ReferenceFraction(period); var variance = values.Aggregate(new ReferenceFraction(0), (a,b) => a + (b - mean) * (b - mean)) / new ReferenceFraction(period); var root = variance.SqrtToDouble();
            return double.IsInfinity(root) ? ReferenceFraction.FromDouble((variance / new ReferenceFraction(BigInteger.One << 2048)).SqrtToDouble()) * new ReferenceFraction(BigInteger.One << 1024) : ReferenceFraction.FromDouble(root);
        }
        for (var i = 0; i < bars.Count; i++)
        {
            var er = ReferenceFraction.FromDouble(efficiency[i]); var deviation = (er * Deviation(i, fastLength) + (new ReferenceFraction(1) - er) * Deviation(i, slowLength)).RoundExtendedBinary64(); var previous = i == 0 ? prices[i] : ReferenceFraction.FromDouble(middle[i - 1]); var center = prices[i].CompareTo(previous + deviation) > 0 || prices[i].CompareTo(previous - deviation) < 0 ? prices[i] : previous;
            upper[i] = (center + deviation).ToDouble(); middle[i] = center.ToDouble(); lower[i] = (center - deviation).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
