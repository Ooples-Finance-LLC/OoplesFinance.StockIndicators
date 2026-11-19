namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Price momentum oscillator: the one-bar percentage change smoothed twice with mean-seeded
    /// averages, the first scaled by ten, and a signal average of the result. A zero close ends the series,
    /// because the change after it has no base.
    /// </summary>
    internal static void PriceMomentum(ReadOnlySpan<double> close, double[][] outputs, SeededPriceMomentum owner)
    {
        var n = close.Length;
        int period = owner.Period, smooth = owner.SmoothPeriod, signalPeriod = owner.SignalPeriod;
        Span<double> momentum = outputs[0].AsSpan(0, n), signal = outputs[1].AsSpan(0, n),
            momentumDefined = outputs[2].AsSpan(0, n), signalDefined = outputs[3].AsSpan(0, n);
        momentum.Clear();
        signal.Clear();
        momentumDefined.Clear();
        signalDefined.Clear();
        long secondStart = (long)period + smooth - 1, signalStart = secondStart + signalPeriod - 1;
        double first = 0, second = 0, line = 0, sum = 0, secondSum = 0, signalSum = 0;
        double firstWeight = period - 2, secondWeight = smooth - 2, signalWeight = signalPeriod - 1;
        for (var i = 1; i < n; i++)
        {
            var previous = close[i - 1];
            if (previous == 0) return;
            var change = (close[i] - previous) * 100 / previous;
            if (i <= period)
            {
                sum += change;
                if (i < period) continue;
                first = sum / period;
            }
            else
                first = (first * firstWeight + 2 * change) / period;
            var scaled = first * 10;
            if (i <= secondStart)
            {
                secondSum += scaled;
                if (i < secondStart) continue;
                second = secondSum / smooth;
            }
            else
                second = (second * secondWeight + 2 * scaled) / smooth;
            momentum[i] = second;
            momentumDefined[i] = 1;
            if (i <= signalStart)
            {
                signalSum += second;
                if (i < signalStart) continue;
                line = signalSum / signalPeriod;
            }
            else
                line = (line * signalWeight + 2 * second) / (signalPeriod + 1.0);
            signal[i] = line;
            signalDefined[i] = 1;
        }
    }

    /// <summary>
    /// Bands a multiple of the window's population deviation above and below a classic average of the close.
    /// </summary>
    internal static void ClassicBands(ReadOnlySpan<double> close, double[][] outputs, ClassicDeviationBands owner)
    {
        var n = close.Length;
        var period = owner.Period;
        Span<double> upper = outputs[0].AsSpan(0, n), middle = outputs[1].AsSpan(0, n), lower = outputs[2].AsSpan(0, n);
        var simple = owner.Method == ClassicAverageMethod.Sma;
        var first = simple ? period - 1L : Math.Max(period - 1L, owner.Average.First);
        for (var slot = 3; slot < 6; slot++) DefinedFrom(outputs[slot], first);
        var deviation = FastMath.Allocate(n);
        Dispersion(close, deviation, new WindowDispersion(period, WindowDispersionOutput.StandardDeviation, false, 1));
        if (simple) Sma(close, middle, period);
        else ClassicAverage(close, middle, owner.Average, owner.Average.First);
        var start = (int)Math.Min(first, n);
        upper.Slice(0, start).Clear();
        middle.Slice(0, start).Clear();
        lower.Slice(0, start).Clear();
        double upperFactor = owner.UpperFactor, lowerFactor = owner.LowerFactor;
        for (var i = start; i < n; i++)
        {
            double center = middle[i], spread = deviation[i];
            upper[i] = center + spread * upperFactor;
            lower[i] = center - spread * lowerFactor;
        }
    }
}
