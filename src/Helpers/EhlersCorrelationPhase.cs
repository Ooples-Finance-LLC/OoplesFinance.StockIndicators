namespace OoplesFinance.StockIndicators.Helpers;

internal static class EhlersCorrelationPhase
{
    internal static double MarketState(double angle, double previous)
    {
        // A 1e-10 degree ambiguity band at the strict nine-degree boundary is
        // neutral. This is a decision convention, not a proven angular error bound.
        return Math.Abs(angle - previous) < 9 - 1e-10 ? angle < 0 ? -1 : 1 : 0;
    }

    internal static double Angle(double real, double imaginary)
    {
        // Correlations are normalized; suppress roundoff on exact Fourier axes.
        if (Math.Abs(real) < 1e-12) real = 0;
        if (Math.Abs(imaginary) < 1e-12) imaginary = 0;
        if (real == 0 && imaginary == 0) return 90;
        return imaginary == 0 ? (real < 0 ? 180 : 0) : Math.Atan2(-imaginary, real) * 180 / Math.PI;
    }
}
