using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VpciOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return VpciValues(bars, Integer(options, "FastLength", 5), Integer(options, "SlowLength", 20),
            Integer(options, "Length", 8), AverageKind(options, 1)).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) VpciValues(IReadOnlyList<Bar> bars,
        int fast, int slow, int signal, int kind = 1)
    {
        fast = Math.Max(1, fast); slow = Math.Max(1, slow); signal = Math.Max(1, signal);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period)
        {
            if (kind is 3 or 6) return SmoothRocBankStage(values, period, kind, v => v);
            if (kind is not (1 or 2)) return Average(values.Select(v => v.ToDouble()).ToArray(), period, kind).Select(R).ToArray();
            return values.Select((_, i) =>
            {
                if (kind == 1 && i + 1 < period) return R(0);
                var sum = R(0);
                for (var j = Math.Max(0, i - period + 1); j <= i; j++) sum += values[j] * R(kind == 2 ? period - (long)i + j : 1);
                return sum / (kind == 2 ? R(period) * R(period + 1L) / R(2) : R(period));
            }).ToArray();
        }
        var prices = bars.Select(b => R(b.Close)).ToArray(); var volumes = bars.Select(b => R(b.Volume)).ToArray();
        ReferenceFraction[] Weighted(int period) => prices.Select((_, i) =>
        {
            if (i + 1 < period) return R(0);
            var mass = R(0); var product = R(0);
            for (var j = i - period + 1; j <= i; j++) { mass += volumes[j]; product += prices[j] * volumes[j]; }
            return mass.Sign == 0 ? R(0) : product / mass;
        }).ToArray();
        var wf = Weighted(fast); var ws = Weighted(slow); var pf = Mean(prices, fast); var ps = Mean(prices, slow);
        var vf = Mean(volumes, fast); var vs = Mean(volumes, slow);
        // Divide once after combining factors; production forms two distinct ratios.
        var raw = prices.Select((_, i) => pf[i].Sign == 0 || vs[i].Sign == 0 ? R(0)
            : (ws[i] - ps[i]) * wf[i] * vf[i] / (pf[i] * vs[i])).ToArray();
        var smoothed = Mean(raw, signal); var previous = R(0); var trades = new Signal[bars.Count];
        for (var i = 0; i < raw.Length; i++)
        {
            var margin = raw[i] - smoothed[i]; var direction = margin.CompareTo(previous);
            trades[i] = margin.Sign > 0 && direction > 0 ? Signal.StrongBuy : margin.Sign < 0 && direction < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            previous = margin;
        }
        return (new Dictionary<string, double[]> { ["Vpci"] = raw.Select(v => v.ToDouble()).ToArray(),
            ["Signal"] = smoothed.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}
