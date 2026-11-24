namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Project completed spans directly into the same independently owned comparison
// output. The builder still constructs and retains its normal Full history.
internal static class PointwiseComparisonPublisher
{
    internal static ComparisonSeries Publish(ReadOnlySpan<double> values, ReadOnlySpan<double> flags)
    {
        if (values.Length != flags.Length)
            throw new ArgumentException("Values and presence must have the same length.");
        var published = new double[values.Length];
        var present = new bool[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            bool defined = flags[i] > 0;
            present[i] = defined;
            published[i] = defined ? values[i] : double.NaN;
        }
        return new ComparisonSeries(new Dictionary<string, ComparisonOutput>
        {
            ["Value"] = new(0, published, present)
        });
    }
}
