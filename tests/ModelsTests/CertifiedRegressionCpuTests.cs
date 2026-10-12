using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CertifiedRegressionCpuTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 1, -1, i % 11 == 0 ? -0d : (i % 31 - 15) / 8d, 1)).ToArray();
    private static StockIndicatorBuilder Build(Bar[] bars, params IIndicator[] indicators) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).ConfigureExecution(IndicatorExecutionBackend.Cpu);
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));

    [Theory]
    [InlineData(1, false)] [InlineData(2, true)] [InlineData(20, false)] [InlineData(65, true)]
    [InlineData(4096, true)] [InlineData(4097, true)] [InlineData(32767, false)]
    [InlineData(32768, false)] [InlineData(int.MaxValue, false)]
    public async Task CertifiedAndFallbackPathsMatchExistingExactState(int period, bool failLater)
    {
        var bars = Data(1027);
        if (failLater) bars[513] = new Bar(default, 0, 1, -1, .1, 1);
        if (period == 1) bars[0] = new Bar(default, 0, 1, -1, -double.Epsilon, 1);
        foreach (var output in Enum.GetValues<WindowRegressionOutput>())
        {
            var indicator = new WindowLinearRegression(period, output);
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var actual = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
                actual.Dispose();
                Bits(expected[indicator].ToArray(), actual[indicator].ToArray());
                Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                Assert.Equal(bars[^1], actual.Latest.Bar);
            }
        }
    }

    [Fact]
    public async Task MixedParallelRootsReplayOnlyRequiredHistoryAndRetainOwnedBars()
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 4;
            var bars = Data(8193);
            foreach (int index in new[] { 2040, 4096, 6144 }) bars[index] = new Bar(default, 0, 1, -1, .1, 1);
            IIndicator[] indicators = [new WindowLinearRegression(20), new WindowLinearRegression(3, WindowRegressionOutput.Slope), new RollingPriceSum(7)];
            using var expected = await Build(bars, indicators).ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await Build(bars, indicators).BuildAsync();
            var original = bars.ToArray(); Array.Clear(bars); actual.Dispose();
            foreach (var indicator in indicators) Bits(expected[indicator].ToArray(), actual[indicator].ToArray());
            int row = 0; await foreach (var snapshot in actual) Assert.Equal(original[row++], snapshot.Bar);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task UnpublishedOverflowAndRawValidationOrderRemainExact()
    {
        var bars = new[] { -double.MaxValue, double.MaxValue }.Select(v => new Bar(default, 0, 1, -1, v, 1)).ToArray();
        foreach (var output in new[] { WindowRegressionOutput.Endpoint, WindowRegressionOutput.Intercept, WindowRegressionOutput.Angle })
        {
            var indicator = new WindowLinearRegression(2, output);
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await Build(bars, indicator).BuildAsync();
            Bits(expected[indicator].ToArray(), actual[indicator].ToArray());
        }
        var forecast = new WindowLinearRegression(2, WindowRegressionOutput.Forecast);
        await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(bars, forecast).BuildAsync());
        var invalid = bars.Concat(new[] { new Bar(default, 0, 1, -1, 1, double.NaN) }).ToArray();
        var reference = await Record.ExceptionAsync(() => Build(invalid, forecast).ConfigureBehavior(_ => { }).BuildAsync());
        var failure = await Record.ExceptionAsync(() => Build(invalid, forecast).BuildAsync());
        Assert.NotNull(reference); Assert.NotNull(failure);
        Assert.Equal(reference.GetType(), failure.GetType()); Assert.Equal(reference.Message, failure.Message);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(Data(8193), forecast).BuildAsync(cancel.Token));
    }

    [Fact]
    public void WarmedCertifiedStateAllocatesNothingAndResetAfterFallbackRecovers()
    {
        var indicator = new WindowLinearRegression(20, WindowRegressionOutput.Forecast);
        var state = new ValuesBarExecution.RegressionValueState(indicator, 1000);
        var bars = Data(1000);
        double Replay() { double value = 0; foreach (var bar in bars) value = state.Update(bar); return value; }
        for (int i = 0; i < 10; i++) { state.Reset(); Replay(); }
        state.Reset(); long before = GC.GetAllocatedBytesForCurrentThread();
        double expected = Replay(); long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        state.Update(new Bar(default, 0, 1, -1, .1, 1));
        state.Reset(); Assert.Equal(expected, Replay());
    }
}
