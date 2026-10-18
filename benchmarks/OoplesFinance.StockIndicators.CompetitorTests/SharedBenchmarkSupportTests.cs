using BenchmarkDotNet.Running;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedBenchmarkSupportTests
{
    [Fact]
    public void MappedScalarStartupIsCheckedOnlyForMappingsThatPublishIt()
    {
        Bar[] bars = [new(default, 4, 10, 0, 6, 1), new(default, 4, 10, 0, 6, 1)];
        var expected = new ComparisonSeries(1, [99d, 0d]);
        SharedBenchmarkSupport.VerifyMappedOutputs(bars, () => new RickshawManCandle(10, 5), expected, ["Value"]);
        Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.VerifyMappedOutputs(
            bars, () => new RickshawManCandle(10, 5), expected, ["Value"], includeScalarStartup: true));
    }

    [Fact]
    public void MappedAndNullableChecksRejectWrongPresenceEvenWhenValuesAreZero()
    {
        Bar[] bars = [new(default, 4, 10, 0, 6, 1), new(default, 4, 10, 0, 6, 1)];
        ComparisonSeries Expected(bool present) => new(new Dictionary<string, ComparisonOutput>
        {
            ["Value"] = new(2, [0d, 0d], [present, false])
        });
        SharedBenchmarkSupport.VerifyMappedOutputs(bars, () => new WindowMoneyFlowIndex(2), Expected(false), ["Value"]);
        Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.VerifyMappedOutputs(
            bars, () => new WindowMoneyFlowIndex(2), Expected(true), ["Value"]));
        var indicator = new WindowMoneyFlowIndex(2);
        using var run = SharedBenchmarkSupport.BuildCpu(bars, indicator, IndicatorHistoryMode.Full);
        SharedBenchmarkSupport.VerifyNullableColumn([null, null], run, indicator.Value, indicator.IsDefined, 2);
        Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.VerifyNullableColumn(
            [0d, null], run, indicator.Value, indicator.IsDefined, 2));
    }

    [Theory]
    [InlineData(typeof(SharedAverageOscillatorBenchmarks), 5)]
    [InlineData(typeof(SharedClassicAverageBenchmarks), 3)]
    [InlineData(typeof(SharedClassicStochasticBenchmarks), 5)]
    [InlineData(typeof(SharedRollingStatisticsBenchmarks), 7)]
    [InlineData(typeof(SharedStrengthBenchmarks), 7)]
    [InlineData(typeof(SharedSmoothedStochasticBenchmarks), 3)]
    [InlineData(typeof(SharedTradyStochasticBenchmarks), 8)]
    public void MappedBenchmarksRetainBothBuilderArmsAndEveryParameter(Type type, int operations)
    {
        var cases = BenchmarkConverter.TypeToBenchmarks(type).BenchmarksCases;
        Assert.Equal(operations * 2 * 2, cases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly" },
            cases.Select(c => c.Descriptor.WorkloadMethod.Name).Distinct().Order().ToArray());
        Assert.All(cases, c =>
        {
            Assert.Equal("Setup", c.Descriptor.GlobalSetupMethod?.Name);
            Assert.Equal("Cleanup", c.Descriptor.GlobalCleanupMethod?.Name);
        });
    }

    [Fact]
    public void ExactNullableComparisonPreservesPresenceAndSignedZero()
    {
        SharedBenchmarkSupport.RequireExactNullable([null, -0d, 1d], [null, -0d, 1d]);
        Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.RequireExactNullable([null], [0d]));
        Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.RequireExactNullable([-0d], [0d]));
        Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.RequireExactNullable([1d], [Math.BitIncrement(1d)]));
        Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.RequireExactNullable([], [null]));
    }

    [Fact]
    public void NativeToleranceStillRejectsNonFiniteAndOutOfToleranceValues()
    {
        SharedBenchmarkSupport.RequireNativeTolerance([1d, 100d], [1d + 5e-11, 100d + 5e-9]);
        Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.RequireNativeTolerance([1d], [1d + 2e-10]));
        Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.RequireNativeTolerance([100d], [100d + 2e-8]));
        foreach (double value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.RequireNativeTolerance([value], [value]));
            Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.RequireNativeTolerance([1d], [value]));
        }
        Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.RequireNativeTolerance([], [0d]));
    }

    [Theory]
    [InlineData(IndicatorHistoryMode.Full)]
    [InlineData(IndicatorHistoryMode.LatestOnly)]
    public void CpuBuilderReturnsOwnedOutputAfterDisposal(IndicatorHistoryMode history)
    {
        Bar[] bars = [new(default, 4, 10, 0, 6, 1), new(default, 4, 10, 0, 6, 1)];
        var owner = new RickshawManCandle(10, 5);
        var run = SharedBenchmarkSupport.BuildCpu(bars, owner, history);
        run.Dispose();
        Assert.Equal(new[] { 0d, 0d }, run[owner].ToArray());
        Assert.Equal(0d, run.Latest[owner]);
    }

    [Fact]
    public void RequiredGpuCheckRejectsCpuExecution()
    {
        var builder = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(new[] { new Bar(default, 4, 10, 0, 6, 1) }))
            .ConfigureIndicators(new RickshawManCandle(10, 5))
            .ConfigureExecution(IndicatorExecutionBackend.Cpu);
        var error = Assert.Throws<InvalidOperationException>(() => SharedBenchmarkSupport.BuildChecked(builder, true, "GPU required"));
        Assert.Equal("GPU required", error.Message);
    }

    [Theory]
    [InlineData(typeof(SharedContainmentCandleBenchmarks), 4)]
    [InlineData(typeof(SharedContextReversalBenchmarks), 4)]
    [InlineData(typeof(SharedKickingCandleBenchmarks), 2)]
    [InlineData(typeof(SharedMatchedLinesBenchmarks), 3)]
    [InlineData(typeof(SharedNeckCandleBenchmarks), 3)]
    [InlineData(typeof(SharedRangeDojiBenchmarks), 5)]
    [InlineData(typeof(SharedSequenceCandleBenchmarks), 4)]
    [InlineData(typeof(SharedTripleBodyBenchmarks), 4)]
    public void InheritedBenchmarksRetainAllArmsParametersAndLifecycle(Type type, int operations)
    {
        var cases = BenchmarkConverter.TypeToBenchmarks(type).BenchmarksCases;
        Assert.Equal(operations * 2 * 4, cases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            cases.Select(c => c.Descriptor.WorkloadMethod.Name).Distinct().Order().ToArray());
        Assert.All(cases, c =>
        {
            Assert.Equal("Setup", c.Descriptor.GlobalSetupMethod?.Name);
            Assert.Equal("Cleanup", c.Descriptor.GlobalCleanupMethod?.Name);
        });
    }
}
