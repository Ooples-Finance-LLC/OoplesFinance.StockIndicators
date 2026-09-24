namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static double[] AnticipateReference(double[] filtered, int length) => filtered.Select((_, i) =>
    {
        if (length <= 2) return 0d;
        var samples = Enumerable.Range(0, length).Select(lag => i < lag ? 0 : filtered[i-lag]).ToArray();
        if (samples.Max()-samples.Min() <= 1e-12*samples.Max(Math.Abs)) return 0d;
        var center = samples.Average();
        var energy = samples.Sum(v => (v-center)*(v-center));
        var scores = Enumerable.Range(0, length).Select(phase =>
        {
            // Every complete sampled sine period has zero mean. Explicitly center
            // the finite precision samples to preserve the correlation definition.
            var wave = Enumerable.Range(0, length).Select(lag => -Math.Sin(2*Math.PI*(lag+phase)/length)).ToArray();
            var mean = wave.Average();
            var norm = energy*wave.Sum(v => (v-mean)*(v-mean));
            return norm == 0 ? 0 : samples.Select((v, j) => (v-center)*(wave[j]-mean)).Sum()/Math.Sqrt(norm);
        }).ToArray();
        var winner = 0;
        for (var phase = 1; phase < length; phase++)
            if (scores[phase] > scores[winner]+1e-12) winner = phase;
        return -Math.Sin(2*Math.PI*winner/length);
    }).ToArray();
}
