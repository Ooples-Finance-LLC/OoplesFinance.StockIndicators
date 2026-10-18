using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedPathRatioParallelTests
{
    [Theory]
    [InlineData(1, 0, 113)] [InlineData(3, 0, 113)] [InlineData(20, 0, 113)]
    [InlineData(20, 0, 2)] [InlineData(20, 0, 4097)]
    [InlineData(20, 1, 113)] [InlineData(20, 2, 113)] [InlineData(20, 3, 113)]
    public async Task CertificateFailureReplaysRawWindowAcrossWorkerBoundaries(int period, int scenario, int firstFailure)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            var bars = Enumerable.Range(0, 8193).Select(i =>
            {
                // Fail before maturity, after ring wrap, and around worker seeds;
                // later flat windows ensure an expired extreme leaves no residue.
                double close = i % 97 < 30 ? -0d : (i * 13 % 101 - 50) / 8d;
                if (i >= firstFailure && (i == firstFailure || i is 1023 or 1024 or 4097))
                    close = scenario switch { 0 => 0.1, 1 => double.Epsilon,
                        2 => double.MaxValue, _ => -double.MaxValue };
                if (scenario == 3 && i == 1025) close = double.MaxValue;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 1, 0, close, 1);
            }).ToArray();
            foreach (var convention in Enum.GetValues<PathRatioConvention>())
            {
                var indicator = new WindowPathRatio(period, convention);
                StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu);
                using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
                foreach (int workers in new[] { 1, 8 })
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    using var actual = await Builder().ConfigureHistory(history).BuildAsync();
                    foreach (var output in indicator.Outputs)
                        Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                            actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(20)] [InlineData(4095)] [InlineData(4096)] [InlineData(int.MaxValue)]
    public async Task ReplayPreservesEveryValuePresenceAndOwnedBar(int period)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            var bars = Enumerable.Range(0, 8193).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 1, 0,
                i % 97 < 30 ? 1 : (i * 13 % 101 - 50) / 8d, 1)).ToArray();
            foreach (var convention in Enum.GetValues<PathRatioConvention>())
            {
                var indicator = new WindowPathRatio(period, convention);
                StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu);
                using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
                foreach (int workers in new[] { 1, 8 })
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    using var actual = await Builder().ConfigureHistory(history).BuildAsync();
                    foreach (var output in indicator.Outputs)
                        Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                            actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    var last = bars[^1];
                    actual.Dispose();
                    Assert.Equal(last, actual.Latest.Bar);
                    Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task IgnoredFieldsAreValidatedAndCancellationLeavesNoSuccessfulExecution()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Repeat(new Bar(default, 1, 1, 1, 1, 1), 8193).ToArray();
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(new WindowPathRatio(20)).ConfigureHistory(IndicatorHistoryMode.LatestOnly);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            Assert.Null(builder.LastExecution);
            bars[4097] = new Bar(default, 1, double.NaN, 1, 1, 1);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            Assert.Null(builder.LastExecution);
            bars[4097] = bars[0];
            using var recovered = await builder.BuildAsync();
            Assert.Equal(bars.Length, recovered.BarCount);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
