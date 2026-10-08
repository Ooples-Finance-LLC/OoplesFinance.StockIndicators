using System.Buffers;
using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Tests.Unit.IntegrationTests;

/// <summary>
/// A snapshot outlives the runtime that produced it - callers hold one, pass it around, read it later. The
/// series it hands back are computed into pooled buffers, and disposing the runtime gives those buffers back
/// to the pool, where the next rental overwrites them. A snapshot that still points at them then reports
/// whatever the next caller happened to write, which is worse than throwing.
/// </summary>
public sealed class SnapshotLifetimeTests : GlobalTestData
{
    private const int SampleSize = 200;

    [Fact]
    public void ASnapshotKeepsItsValuesAfterTheRuntimeIsDisposed()
    {
        var builder = new StockIndicatorBuilder(
            IndicatorDataSource.FromBatch(new StockData(StockTestData.Take(SampleSize))));

        SeriesHandle handle = default;
        builder.ConfigureIndicators(catalog => handle = catalog.Sma(20));

        var pool = new ReusingPool();
        double[] expected;
        IndicatorSnapshot snapshot;

        using (var runtime = builder.Build(pool))
        {
            runtime.Start();
            runtime.Subscribe(handle);
            snapshot = runtime.Latest ?? throw new InvalidOperationException("no snapshot published");
            snapshot.TryGetSeries(handle, out var live).Should().BeTrue();
            expected = live.ToArray();
        }

        // The runtime is gone and its buffers are back in the pool. Take them and write over them, which is
        // exactly what the next caller of ArrayPool does.
        var reused = pool.Rent(SampleSize);
        pool.Returned.Should().Contain(array => ReferenceEquals(array, reused));
        reused.AsSpan().Fill(double.NaN);
        snapshot.TryGetSeries(handle, out var afterwards).Should().BeTrue();
        afterwards.ToArray().Should().Equal(expected,
            "a snapshot must not observe the reused runtime buffer");
        pool.Return(reused);
    }

    [Fact]
    public void DeferredSeriesCanFirstBeReadAfterRuntimeDisposal()
    {
        var builder = new StockIndicatorBuilder(
            IndicatorDataSource.FromBatch(new StockData(StockTestData.Take(SampleSize))));
        SeriesHandle active = default, deferred = default;
        builder.ConfigureIndicators(catalog => { active = catalog.Sma(20); deferred = catalog.Ema(15); });
        builder.ConfigureSignals(signals => signals.When(active).CrossesAbove(0).Emit("active"));
        double[] expected;
        using (var control = builder.Build())
        {
            control.Subscribe(deferred);
            control.Start();
            expected = control.Latest!.GetSeries(deferred).ToArray();
        }
        IndicatorSnapshot snapshot;
        using (var runtime = builder.Build())
        {
            runtime.Start();
            snapshot = runtime.Latest!;
        }
        snapshot.TryGetSeries(deferred, out var actual).Should().BeTrue();
        actual.ToArray().Should().Equal(expected);
        snapshot.GetSeries(deferred).ToArray().Should().Equal(expected);
    }

    [Fact]
    public void DeferredNamedSourceSurvivesRuntimeDisposal()
    {
        var prices = new[] { 11d, 23, 17, 29 };
        var dates = Enumerable.Range(0, prices.Length).Select(i => DateTime.UnixEpoch.AddDays(i));
        var market = new StockData(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length), dates);
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(new StockData(StockTestData.Take(prices.Length))));
        builder.AddDataSource("market", IndicatorDataSource.FromBatch(market));
        SeriesHandle active = default, deferred = default;
        builder.ConfigureIndicators(catalog => { active = catalog.Sma(2); deferred = catalog.Price("market"); });
        builder.ConfigureSignals(signals => signals.When(active).CrossesAbove(0).Emit("active"));
        IndicatorSnapshot snapshot;
        using (var runtime = builder.Build())
        {
            runtime.Start();
            snapshot = runtime.Latest!;
        }
        snapshot.TryGetSeries(deferred, out var actual).Should().BeTrue();
        actual.ToArray().Should().Equal(prices, "deferred evaluation must retain the named source map");
        snapshot.GetSeries(deferred).ToArray().Should().Equal(prices);
    }

    private sealed class ReusingPool : ArrayPool<double>
    {
        private readonly Stack<double[]> _available = new();
        internal List<double[]> Returned { get; } = new();
        public override double[] Rent(int minimumLength)
            => _available.Count > 0 ? _available.Pop() : new double[minimumLength];
        public override void Return(double[] array, bool clearArray = false)
        {
            Returned.Add(array);
            _available.Push(array);
        }
    }
}
