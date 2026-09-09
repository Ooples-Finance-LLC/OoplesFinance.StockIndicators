using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class StreamingIndicatorEngineTests
{
    [Fact]
    public void RegistersIndicatorAndEmitsUpdates()
    {
        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        IndicatorUpdate? last = null;
        using var registration = engine.RegisterIndicator(
            "AAPL",
            BarTimeframe.Tick,
            data => data.CalculateSimpleMovingAverage(2),
            update => last = update,
            new IndicatorSubscriptionOptions { IncludeUpdates = false });

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        engine.OnTrade(new StreamTrade("AAPL", start, 10, 1));
        engine.OnTrade(new StreamTrade("AAPL", start.AddSeconds(1), 20, 1));

        last.Should().NotBeNull();
        last!.IndicatorData.CustomValuesList.Should().NotBeNullOrEmpty();
        last.IndicatorData.CustomValuesList[^1].Should().BeApproximately(15, 1e-10);
        last.IsFinalBar.Should().BeTrue();
    }

    [Fact]
    public void FansOutTradesAcrossMultipleTimeframes()
    {
        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        var updates = new List<IndicatorUpdate>();
        engine.RegisterIndicator(
            "AAPL",
            new[] { BarTimeframe.Tick, BarTimeframe.Seconds(1) },
            data => data.CalculateSimpleMovingAverage(2),
            update => updates.Add(update),
            new IndicatorSubscriptionOptions { IncludeUpdates = false });

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        engine.OnTrade(new StreamTrade("AAPL", start, 10, 1));
        engine.OnTrade(new StreamTrade("AAPL", start.AddSeconds(1), 20, 1));
        engine.OnTrade(new StreamTrade("AAPL", start.AddSeconds(2), 30, 1));

        var tickCount = 0;
        var secondCount = 0;
        for (var i = 0; i < updates.Count; i++)
        {
            var update = updates[i];
            if (update.Timeframe.Equals(BarTimeframe.Tick))
            {
                tickCount++;
            }
            else if (update.Timeframe.Equals(BarTimeframe.Seconds(1)))
            {
                secondCount++;
            }
        }

        tickCount.Should().Be(3);
        secondCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void RegistersStatefulIndicatorAndEmitsUpdates()
    {
        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        StreamingIndicatorStateUpdate? last = null;
        using var registration = engine.RegisterStatefulIndicator(
            "AAPL",
            BarTimeframe.Tick,
            new SimpleMovingAverageState(2),
            update => last = update,
            new IndicatorSubscriptionOptions { IncludeUpdates = false, IncludeOutputValues = true });

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        engine.OnTrade(new StreamTrade("AAPL", start, 10, 1));
        engine.OnTrade(new StreamTrade("AAPL", start.AddSeconds(1), 20, 1));

        last.Should().NotBeNull();
        last!.Indicator.Should().Be(IndicatorName.SimpleMovingAverage);
        last.Value.Should().BeApproximately(15, 1e-10);
        last.Outputs.Should().NotBeNull();
        last.Outputs!["Sma"].Should().BeApproximately(15, 1e-10);
        last.IsFinalBar.Should().BeTrue();
    }

    [Fact]
    public void StatefulIndicatorPreviewKeepsStateAndReturnsOutputs()
    {
        var state = new SimpleMovingAverageState(2);
        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        var bar1 = new OhlcvBar("AAPL", BarTimeframe.Tick, start, start,
            10, 10, 10, 10, 0, true);
        var bar2Preview = new OhlcvBar("AAPL", BarTimeframe.Tick,
            start.AddSeconds(1), start.AddSeconds(1),
            20, 20, 20, 20, 0, false);
        var bar2Final = new OhlcvBar("AAPL", BarTimeframe.Tick,
            start.AddSeconds(1), start.AddSeconds(1),
            20, 20, 20, 20, 0, true);
        var bar3 = new OhlcvBar("AAPL", BarTimeframe.Tick,
            start.AddSeconds(2), start.AddSeconds(2),
            30, 30, 30, 30, 0, true);

        var first = state.Update(bar1, isFinal: true, includeOutputs: true);
        first.Value.Should().BeApproximately(0, 1e-10);
        first.Outputs.Should().NotBeNull();
        first.Outputs!["Sma"].Should().BeApproximately(0, 1e-10);

        var preview = state.Update(bar2Preview, isFinal: false, includeOutputs: true);
        preview.Value.Should().BeApproximately(15, 1e-10);
        preview.Outputs.Should().NotBeNull();
        preview.Outputs!["Sma"].Should().BeApproximately(15, 1e-10);

        var final = state.Update(bar2Final, isFinal: true, includeOutputs: true);
        final.Value.Should().BeApproximately(15, 1e-10);
        final.Outputs.Should().NotBeNull();
        final.Outputs!["Sma"].Should().BeApproximately(15, 1e-10);

        var next = state.Update(bar3, isFinal: true, includeOutputs: false);
        next.Value.Should().BeApproximately(25, 1e-10);
        next.Outputs.Should().BeNull();
    }

    [Fact]
    public void RegistersMultiSeriesIndicatorAndEmitsOnPrimaryUpdates()
    {
        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        var updates = new List<MultiSeriesIndicatorStateUpdate>();
        var primary = new SeriesKey("AAPL", BarTimeframe.Minutes(1));
        var secondary = new SeriesKey("MSFT", BarTimeframe.Minutes(1));
        using var registration = engine.RegisterMultiSeriesIndicator(
            primary,
            new[] { secondary },
            new MultiSeriesSumState(primary, secondary),
            update => updates.Add(update));

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        engine.OnBar(new OhlcvBar("MSFT", BarTimeframe.Minutes(1), start, start.AddMinutes(1),
            10, 10, 10, 10, 100, true));
        engine.OnBar(new OhlcvBar("AAPL", BarTimeframe.Minutes(1), start.AddMinutes(1), start.AddMinutes(2),
            20, 20, 20, 20, 100, true));

        updates.Count.Should().Be(1);
        updates[0].PrimarySeries.Equals(primary).Should().BeTrue();
        updates[0].Value.Should().BeApproximately(30, 1e-10);
    }

    [Fact]
    public void MultiSeriesStrictAlignmentUsesFinalBarsWhenUpdatesExcluded()
    {
        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        var updates = new List<MultiSeriesIndicatorStateUpdate>();
        var primary = new SeriesKey("AAPL", BarTimeframe.Minutes(1));
        var secondary = new SeriesKey("MSFT", BarTimeframe.Minutes(1));
        var options = new IndicatorSubscriptionOptions
        {
            IncludeUpdates = false,
            SeriesAlignmentPolicy = SeriesAlignmentPolicy.Strict
        };

        using var registration = engine.RegisterMultiSeriesIndicator(
            primary,
            new[] { secondary },
            new MultiSeriesSumState(primary, secondary),
            update => updates.Add(update),
            options);

        var start = new DateTime(2024, 1, 2, 9, 30, 0, DateTimeKind.Utc);
        var firstEnd = start.AddMinutes(1);
        var secondEnd = start.AddMinutes(2);

        engine.OnBar(new OhlcvBar("AAPL", BarTimeframe.Minutes(1), start, firstEnd,
            10, 10, 10, 10, 100, true));
        engine.OnBar(new OhlcvBar("MSFT", BarTimeframe.Minutes(1), start, firstEnd,
            5, 5, 5, 5, 100, false));

        updates.Should().BeEmpty();

        engine.OnBar(new OhlcvBar("MSFT", BarTimeframe.Minutes(1), firstEnd, secondEnd,
            7, 7, 7, 7, 100, true));
        engine.OnBar(new OhlcvBar("AAPL", BarTimeframe.Minutes(1), firstEnd, secondEnd,
            12, 12, 12, 12, 100, true));

        updates.Count.Should().Be(1);
        updates[0].IsFinalBar.Should().BeTrue();
        updates[0].Value.Should().BeApproximately(19, 1e-10);
    }

    [Fact]
    public void ComparePriceMomentumOscillatorMatchesBatch()
    {
        var stockData = GlobalTestData.StockTestData;
        var marketData = GlobalTestData.MarketTestData;
        stockData.Should().NotBeNullOrEmpty();
        marketData.Should().NotBeNullOrEmpty();

        var maxCount = Math.Min(stockData.Count, marketData.Count);
        maxCount.Should().BeGreaterThan(0);

        var sampleCount = Math.Min(200, maxCount);
        var stockSlice = stockData.Take(sampleCount).ToList();
        var marketSlice = marketData.Take(sampleCount).ToList();

        var batchValues = new StockData(stockSlice)
            .CalculateComparePriceMomentumOscillator(new StockData(marketSlice))
            .OutputValues["Cpmo"];
        batchValues.Should().NotBeNullOrEmpty();

        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        var updates = new List<MultiSeriesIndicatorStateUpdate>();
        var primary = new SeriesKey("AAPL", BarTimeframe.Days(1));
        var market = new SeriesKey("SP500", BarTimeframe.Days(1));
        var options = new IndicatorSubscriptionOptions
        {
            IncludeUpdates = false,
            SeriesAlignmentPolicy = SeriesAlignmentPolicy.Strict
        };

        using var registration = engine.RegisterMultiSeriesIndicator(
            primary,
            new[] { market },
            new ComparePriceMomentumOscillatorState(primary, market),
            update => updates.Add(update),
            options);

        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < sampleCount; i++)
        {
            var start = baseTime.AddDays(i);
            var end = start.AddDays(1);
            var marketBar = marketSlice[i];
            engine.OnBar(new OhlcvBar("SP500", BarTimeframe.Days(1), start, end,
                marketBar.Open, marketBar.High, marketBar.Low, marketBar.Close, marketBar.Volume, true));

            var stockBar = stockSlice[i];
            engine.OnBar(new OhlcvBar("AAPL", BarTimeframe.Days(1), start, end,
                stockBar.Open, stockBar.High, stockBar.Low, stockBar.Close, stockBar.Volume, true));
        }

        updates.Count.Should().Be(sampleCount);
        for (var i = 0; i < sampleCount; i++)
        {
            updates[i].Value.Should().BeApproximately(batchValues[i], 1e-10, $"Cpmo mismatch at index {i}");
        }
    }

    [Fact]
    public void KaufmanStressIndicatorMatchesBatch()
    {
        var stockData = GlobalTestData.StockTestData;
        var marketData = GlobalTestData.MarketTestData;
        stockData.Should().NotBeNullOrEmpty();
        marketData.Should().NotBeNullOrEmpty();

        var maxCount = Math.Min(stockData.Count, marketData.Count);
        maxCount.Should().BeGreaterThan(0);

        var sampleCount = Math.Min(200, maxCount);
        var stockSlice = stockData.Take(sampleCount).ToList();
        var marketSlice = marketData.Take(sampleCount).ToList();

        var batchValues = new StockData(stockSlice)
            .CalculateKaufmanStressIndicator(new StockData(marketSlice))
            .OutputValues["Ksi"];
        batchValues.Should().NotBeNullOrEmpty();

        var engine = new StreamingIndicatorEngine(new StreamingIndicatorEngineOptions
        {
            EmitUpdates = false
        });

        var updates = new List<MultiSeriesIndicatorStateUpdate>();
        var primary = new SeriesKey("AAPL", BarTimeframe.Days(1));
        var market = new SeriesKey("SP500", BarTimeframe.Days(1));
        var options = new IndicatorSubscriptionOptions
        {
            IncludeUpdates = false,
            SeriesAlignmentPolicy = SeriesAlignmentPolicy.Strict
        };

        using var registration = engine.RegisterMultiSeriesIndicator(
            primary,
            new[] { market },
            new KaufmanStressIndicatorState(primary, market),
            update => updates.Add(update),
            options);

        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < sampleCount; i++)
        {
            var start = baseTime.AddDays(i);
            var end = start.AddDays(1);
            var marketBar = marketSlice[i];
            engine.OnBar(new OhlcvBar("SP500", BarTimeframe.Days(1), start, end,
                marketBar.Open, marketBar.High, marketBar.Low, marketBar.Close, marketBar.Volume, true));

            var stockBar = stockSlice[i];
            engine.OnBar(new OhlcvBar("AAPL", BarTimeframe.Days(1), start, end,
                stockBar.Open, stockBar.High, stockBar.Low, stockBar.Close, stockBar.Volume, true));
        }

        updates.Count.Should().Be(sampleCount);
        for (var i = 0; i < sampleCount; i++)
        {
            updates[i].Value.Should().BeApproximately(batchValues[i], 1e-10, $"Ksi mismatch at index {i}");
        }
    }

    private sealed class MultiSeriesSumState : IMultiSeriesIndicatorState       
    {
        private readonly SeriesKey _left;
        private readonly SeriesKey _right;

        public MultiSeriesSumState(SeriesKey left, SeriesKey right)
        {
            _left = left;
            _right = right;
        }

        public IndicatorName Name => IndicatorName.SimpleMovingAverage;

        public void Reset()
        {
        }

        public MultiSeriesIndicatorStateResult Update(MultiSeriesContext context, SeriesKey series, OhlcvBar bar,
            bool isFinal, bool includeOutputs)
        {
            if (!context.TryGetLatest(_left, out var leftBar) || !context.TryGetLatest(_right, out var rightBar))
            {
                return new MultiSeriesIndicatorStateResult(false, 0d, null);
            }

            var value = leftBar.Close + rightBar.Close;
            IReadOnlyDictionary<string, double>? outputs = null;
            if (includeOutputs)
            {
                outputs = new Dictionary<string, double>
                {
                    ["Sum"] = value
                };
            }

            return new MultiSeriesIndicatorStateResult(true, value, outputs);
        }
    }
}
