using OoplesFinance.StockIndicators.Builder;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>
/// The builder every comparison measures. COMPARISON_NUMERICS selects the numeric mode for the whole
/// process, so one run measures one mode: unset or Exact for the default route, Fast for the opt-in one.
/// </summary>
internal static class ComparisonBuilder
{
    internal static readonly IndicatorNumerics Numerics = Parse(Environment.GetEnvironmentVariable("COMPARISON_NUMERICS"));

    [ThreadStatic] private static StockIndicatorBuilder? _latest;

    internal static StockIndicatorBuilder Create() => _latest = new StockIndicatorBuilder().ConfigureNumerics(Numerics);

    /// <summary>
    /// What the most recent build on this thread reported about the route it took, or null when the arm
    /// did not go through a builder. The scoreboard prints it so a pair that stayed on the exact route in
    /// fast mode is visible as such.
    /// </summary>
    internal static string? LatestRoute => _latest?.LastExecution?.Reason;

    internal static void ForgetLatest() => _latest = null;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<OoplesFinance.StockIndicators.Indicators.Bar[],
        OoplesFinance.StockIndicators.Indicators.IBarSource> Columns = new();

    /// <summary>
    /// The source an Ooples arm reads. The exact mode reads the bar array, as it always has. The fast mode
    /// borrows columns, which is the input it is designed for and the input the TA-Lib arms are handed: the
    /// fixture's open, high, low, close and volume as arrays, split out once per fixture and not per call.
    /// </summary>
    internal static OoplesFinance.StockIndicators.Indicators.IBarSource Source(
        IEnumerable<OoplesFinance.StockIndicators.Indicators.Bar> bars)
    {
        if (Numerics != IndicatorNumerics.Fast || bars is not OoplesFinance.StockIndicators.Indicators.Bar[] array)
            return OoplesFinance.StockIndicators.Indicators.Bars.From(bars);
        return Columns.GetValue(array, static source =>
        {
            int n = source.Length;
            double[] open = new double[n], high = new double[n], low = new double[n], close = new double[n], volume = new double[n];
            var times = new DateTime[n];
            for (var i = 0; i < n; i++)
            {
                var bar = source[i];
                open[i] = bar.Open;
                high[i] = bar.High;
                low[i] = bar.Low;
                close[i] = bar.Close;
                volume[i] = bar.Volume;
                times[i] = bar.Time;
            }
            return OoplesFinance.StockIndicators.Indicators.Bars.FromColumns(open, high, low, close, volume, times);
        });
    }

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
