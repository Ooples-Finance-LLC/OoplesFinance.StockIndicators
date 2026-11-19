using OoplesFinance.StockIndicators.Builder;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>
/// The builder every comparison measures. COMPARISON_NUMERICS selects the numeric mode for the whole
/// process, so one run measures one mode: unset or Exact for the default route, Fast for the opt-in one.
/// </summary>
internal static class ComparisonBuilder
{
    internal static readonly IndicatorNumerics Numerics = Parse(Environment.GetEnvironmentVariable("COMPARISON_NUMERICS"));

    internal static StockIndicatorBuilder Create() => new StockIndicatorBuilder().ConfigureNumerics(Numerics);

    private static IndicatorNumerics Parse(string? text) =>
        string.IsNullOrEmpty(text) ? IndicatorNumerics.Exact
        : Enum.TryParse<IndicatorNumerics>(text, ignoreCase: true, out var numerics) && Enum.IsDefined(numerics) ? numerics
        : throw new InvalidOperationException("COMPARISON_NUMERICS must be Exact or Fast.");
}
