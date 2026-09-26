using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static ReferenceFraction RoundRocBankStage(ReferenceFraction value)
    {
        // Independent power-of-two reduction: ordinary/subnormal publication stays unchanged.
        var scale = new ReferenceFraction(1);
        var factor = ReferenceFraction.FromDouble(Math.Pow(2, 512));
        while (double.IsInfinity(value.ToDouble()))
        {
            value /= factor;
            scale *= factor;
        }
        return ReferenceFraction.FromDouble(value.ToDouble()) * scale;
    }

    private static ReferenceFraction[] SmoothRocBankStage(ReferenceFraction[] input, int length, int kind, Func<ReferenceFraction, ReferenceFraction>? rounding = null)
    {
        var result = new ReferenceFraction[input.Length];
        var prefix = new ReferenceFraction[input.Length + 1];
        var weightedPrefix = new ReferenceFraction[input.Length + 1];
        prefix[0] = weightedPrefix[0] = new ReferenceFraction(0);
        for (var i = 0; i < input.Length; i++)
        {
            prefix[i + 1] = prefix[i] + input[i];
            weightedPrefix[i + 1] = weightedPrefix[i] + input[i] * new ReferenceFraction(i + 1L);
        }
        for (var i = 0; i < input.Length; i++)
        {
            var total = new ReferenceFraction(0);
            if (kind == 1 && i + 1 < length) { result[i] = total; continue; }
            if (kind == 6 || kind == 3 && i >= length)
            {
                var previous = i == 0 ? total : result[i - 1];
                total = (previous * new ReferenceFraction(length - 1L) + input[i] * new ReferenceFraction(kind == 3 ? 2 : 1))
                    / new ReferenceFraction(kind == 3 ? length + 1L : length);
            }
            else
            {
                var start = Math.Max(0, i - length + 1);
                var sum = prefix[i + 1] - prefix[start];
                // Expand each chronological weight as (j + 1) + (length - i - 1).
                // Exact prefix differences preserve cancellation without sharing the production recurrence.
                total = kind == 2 ? weightedPrefix[i + 1] - weightedPrefix[start] + sum * new ReferenceFraction(length - i - 1L) : sum;
                total /= kind == 2 ? new ReferenceFraction(length) * new ReferenceFraction(length + 1L) / new ReferenceFraction(2)
                    : new ReferenceFraction(kind == 3 ? Math.Min(i + 1, length) : length);
            }
            result[i] = rounding is null ? RoundRocBankStage(total) : rounding(total);
        }
        return result;
    }

    internal static IReadOnlyDictionary<string, double[]> RocBankOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var kind = AverageKind(options, 1);
        int[] lookbacks, smoothing;
        int signal;
        string key;
        if (indicator.BatchName == IndicatorName.KnowSureThing)
        {
            lookbacks = new[] { 10, 15, 20, 30 }.Select((n, i) => Integer(options, "RocLength" + (i + 1), n)).ToArray();
            smoothing = new[] { Integer(options, "Length1", Integer(options, "Length", 10)),
                Integer(options, "Length2", 10), Integer(options, "Length3", 10), Integer(options, "Length4", 15) };
            signal = Integer(options, "SignalLength", 9); key = "Kst";
        }
        else
        {
            var periods = new[] { 10, 15, 20, 30, 40, 50, 65, 75, 100, 130, 195, 265, 390, 530 }
                .Select((n, i) => Integer(options, "Length" + (i + 1), n)).ToArray();
            lookbacks = new[] { 0, 1, 2, 3, 4, 6, 7, 8, 10, 11, 12, 13 }.Select(i => periods[i]).ToArray();
            smoothing = new[] { 0, 0, 0, 1, 5, 6, 7, 8, 9, 9, 9, 10 }.Select(i => periods[i]).ToArray();
            signal = Integer(options, "SmoothLength", 10); key = "PringSpecialK";
        }
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var legs = lookbacks.Select((period, leg) => SmoothRocBankStage(prices.Select((price, i) =>
            i < period || prices[i - period].Sign == 0 ? new ReferenceFraction(0) :
            RoundRocBankStage((price - prices[i - period]) * new ReferenceFraction(100) / prices[i - period])).ToArray(), smoothing[leg], kind)).ToArray();
        var line = prices.Select((_, i) =>
        {
            var total = new ReferenceFraction(0);
            for (var leg = 0; leg < legs.Length; leg++) total += legs[leg][i] * new ReferenceFraction(leg % 4 + 1);
            return RoundRocBankStage(total);
        }).ToArray();
        var signalLine = SmoothRocBankStage(line, signal, kind);
        return Outputs((key, line.Select(v => v.ToDouble()).ToArray()), ("Signal", signalLine.Select(v => v.ToDouble()).ToArray()));
    }
}
