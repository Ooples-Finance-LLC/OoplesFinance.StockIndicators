using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedWeightedWindowTests : IDisposable
{
    private readonly int _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
    public SharedWeightedWindowTests() => CpuParallelSettings.MaxDegreeOfParallelism = 8;
    public void Dispose() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;

    [Fact]
    public void CachedKernelResetMatchesIndependentPrefixAndZeroMassReferences()
    {
        var bars = Fixture(41);
        foreach (double[] weights in new double[][] { [1, -1], [0, -0d, 1, -1, double.Epsilon, .75, double.MaxValue], [.25, 3, -.5] })
        {
            var expected = NormalizedKernelReference.Values(bars, weights.Length,
                lag => ReferenceFraction.FromDouble(weights[lag]));
            var state = new NormalizedKernelState(weights.Length, lag => weights[lag]);
            for (int replay = 0; replay < 3; replay++)
            {
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(state.Update(in bars[i])));
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task ExactWindowsPreserveStartupAndWorkerBoundaries(int operation)
    {
        foreach (int count in new[] { 8191, 8192, 8193 })
            await Compare(Fixture(count), [Create(operation, 7)]);
        foreach (int count in new[] { 511, 512, 513 })
            await Compare(Fixture(count), [Create(operation, 20)]);
    }

    [Fact]
    public async Task MixedPeriodsAndExtremeValuesPreserveExactReplay()
    {
        await Compare(Fixture(513), [Create(1, 20), new SineWeightedAverage(3), new RollingPriceSum(7)]);
        await Compare(Fixture(8193), [new FixedPeriodWma(11), new NormalizedConvolution([1, -1, 2, -2]),
            new GaussianWeightedAverage(7), new SineWeightedAverage(3), new MarubozuCandle(10)]);
        foreach (int count in new[] { 0, 1, 41 })
        foreach (int operation in Enumerable.Range(0, 4))
            await Compare(Fixture(count), [Create(operation, operation == 2 ? 2 : 1)]);
        foreach (int period in new[] { 4096, 4097, int.MaxValue })
            await Compare(Fixture(period == int.MaxValue ? 41 : 8193), [new FixedPeriodWma(period)]);
        await Compare(Fixture(41), [new GaussianWeightedAverage(int.MaxValue, double.Epsilon),
            new SineWeightedAverage(int.MaxValue)]);
    }

    [Fact]
    public async Task InputPrecedenceCancellationAndSourcedFallbackRemainIntact()
    {
        var shortBars = Fixture(513);
        shortBars[0] = new Bar(default, 0, 1, 0, -double.MaxValue, 1);
        shortBars[1] = new Bar(default, 0, 1, 0, double.MaxValue, 1);
        shortBars[63] = new Bar(default, 0, 1, 0, 1, double.NaN);
        await Compare(shortBars, [new NormalizedConvolution(new[] { 2d, -2d }.Concat(new double[18]))], expectError: true);
        var bars = Fixture(8193);
        IIndicator[] indicators = [new NormalizedConvolution([2, -2]), new FixedPeriodWma(7)];
        bars[0] = new Bar(default, 0, 1, 0, -double.MaxValue, 1);
        bars[1] = new Bar(default, 0, 1, 0, double.MaxValue, 1);
        await Compare(bars, indicators, expectError: true);
        foreach (int field in Enumerable.Range(0, 5))
        {
            bars[1022] = new Bar(default, field == 0 ? double.NaN : 0, field == 1 ? double.NaN : 1,
                field == 2 ? double.NaN : 0, field == 3 ? double.NaN : 1, field == 4 ? double.NaN : 1);
            await Compare(bars, indicators, expectError: true);
        }
        var builder = Build(Fixture(8193), [new FixedPeriodWma(7)], IndicatorHistoryMode.Full);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        Assert.Null(builder.LastExecution);
        await Compare(Fixture(41), [new FixedPeriodWma(3).Of(new Sma(2))]);
    }

    private static IIndicator Create(int operation, int period) => operation switch
    {
        0 => new FixedPeriodWma(period),
        1 => new NormalizedConvolution(Enumerable.Range(1, period).Select(i => (double)i)),
        2 => new GaussianWeightedAverage(period),
        _ => new SineWeightedAverage(period)
    };
    private static Bar[] Fixture(int count)
    {
        double[] values = [-0d, 0d, double.Epsilon, -double.Epsilon, 1, -1, double.MaxValue / 16, -double.MaxValue / 16, .125];
        return Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, 0, values[i % values.Length], 1)).ToArray();
    }
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator[] indicators, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).ConfigureHistory(history);
    private static async Task Compare(Bar[] bars, IIndicator[] indicators, bool expectError = false)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            IIndicatorRun? expected = null, actual = null;
            var expectedError = await Record.ExceptionAsync(async () =>
                expected = await Build(bars, indicators, history).ConfigureBehavior(_ => { }).BuildAsync());
            var actualError = await Record.ExceptionAsync(async () => actual = await Build(bars, indicators, history).BuildAsync());
            using (expected) using (actual)
            {
                if (expectError)
                {
                    Assert.NotNull(expectedError); Assert.NotNull(actualError);
                    Assert.Equal(expectedError.GetType(), actualError.GetType());
                    Assert.Equal(expectedError.Message, actualError.Message);
                    continue;
                }
                Assert.Null(expectedError); Assert.Null(actualError);
                foreach (var indicator in indicators)
                    Assert.Equal(expected![indicator].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual![indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                var original = bars.ToArray(); Array.Clear(bars);
                if (history == IndicatorHistoryMode.Full)
                {
                    int index = 0;
                    await foreach (var snapshot in actual!) Assert.Equal(original[index++], snapshot.Bar);
                    Assert.Equal(original.Length, index);
                }
                else if (original.Length > 0) Assert.Equal(original[^1], actual!.Latest.Bar);
                original.CopyTo(bars, 0);
            }
        }
    }
}
