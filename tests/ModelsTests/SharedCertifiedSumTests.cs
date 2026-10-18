using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedCertifiedSumTests
{
    [Theory]
    [InlineData(0)] [InlineData(19)] [InlineData(20)] [InlineData(63)] [InlineData(4096)]
    public async Task CertificateFailureRebuildsRetainedInputsBeforeEviction(int failureIndex)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (double wide in new[] { .1, double.Epsilon, Math.ScaleB(1, 501) })
            {
                var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 1, -1,
                    i % 9 == 0 ? -0d : (i % 31 - 15) / 8d, 1)).ToArray();
                bars[failureIndex] = new Bar(default, 0, 1, -1, wide, 1);
                var sum = new RollingPriceSum(20);
                using var expected = await Builder(bars, sum).ConfigureBehavior(_ => { }).BuildAsync();
                foreach (int workers in new[] { 1, 8 })
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    using var actual = await Builder(bars, sum).ConfigureHistory(history).BuildAsync();
                    Assert.Equal(expected[sum].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual[sum].ToArray().Select(BitConverter.DoubleToInt64Bits));
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task OverflowInputFailureAndRecoveryKeepTheOrdinaryContract()
    {
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 1, -1, i % 13, 1)).ToArray();
        var sum = new RollingPriceSum(20); var builder = Builder(bars, sum);
        using var saved = await builder.BuildAsync(); var original = bars.ToArray();
        bars[4000] = bars[4001] = new Bar(default, 0, 1, -1, double.MaxValue, 1);
        foreach (bool invalid in new[] { false, true })
        {
            if (invalid) bars[^1] = new Bar(default, 0, 1, -1, 0, double.NaN);
            var expected = await Record.ExceptionAsync(() => Builder(bars, sum).ConfigureBehavior(_ => { }).BuildAsync());
            var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType()); Assert.Equal(expected.Message, actual.Message);
            Assert.Null(builder.LastExecution);
        }
        original.CopyTo(bars, 0); using var recovered = await builder.BuildAsync();
        Assert.Equal(saved[sum].ToArray(), recovered[sum].ToArray());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
    }
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
}
