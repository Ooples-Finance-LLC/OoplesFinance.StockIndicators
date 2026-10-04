using OoplesFinance.StockIndicators.Indicators;
using R = OoplesFinance.StockIndicators.Validation.ConfluenceReferenceWave.R;
using W = OoplesFinance.StockIndicators.Validation.ConfluenceReferenceWave;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] ConfluenceExact(IReadOnlyList<Bar> bars, int length, int kind, bool selectedInput = false)
    {
        var n = Math.Max(1, length); var periods = new long[] { n, 2L * n - 1, 4L * n - 3, 8L * n - 7 };
        var delays = periods.Take(3).Select(p => p / 2).ToArray(); var shortPeriod = Math.Max(2, Math.Min(530, n - 1));
        var close = bars.Select(b => R.Of(b.Close)).ToArray();
        R[] Average(R[] values, long period)
        {
            var output = new R[values.Length];
            for (var i = 0; i < output.Length; i++)
            {
                if (kind == 6) { output[i] = ((i == 0 ? (R)0 : output[i - 1]) * (period - 1) + values[i]) / period; continue; }
                if (kind == 3 && i >= period) { output[i] = output[i - 1] + (R)2 / (period + 1) * (values[i] - output[i - 1]); continue; }
                if (kind == 1 && i + 1 < period) continue;
                R sum = 0;
                for (var j = (int)Math.Max(0, i - period + 1); j <= i; j++) sum += values[j] * (kind == 2 ? period - i + j : 1);
                output[i] = sum / (kind == 2 ? (R)period * (period + 1) / 2 : kind == 3 ? (R)(i + 1) : (R)period);
            }
            return output;
        }
        var means = periods.Select(p => Average(close, p)).ToArray();
        var shorter = Average(close, shortPeriod); var last = Average(close, Math.Max(1, periods[3] - 1));
        var full = Average(selectedInput ? close : bars.Select(b => (R.Of(b.Open) + R.Of(b.High) + R.Of(b.Low) + R.Of(b.Close)) / 4).ToArray(), Math.Max(1, periods[3] - 1));
        R At(R[] values, long index) => index < 0 ? (R)0 : values[(int)index];
        W WaveAt(W[] values, long index) => index < 0 ? W.Zero : values[(int)index];
        var projected = means.Select(m => m.Select((v, i) => 2 * v - At(m, i - 1)).ToArray()).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            projected[0][i] += (R)(n - 1 - shortPeriod) / n * shorter[i];
            projected[3][i] += (R)(periods[3] - 1) / periods[3] * (full[i] - last[i]);
        }
        var primes = W.Factors(periods.Concat(periods.Select(p => p + 1)).Concat(new long[] { shortPeriod, shortPeriod + 1, Math.Max(1, periods[3] - 1) })
            .Concat(Enumerable.Range(1, bars.Count).Select(i => (long)i)));
        var momentum = new R[bars.Count]; var benchmark = new R[bars.Count]; var spread = new R[bars.Count];
        var cyclic = new W[bars.Count]; var errors = new W[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            cyclic[i] = W.Zero; var baseline = W.Zero;
            for (var stage = 0; stage < 3; stage++)
            {
                momentum[i] += projected[stage + 1][i] - At(projected[stage], i - delays[stage]);
                benchmark[i] += means[stage + 1][i] - At(means[stage], i - delays[stage]);
                cyclic[i] += W.Wave(projected[stage][i]); baseline += W.Wave(means[stage][i]);
            }
            var flip = (cyclic[i] - WaveAt(cyclic, i - delays[1])).Sign(primes) * (means[0][i] - At(means[0], i - delays[1])).Sign < 0;
            errors[i] = (cyclic[i] - baseline).Times(flip ? -1 : 1);
            spread[i] = projected[0][i] - projected[3][i];
        }
        int Vote(int sign, int motion, int position) => sign == 0 || motion == 0 || position == 0 ? 0
            : motion == sign && position == sign ? 3 * sign : motion != sign && position != sign ? sign : 2 * sign;
        return Enumerable.Range(0, bars.Count).Select(i =>
        {
            var errorSignal = W.Zero;
            if (delays[1] > 0)
            {
                for (var j = (int)Math.Max(0, i - delays[1] + 1); j <= i; j++) errorSignal += errors[j];
                errorSignal = errorSignal.Times((R)1 / Math.Min(i + 1L, delays[1]));
            }
            R spreadSignal = 0;
            for (var j = Math.Max(0, i - n + 1); j <= i; j++) spreadSignal += spread[j];
            spreadSignal /= Math.Min(i + 1, n);
            var total = Vote(errors[i].Sign(primes), (errors[i] - WaveAt(errors, i - 1)).Sign(primes), (errors[i] - errorSignal).Sign(primes))
                + Vote(momentum[i].Sign, (momentum[i] - At(momentum, i - 1)).Sign, (momentum[i] - benchmark[i]).Sign)
                + Vote(spread[i].Sign, (spread[i] - At(spread, i - 1)).Sign, (spread[i] - spreadSignal).Sign);
            return spread[i].Sign == 0 ? 0 : total * spread[i].Sign > 0 ? total : total / 10d;
        }).ToArray();
    }
}
