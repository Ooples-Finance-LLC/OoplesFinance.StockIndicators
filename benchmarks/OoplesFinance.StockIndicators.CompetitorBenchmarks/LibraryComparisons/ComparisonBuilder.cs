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

    /// <summary>
    /// Values where the flag is set and null elsewhere. A loop, because this runs inside the timed arm and
    /// the competitor arms build the same shape with a loop.
    /// </summary>
    internal static double?[] WherePresent(this double[] values, double[] flags)
    {
        var result = new double?[values.Length];
        for (var i = 0; i < result.Length; i++)
            if (flags[i] > 0) result[i] = values[i];
        return result;
    }

    internal static double?[] WherePresent(this IEnumerable<double> values, double[] flags) =>
        WherePresent(values as double[] ?? values.ToArray(), flags);
}
