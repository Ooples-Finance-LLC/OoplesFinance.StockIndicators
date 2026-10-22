using System.Linq.Expressions;
using System.Reflection;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Original PairedBatch.Run loop from 71c31b97. Measures dictionary publication
// versus direct values using the same numerical state in both arms.
internal static class PreservedSectorRotationBatch
{
    // The benchmark assembly has no internal-access grant. Bind construction
    // once during setup; timed runs create the same empty store/context without
    // reflection or argument arrays. The preserved numerical loop is unchanged.
    private static readonly Func<MultiSeriesContext> ContextFactory = BindContext();
    internal static MultiSeriesContext CreateContext() => ContextFactory();
    private static Func<MultiSeriesContext> BindContext()
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var store = typeof(MultiSeriesContext).Assembly.GetType("OoplesFinance.StockIndicators.Streaming.SeriesStore", true)!;
        var storeConstructor = store.GetConstructor(flags, null, Type.EmptyTypes, null)!;
        var contextConstructor = typeof(MultiSeriesContext).GetConstructor(flags, null,
            new[] { store, typeof(SeriesAlignmentPolicy), typeof(TimeSpan?) }, null)!;
        return Expression.Lambda<Func<MultiSeriesContext>>(Expression.New(contextConstructor,
            Expression.New(storeConstructor), Expression.Constant(SeriesAlignmentPolicy.Strict),
            Expression.Constant(null, typeof(TimeSpan?)))).Compile();
    }

    internal static Dictionary<string, List<double>> Run(IReadOnlyList<double> primary, IReadOnlyList<double> benchmark,
        Func<SeriesKey, SeriesKey, IMultiSeriesIndicatorState> factory)
    {
        var p = new SeriesKey("PRIMARY", BarTimeframe.Minutes(1));
        var b = new SeriesKey("BENCHMARK", BarTimeframe.Minutes(1));
        var state = factory(p, b);
        using var lifetime = state as IDisposable;
        var context = CreateContext();
        var result = new Dictionary<string, List<double>>();
        for (var i = 0; i < primary.Count; i++)
        {
            var time = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i);
            OhlcvBar Bar(SeriesKey key, double value) => new(key.Symbol, key.Timeframe, time, time, value, value, value, value, 0, true);
            state.Update(context, b, Bar(b, benchmark[i]), true, false);
            var point = state.Update(context, p, Bar(p, primary[i]), true, true);
            if (!point.HasValue || point.Outputs is null) throw new InvalidOperationException("An aligned pair did not publish outputs.");
            foreach (var output in point.Outputs)
            {
                if (!result.TryGetValue(output.Key, out var values)) result[output.Key] = values = new List<double>(primary.Count);
                values.Add(output.Value);
            }
        }
        return result;
    }
}
