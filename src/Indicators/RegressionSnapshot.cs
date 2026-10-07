namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>One row of a retrospective regression snapshot.</summary>
/// <param name="Slope">Rolling full-window slope, or null before the first full window.</param>
/// <param name="Intercept">Global one-based intercept, or null before the first full window.</param>
/// <param name="StandardDeviation">Population deviation, or null before the first full window.</param>
/// <param name="RSquared">Squared time/close correlation, or null during startup or for a flat window.</param>
/// <param name="Line">Final window's fitted line at this row, or null outside that final window.</param>
public sealed record RegressionSnapshotValue(
    double? Slope,
    double? Intercept,
    double? StandardDeviation,
    double? RSquared,
    double? Line
);

/// <summary>Batch regression statistics with a retrospective overlay of the final fitted line.</summary>
/// <remarks>The Line field repaints when more data is supplied: only the last period rows
/// receive the fit from the final window. Other fields describe each row's own completed
/// rolling window. Use WindowRegressionStatistics for causal window-local outputs.</remarks>
public static class RegressionSnapshot
{
    /// <summary>Fits closes using global x coordinates 1..bars.Count and overlays the final window's line.</summary>
    /// <param name="bars">Finite closes in chronological order. No input is changed.</param>
    /// <param name="period">Full rolling window size, at least two.</param>
    /// <returns>One independent result per supplied bar, including startup rows.</returns>
    /// <exception cref="ArgumentNullException">The input list is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The period is below two or a close is nonfinite.</exception>
    /// <exception cref="OverflowException">A published statistic is not representable as a finite double.</exception>
    public static IReadOnlyList<RegressionSnapshotValue> Calculate(
        IReadOnlyList<Bar> bars,
        int period = 14
    )
    {
        if (bars is null)
            throw new ArgumentNullException(nameof(bars));
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        foreach (var bar in bars)
            if (double.IsNaN(bar.Close) || double.IsInfinity(bar.Close))
                throw new ArgumentOutOfRangeException(nameof(bars), "Closes must be finite.");
        var result = new RegressionSnapshotValue[bars.Count];
        // No full window means no published outputs, including the retrospective overlay.
        if (bars.Count < period)
        {
            for (var i = 0; i < bars.Count; i++)
                result[i] = new(null, null, null, null, null);
            return result;
        }
        using var window = new WindowRegressionStatistics.State(period);
        Span<double> output = stackalloc double[10];
        for (var i = 0; i < bars.Count; i++)
        {
            window.Update(bars[i], output);
            result[i] =
                i + 1 < period
                    ? new(null, null, null, null, null)
                    : new(
                        Finite(output[0]),
                        Finite(window.GlobalIntercept),
                        Finite(output[2]),
                        output[8] > 0 ? Finite(output[3]) : null,
                        null
                    );
        }
        for (var position = 0; position < period; position++)
        {
            var index = bars.Count - period + position;
            result[index] = result[index] with { Line = Finite(window.LineAt(position)) };
        }
        return result;
    }

    private static double Finite(double value) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? throw new OverflowException(
                "The regression statistic is not representable as a finite double."
            )
            : value;
}
