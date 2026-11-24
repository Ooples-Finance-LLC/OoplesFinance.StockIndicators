namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Completed spans are projected into independently owned comparison columns.
// The producer still builds and retains its ordinary Full history and outputs.
internal static class NamedComparisonPublisher
{
    internal static ComparisonOutput Column(ReadOnlySpan<double> values, ReadOnlySpan<double> flags) =>
        PointwiseComparisonPublisher.Publish(values, flags).Outputs["Value"];

    internal static ComparisonSeries Single(ReadOnlySpan<double> values, ReadOnlySpan<double> flags) =>
        PointwiseComparisonPublisher.Publish(values, flags);
}
