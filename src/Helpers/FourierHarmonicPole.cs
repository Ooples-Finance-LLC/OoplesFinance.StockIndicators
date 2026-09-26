namespace OoplesFinance.StockIndicators.Helpers;

internal static class FourierHarmonicPole
{
    internal static double For(double period, double bandwidth)
    {
        // Frequencies at/above Nyquist are unresolved. A unit pole with zero drive
        // leaves that harmonic at its zero initial condition.
        if (period <= 2) return 1;
        var angle = Math.Min(Math.PI/2, Math.Abs(bandwidth)*2*Math.PI/period);
        return Math.Cos(angle)/(1+Math.Sin(angle));
    }
}
