using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> KlingerValues(IReadOnlyList<Bar> bars, int fast, int slow, int signal, int kind)
    {
        ReferenceFraction I(long n) => new(n);
        var zero = I(0); var one = I(1); var two = I(2);
        var sums = bars.Select(b => ReferenceFraction.FromDouble(b.High) + ReferenceFraction.FromDouble(b.Low) + ReferenceFraction.FromDouble(b.Close)).ToArray();
        var ranges = bars.Select(b => ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(b.Low)).ToArray();
        var directions = new int[bars.Count];
        for (var i = 1; i < bars.Count; i++)
        { var comparison = sums[i].CompareTo(sums[i - 1]); directions[i] = comparison == 0 ? directions[i - 1] : comparison; }
        // Sum the complete current trend segment, including the bar before its reversal.
        // This does not reuse the production cumulative-range recurrence.
        var force = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var start = i;
            while (start > 0 && directions[start] == directions[start - 1]) start--;
            start = Math.Max(0, start - 1);
            var range = zero;
            for (var j = start; j <= i; j++) range += ranges[j];
            force[i] = range.Sign == 0 ? zero : ReferenceFraction.FromDouble(bars[i].Volume) *
                (two * ranges[i] / range - one).Abs() * I(100L * directions[i]);
        }
        ReferenceFraction[] Mean(ReferenceFraction[] values, int period)
        {
            period = Math.Max(1, period);
            var output = new ReferenceFraction[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var sum = zero;
                if (kind is 1 or 2 || kind == 3 && i < period)
                {
                    for (var j = Math.Max(0, i - period + 1); j <= i; j++)
                        sum += values[j] * I(kind == 2 ? (long)period - i + j : 1);
                    output[i] = kind == 1 && i < period - 1 ? zero : sum /
                        I(kind == 2 ? (long)period * (period + 1L) / 2 : kind == 3 ? i + 1L : period);
                }
                else
                {
                    var gain = I(kind == 3 ? 2 : 1) / I(kind == 3 ? period + 1L : period);
                    output[i] = values[i] * gain + (i == 0 ? zero : output[i - 1]) * (one - gain);
                }
            }
            return output;
        }
        var first = Mean(force, fast); var second = Mean(force, slow);
        var line = first.Zip(second, (a, b) => a - b).ToArray(); var smooth = Mean(line, signal);
        return new() { ["Kvo"] = line.Select(v => v.ToDouble()).ToArray(), ["KvoSignal"] = smooth.Select(v => v.ToDouble()).ToArray(),
            ["KvoHistogram"] = line.Zip(smooth, (a, b) => (a - b).ToDouble()).ToArray() };
    }
}
