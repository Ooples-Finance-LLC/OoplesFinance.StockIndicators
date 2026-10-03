using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VortexBandsOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return VortexBandsValues(bars, Integer(options, "Length", 20), (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) VortexBandsValues(IReadOnlyList<Bar> bars, int length, MovingAvgType kind)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var mcNicholl = kind == MovingAvgType.McNichollMovingAverage;
        length = Math.Max(mcNicholl ? 2 : 1, length);
        var code = mcNicholl ? 3 : AverageKind(new { MaType = kind }, 0);
        ReferenceFraction[] Basic(ReferenceFraction[] values)
        {
            if (code is 3 or 6) return SmoothRocBankStage(values, length, code, v => v);
            if (code is not (1 or 2)) return Average(values.Select(v => v.ToDouble()).ToArray(), length, code).Select(R).ToArray();
            return values.Select((_, i) =>
            {
                if (code == 1 && i + 1 < length) return R(0);
                var sum = R(0);
                for (var j = Math.Max(0, i - length + 1); j <= i; j++) sum += values[j] * R(code == 2 ? length - (long)i + j : 1);
                return sum / (code == 2 ? R(length) * R(length + 1L) / R(2) : R(length));
            }).ToArray();
        }
        ReferenceFraction[] Smooth(ReferenceFraction[] values)
        {
            var first = Basic(values); if (!mcNicholl) return first;
            var second = Basic(first); var correction = R(length + 1L) / R(length - 1L);
            // Independent first-plus-correction form; production combines weighted stages.
            return first.Select((v, i) => v + correction * (v - second[i])).ToArray();
        }
        var prices = bars.Select(b => R(b.Close)).ToArray(); var basis = Smooth(prices);
        var width = Smooth(prices.Select((v, i) => (v - basis[i]).Abs()).ToArray());
        var half = width.Select(v => v.Sign > 0 ? v * R(2) : R(0)).ToArray();
        return (new Dictionary<string, double[]> { ["UpperBand"] = basis.Select((v, i) => (v + half[i]).ToDouble()).ToArray(),
            ["MiddleBand"] = basis.Select(v => v.ToDouble()).ToArray(), ["LowerBand"] = basis.Select((v, i) => (v - half[i]).ToDouble()).ToArray() },
            half.Select(v => v.Sign > 0 ? Signal.Buy : Signal.None).ToArray());
    }
}
