using Xunit;

namespace OoplesFinance.StockIndicators.Tests.PerformanceTests;

/// <summary>
/// Runs the throughput and latency benchmarks on their own, never alongside other tests.
/// </summary>
/// <remarks>
/// <para>
/// These tests assert absolute rates - more than 1000 VaR calculations a second, for instance - and xUnit
/// runs test classes in parallel by default. On a two-core CI runner a benchmark sharing the CPU with a
/// heavy test class measures the contention rather than the code: ConcurrentVaRCalculations failed at 626
/// and 646 a second on two consecutive runs of a branch whose only change near it was a much heavier
/// sweep in another class, while its own code path (PortfolioRiskCalculator) was untouched.
/// </para>
/// <para>
/// A [Collection] attribute alone does not do this - without a matching [CollectionDefinition] with
/// DisableParallelization it silently loses the guarantee.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceCollection
{
    /// <summary>The collection name the benchmark classes opt into.</summary>
    public const string Name = "Performance";
}
