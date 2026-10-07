namespace OoplesFinance.StockIndicators.Helpers;

internal static class EhlersFirstOrderCoefficient
{
    // (cos(w)+sin(w)-1)/cos(w) = 2*tan(w/2)/(1+tan(w/2)).
    // The half-angle form avoids cancellation at long periods and the removable
    // singularity at w=pi/2. A cutoff above Nyquist saturates at its w=pi limit.
    internal static double Alpha(double period)
    {
        if (period <= 2) return 2;
        var tangent = Math.Tan(Math.PI / period);
        return 2 * tangent / (1 + tangent);
    }
}
