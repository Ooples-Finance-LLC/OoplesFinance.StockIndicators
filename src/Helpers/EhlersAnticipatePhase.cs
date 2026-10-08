namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Matches an impulse response against a complete periodic sine basis.</summary>
internal sealed class EhlersAnticipatePhase
{
    private readonly double[][] _waves;
    internal EhlersAnticipatePhase(int length)
    {
        _waves = new double[length][];
        for (var phase = 0; phase < length; phase++)
        {
            _waves[phase] = new double[length];
            for (var lag = 0; lag < length; lag++)
                _waves[phase][lag] = -Math.Sin(2 * Math.PI * (phase + lag) / length);
        }
    }
    internal double Predict(double[] history)
    {
        // One and two samples cannot resolve this sine basis.
        if (history.Length <= 2) return 0;
        var low = history.Min(); var high = history.Max();
        if (high-low <= 1e-12*Math.Max(Math.Abs(low), Math.Abs(high))) return 0;
        var best = double.NegativeInfinity;
        var phase = 0;
        for (var candidate = 0; candidate < _waves.Length; candidate++)
        {
            var score = WindowCorrelation.Pearson(history, _waves[candidate]);
            // Retain the first phase for a numerical tie in correlations bounded by one.
            if (score > best + 1e-12) { best = score; phase = candidate; }
        }
        return -Math.Sin(2 * Math.PI * phase / history.Length);
    }
}
