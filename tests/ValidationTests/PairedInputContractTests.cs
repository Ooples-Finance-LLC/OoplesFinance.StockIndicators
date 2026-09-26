using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PairedInputContractTests
{
    [Theory]
    [InlineData(SeriesAlignmentPolicy.Strict, 0)]
    [InlineData(SeriesAlignmentPolicy.LastKnown, 1)]
    public void CustomerSubscriptionChecksAlignmentBeforeCalculation(SeriesAlignmentPolicy policy, int expectedUpdates)
    {
        var primary = new SeriesKey("PRIMARY", BarTimeframe.Minutes(1));
        var benchmark = new SeriesKey("BENCHMARK", BarTimeframe.Minutes(1));
        var outputs = new List<double>();
        using var subscription = new StreamingIndicatorEngine.MultiSeriesIndicatorSubscription(primary,
            new[] { primary, benchmark }, new SharedIndicatorValidationTests.CustomerSpread(primary, benchmark),
            update => outputs.Add(update.Value), new IndicatorSubscriptionOptions
            { SeriesAlignmentPolicy = policy, MaximumBenchmarkAge = TimeSpan.FromMinutes(1) });
        subscription.HandleBar(benchmark, Bar(benchmark, 0));
        subscription.HandleBar(primary, Bar(primary, 1));
        Assert.Equal(expectedUpdates, outputs.Count);
        subscription.HandleBar(primary, Bar(primary, 2));
        Assert.Equal(expectedUpdates, outputs.Count); // Stale observations cannot produce a new output.
        subscription.HandleBar(benchmark, Bar(benchmark, 4));
        Assert.Throws<ArgumentException>(() => subscription.HandleBar(primary, Bar(primary, 3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => subscription.HandleBar(benchmark, Bar(benchmark, 5, double.NaN)));
        subscription.HandleBar(primary, Bar(primary, 4));
        Assert.Equal(expectedUpdates + 1, outputs.Count);
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.DiscoverMultiSeries(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Name == "default").Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Cases))]
    public void MissingInvalidDuplicateAndFutureEventsCannotContaminateCommittedState(MultiSeriesIndicatorValidationCase testCase)
    {
        var primary = new SeriesKey("PRIMARY", BarTimeframe.Minutes(1));
        var benchmark = new SeriesKey("BENCHMARK", BarTimeframe.Minutes(1));
        var actual = testCase.Factory(primary, benchmark); var control = testCase.Factory(primary, benchmark);
        using var actualLifetime = actual as IDisposable;
        using var controlLifetime = control as IDisposable;
        var context = new MultiSeriesContext(new SeriesStore());
        for (var replay = 0; replay < 2; replay++)
        {
            actual.Reset(); control.Reset();
            for (var i = 0; i < 128; i++)
            {
                if (i == 64)
                {
                    Assert.False(actual.Update(context, primary, Bar(primary, i), true, true).HasValue);
                    Assert.Throws<ArgumentException>(() => actual.Update(context, benchmark, Bar(benchmark, i - 1), true, true));
                    var invalid = Bar(benchmark, i, double.NaN);
                    Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(context, benchmark, invalid, true, true));
                }
                actual.Update(context, benchmark, Bar(benchmark, i), true, true);
                control.Update(context, benchmark, Bar(benchmark, i), true, true);
                Same(actual.Update(context, primary, Bar(primary, i), true, true),
                    control.Update(context, primary, Bar(primary, i), true, true));
            }
            actual.Update(context, benchmark, Bar(benchmark, 129), true, true);
            control.Update(context, benchmark, Bar(benchmark, 129), true, true);
            Assert.Throws<ArgumentException>(() => actual.Update(context, primary, Bar(primary, 128), true, true));
            Same(actual.Update(context, primary, Bar(primary, 129), true, true),
                control.Update(context, primary, Bar(primary, 129), true, true));
        }
    }

    private static void Same(MultiSeriesIndicatorStateResult actual, MultiSeriesIndicatorStateResult expected)
    {
        Assert.True(actual.HasValue); Assert.True(expected.HasValue);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
    }

    private static OhlcvBar Bar(SeriesKey series, int i, double? close = null)
    {
        var time = DateTime.UnixEpoch.AddMinutes(i);
        var value = 100 + (series.Symbol == "PRIMARY" ? i % 13 : i % 7) + .05 * i;
        return new(series.Symbol, series.Timeframe, time, time, value, value + 1, value - 1, close ?? value, 1000 + i, true);
    }
}
