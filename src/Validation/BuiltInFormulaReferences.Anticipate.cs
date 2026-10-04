using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] AnticipateValues(IReadOnlyList<Bar> bars, int length, double bandwidth, int kind)
    {
        length = Math.Max(1, length);
        var filtered = new ReferenceFraction[bars.Count];
        ImpulseResponseValues(bars, length, bandwidth, kind, filtered);
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (length <= 2) continue;
            var count = Math.Min(length, i + 1);
            var total = new ReferenceFraction(0); var energy = new ReferenceFraction(0);
            for (var lag = 0; lag < count; lag++) { total += filtered[i-lag]; energy += filtered[i-lag]*filtered[i-lag]; }
            if ((new ReferenceFraction(length)*energy-total*total).Sign == 0) continue;
            var mean = total/new ReferenceFraction(length);
            var winner = 0; var best = new ReferenceFraction(0); var bestNorm = new ReferenceFraction(1);
            for (var phase = 0; phase < length; phase++)
            {
                var wave = Enumerable.Range(0,length).Select(lag => ReferenceFraction.FromDouble(-Math.Sin(2*Math.PI*((long)phase+lag)/length))).ToArray();
                var center = wave.Aggregate(new ReferenceFraction(0),(sum,v)=>sum+v)/new ReferenceFraction(length);
                var covariance = new ReferenceFraction(0); var norm = new ReferenceFraction(0);
                for (var lag = 0; lag < length; lag++)
                {
                    var deviation = wave[lag]-center;
                    covariance += ((lag<count ? filtered[i-lag] : new ReferenceFraction(0))-mean)*deviation;
                    norm += deviation*deviation;
                }
                if (norm.Sign == 0) { covariance = new(0); norm = new(1); }
                if (phase == 0 || covariance.Sign > best.Sign || covariance.Sign == best.Sign && covariance.Sign*(covariance*covariance*bestNorm).CompareTo(best*best*norm)>0)
                { winner=phase; best=covariance; bestNorm=norm; }
            }
            result[i]=-Math.Sin(2*Math.PI*winner/length);
        }
        return result;
    }
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
