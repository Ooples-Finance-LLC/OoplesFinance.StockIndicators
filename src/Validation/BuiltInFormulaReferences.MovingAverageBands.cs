using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> MovingAverageBandOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, double[]? customerFast = null, double[]? customerSlow = null)
    {
        var options = indicator.CreateOptions(); var fastLength = Math.Max(1, Integer(options, "FastLength", 10)); var slowLength = Math.Max(1, Integer(options, "SlowLength", 50));
        var kind = AverageKind(options, 3); var mult = ReferenceFraction.FromDouble(Number(options, 1, "Mult")); var two = new ReferenceFraction(2);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var fast = customerFast is null ? SmoothRocBankStage(prices, fastLength, kind) : customerFast.Select(ReferenceFraction.FromDouble).ToArray();
        var slow = customerSlow is null ? SmoothRocBankStage(prices, slowLength, kind) : customerSlow.Select(ReferenceFraction.FromDouble).ToArray();
        var gaps = fast.Select((v, i) => (slow[i] - v).RoundExtendedBinary64()).ToArray();
        var upper = new double[bars.Count]; var lower = new double[bars.Count]; var bandwidth = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(i + 1, fastLength); var square = new ReferenceFraction(0);
            for (var j = i - count + 1; j <= i; j++) square += gaps[j] * gaps[j];
            var mean = square / new ReferenceFraction(count); var root = mean.SqrtToDouble();
            var deviation = double.IsInfinity(root) ? ReferenceFraction.FromDouble((mean / new ReferenceFraction(4)).SqrtToDouble()) * two : ReferenceFraction.FromDouble(root);
            var width = (deviation * mult).RoundExtendedBinary64();
            upper[i] = (slow[i] + width).ToDouble(); lower[i] = (slow[i] - width).ToDouble();
            bandwidth[i] = slow[i].Sign == 0 ? 0 : (new ReferenceFraction(200) * width / slow[i]).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", slow.Select(v => v.ToDouble()).ToArray()), ("LowerBand", lower), ("FastMa", fast.Select(v => v.ToDouble()).ToArray()), ("Mabw", bandwidth));
    }
}
