using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedNormalizedMacd(IReadOnlyList<Bar> bars, object options)
    {
        double[] Mean(int length)
        {
            var result = new double[bars.Count];
            if (result.Length == 0) return result;
            result[0] = bars[0].Close;
            for (var i = 1; i < result.Length; i++) result[i] = ((new ReferenceFraction(2) * ReferenceFraction.FromDouble(bars[i].Close) +
                new ReferenceFraction(length - 1) * ReferenceFraction.FromDouble(result[i - 1])) / new ReferenceFraction((long)length + 1)).ToDouble();
            return result;
        }
        var fast = Mean(Math.Max(1, Integer(options, "FastLength", 12)));
        var slow = Mean(Math.Max(1, Integer(options, "SlowLength", 26)));
        return fast.Select((v, i) => slow[i] == 0 ? 0 : (new ReferenceFraction(100) *
            (ReferenceFraction.FromDouble(v) / ReferenceFraction.FromDouble(slow[i]) - new ReferenceFraction(1))).ToDouble()).ToArray();
    }
}
