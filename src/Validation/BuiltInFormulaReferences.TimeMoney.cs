using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> TimeMoneyOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return TimeMoneyOutputs(bars, Math.Max(1, Integer(options, "Length1", 41)),
            Math.Max(1, Integer(options, "Length2", 82)), AverageKind(options, 1));
    }
    internal static IReadOnlyDictionary<string,double[]> TimeMoneyOutputs(IReadOnlyList<Bar> bars, int length1, int length2,
        int kind, ICollection<Signal>? signals = null)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => value.RoundExtendedBinary64();
        ReferenceFraction Root(ReferenceFraction value)
        {
            if (value.Sign <= 0) return R(0);
            var factor = R(1); var step = R(Math.Pow(2, 256));
            while (double.IsInfinity(value.SqrtToDouble())) { value /= step * step; factor *= step; }
            return R(value.SqrtToDouble()) * factor;
        }
        var lag = (int)Math.Max(2, Math.Min(530, (length1 + 1L) / 2));
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var basis = SmoothRocBankStage(prices, length1, kind, Round);
        var returns = prices.Select((value, i) => i < lag || basis[i - lag].Sign == 0 ? R(0)
            : Round(Round(R(100) * Round(value - basis[i - lag])) / basis[i - lag])).ToArray();
        var mean = SmoothRocBankStage(returns, length2, kind, Round);
        var moment = SmoothRocBankStage(returns.Select(v => Round(v * v)).ToArray(), length2, kind, Round);
        var variance = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (kind != 1) { variance[i] = Round(moment[i] - Round(mean[i] * mean[i])); continue; }
            var first = Math.Max(0, i - length2 + 1); var count = i - first + 1;
            var center = R(0); for (var j = first; j <= i; j++) center += returns[j];
            center /= new ReferenceFraction(count);
            var spread = R(0); for (var j = first; j <= i; j++) { var delta = returns[j] - center; spread += delta * delta; }
            var deviation = Root(spread / new ReferenceFraction(count));
            variance[i] = Round(deviation * deviation);
        }
        var deviations = prices.Select((_, i) => i < lag ? R(0) : Root(variance[i - lag])).ToArray();
        var width = SmoothRocBankStage(deviations, length1, kind, Round);
        var outputs = new Dictionary<string,double[]> { ["Median"] = width.Select(v => v.ToDouble()).ToArray() };
        foreach (var (coefficient, band) in new[] { (.01, "1"), (.02, "2"), (.03, "3") })
        {
            outputs["Ch+" + band] = basis.Select((value, i) => Round(value * Round(R(1) + Round(R(coefficient) * width[i]))).ToDouble()).ToArray();
            outputs["Ch-" + band] = basis.Select((value, i) => Round(value * Round(R(1) - Round(R(coefficient) * width[i]))).ToDouble()).ToArray();
        }
        var previous = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var slope = deviations[i] - width[i];
            signals?.Add(slope.Sign > 0 ? slope.CompareTo(previous) > 0 ? Signal.StrongBuy : Signal.Buy
                : slope.Sign < 0 ? slope.CompareTo(previous) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None);
            previous = slope;
        }
        return outputs;
    }
}
