using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> QqeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var o = indicator.CreateOptions();
        return QqeValues(bars, Integer(o, "Length", 14), Integer(o, "SmoothLength", 5), AverageKind(o, 3),
            Number(o, 2.618, "FastFactor"), Number(o, 4.236, "SlowFactor")).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) QqeValues(IReadOnlyList<Bar> bars, int length, int smooth, int kind, double fast, double slow)
    {
        length = Math.Max(1, length); smooth = Math.Max(1, smooth);
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var zero = R(0);
        ReferenceFraction[] Mean(ReferenceFraction[] values, long period)
        {
            if (kind is not (1 or 2 or 3 or 6)) return Average(values.Select(v => v.ToDouble()).ToArray(), checked((int)period), kind).Select(R).ToArray();
            var prefix = new ReferenceFraction[values.Length + 1]; var moments = new ReferenceFraction[prefix.Length];
            prefix[0] = moments[0] = zero;
            for (var i = 0; i < values.Length; i++) { prefix[i + 1] = prefix[i] + values[i]; moments[i + 1] = moments[i] + R(i + 1) * values[i]; }
            var result = new ReferenceFraction[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var previous = i == 0 ? zero : result[i - 1];
                if (kind == 6) result[i] = (previous * R(period - 1) + values[i]) / R(period);
                else if (kind == 3 && i >= period) result[i] = (previous * R(period - 1) + R(2) * values[i]) / R(period + 1);
                else if (kind == 1 && i + 1 < period) result[i] = zero;
                else
                {
                    var start = (int)Math.Max(0, i - period + 1); var sum = prefix[i + 1] - prefix[start];
                    result[i] = kind == 2 ? (moments[i + 1] - moments[start] + R(period - i - 1) * sum)
                        / new ReferenceFraction(new System.Numerics.BigInteger(period) * (period + 1) / 2)
                        : sum / R(Math.Min(i + 1L, period));
                }
            }
            return result;
        }
        var rsi = RoundedPriceRsi(bars, length, kind).Select(R).ToArray();
        var smoothed = Mean(rsi, smooth);
        var changes = smoothed.Select((v, i) => { var delta = v - (i == 0 ? zero : smoothed[i - 1]); return delta.Sign < 0 ? zero - delta : delta; }).ToArray();
        var widths = Mean(Mean(changes, 2L * length - 1), 2L * length - 1);
        var first = new double[bars.Count]; var second = new double[bars.Count]; var signals = new Signal[bars.Count];
        var lastBull = zero; var lastBear = zero;
        for (var i = 0; i < bars.Count; i++)
        {
            var f = widths[i] * R(fast); var s = widths[i] * R(slow);
            var bull = widths[i] - (f.CompareTo(s) >= 0 ? f : s); var bear = widths[i] - (f.CompareTo(s) >= 0 ? s : f);
            signals[i] = bull.Sign > 0 && bull.CompareTo(lastBull) > 0 ? Signal.StrongBuy
                : bear.Sign < 0 && bear.CompareTo(lastBear) < 0 ? Signal.StrongSell
                : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
            first[i] = f.ToDouble(); second[i] = s.ToDouble(); lastBull = bull; lastBear = bear;
        }
        return (new() { ["FastAtrRsi"] = first, ["SlowAtrRsi"] = second }, signals);
    }
}
