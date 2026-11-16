namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Completed spans are projected into independently owned comparison columns.
// The producer still builds and retains its ordinary Full history and outputs.
internal static class NamedComparisonPublisher
{
    internal static ComparisonOutput Column(ReadOnlySpan<double> values, ReadOnlySpan<double> flags)
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
        return new ComparisonOutput(0, published, present);
    }

    internal static ComparisonSeries Single(ReadOnlySpan<double> values, ReadOnlySpan<double> flags) =>
        new(new Dictionary<string, ComparisonOutput> { ["Value"] = Column(values, flags) });
}
