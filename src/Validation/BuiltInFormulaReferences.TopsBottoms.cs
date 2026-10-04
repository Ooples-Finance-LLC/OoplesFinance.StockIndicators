using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TopsBottomsOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions(); return new Dictionary<string, double[]> { ["Tabf"] = TopsBottomsValues(bars, Integer(o, "Length", 50), (MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!) };
    }
    internal static double[] TopsBottomsValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v); var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values) => RationalAverage(values, length, kind);
        var mean = Mean(bars.Select(b => R(b.Close)).ToArray());
        var rising = mean.Select((v, i) => v.CompareTo(i == 0 ? zero : mean[i - 1]) > 0 ? v : zero).ToArray();
        var falling = mean.Select((v, i) => v.CompareTo(i == 0 ? zero : mean[i - 1]) < 0 ? v : zero).ToArray();
        bool AtEndpoint(ReferenceFraction[] samples, int i)
        {
            if (mean[i].Sign == 0) return false;
            if (i + 1 < length) return true;
            // A nonzero squared distance from the first sample proves positive variance.
            // This independent witness avoids forming large cancelling second moments.
            for (var j = i - length + 2; j <= i; j++)
            {
                var delta = samples[j] - samples[i - length + 1];
                if ((delta * delta).Sign > 0) return false;
            }
            return true;
        }
        var result = new double[bars.Count];
        for (var i = 1; i < result.Length; i++)
            result[i] = AtEndpoint(rising, i - 1) && !AtEndpoint(rising, i) ? 1
                : AtEndpoint(falling, i - 1) && !AtEndpoint(falling, i) ? -1 : 0;
        return result;
    }
}
