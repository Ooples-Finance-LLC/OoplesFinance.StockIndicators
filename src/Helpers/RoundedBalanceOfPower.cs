namespace OoplesFinance.StockIndicators.Helpers;

internal static class RoundedBalanceOfPower
{
    internal static double Of(double open, double high, double low, double close)
    {
        if (high == low) return 0; // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
#if !NETFRAMEWORK
        // When both differences are exactly representable, hardware division
        // rounds the complete exact ratio once, just like the accumulator path.
        // TwoDiff certifies this without restricting prices to a common grid.
        if (TryExactDifference(close, open, out var top) && TryExactDifference(high, low, out var bottom))
            return top == 0 ? 0 : top / bottom; // NOSONAR: exact cancellation has canonical positive zero.
#endif
        var numerator = new ExactMeanAccumulator();
        numerator.Add(close);
        numerator.Add(open, -1);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(high);
        denominator.Add(low, -1);
        return numerator.Ratio(denominator);
    }

#if !NETFRAMEWORK
    // Legacy Framework JITs may retain extended floating intermediates; keep
    // their existing accumulator route instead of assuming binary64 evaluation.
    internal static bool TryExactDifference(double left, double right, out double difference)
    {
        difference = left - right;
        if (double.IsInfinity(difference) || double.IsNaN(difference)) return false;
        // Knuth's error-free subtraction transform; gradual underflow preserves
        // subnormal residuals. Nonfinite intermediates cannot certify a zero error.
        var virtualRight = left - difference;
        var virtualLeft = difference + virtualRight;
        var rightError = virtualRight - right;
        var leftError = left - virtualLeft;
        return leftError + rightError == 0; // NOSONAR: an exactly zero residual is the certificate.
    }
#endif
}
