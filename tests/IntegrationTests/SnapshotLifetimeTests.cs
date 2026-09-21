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

        double[] expected;
        IndicatorSnapshot snapshot;

        using (var runtime = builder.Build())
        {
            runtime.Start();
            runtime.Subscribe(handle);
            snapshot = runtime.Latest ?? throw new InvalidOperationException("no snapshot published");
            snapshot.TryGetSeries(handle, out var live).Should().BeTrue();
            expected = live.ToArray();
        }

        // The runtime is gone and its buffers are back in the pool. Take them and write over them, which is
        // exactly what the next caller of ArrayPool does.
        var stolen = new List<double[]>();
        for (var i = 0; i < 8; i++)
        {
            var array = ArrayPool<double>.Shared.Rent(SampleSize);
            array.AsSpan().Fill(double.NaN);
            stolen.Add(array);
        }

        try
        {
            snapshot.TryGetSeries(handle, out var afterwards).Should().BeTrue();
            afterwards.ToArray().Should().Equal(expected,
                "a snapshot reports what it computed, not what later rented its memory");
        }
        finally
        {
            foreach (var array in stolen)
            {
                ArrayPool<double>.Shared.Return(array);
            }
        }
    }
}
