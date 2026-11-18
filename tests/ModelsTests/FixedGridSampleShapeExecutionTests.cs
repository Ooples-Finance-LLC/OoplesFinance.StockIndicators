using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class FixedGridSampleShapeExecutionTests
{
    [Theory]
    [InlineData(false, 20)] [InlineData(true, 20)]
    [InlineData(false, 64)] [InlineData(true, 64)]
    [InlineData(false, 65)] [InlineData(true, 65)]
    public async Task SignedMomentsFlatWindowsAndWorkerHalosMatchOriginalAndCenteredReference(bool kurtosis, int period)
    {
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            0, 1, -1, i % 197 < 80 ? 128 : (i % 257 - 128) / 8d, 1)).ToArray();
        var indicator = new WindowSampleShape(period, kurtosis);
        using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
        foreach (int row in new[] { 0, 1, 2, 3, period - 1, 1023, 1024, 4095, 4096, bars.Length - 1 })
            CheckCenteredWindow(bars, row, indicator, expected[indicator.Value].ToArray()[row],
                expected[indicator.IsDefined].ToArray()[row]);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                using var actual = await Build(input, indicator).ConfigureHistory(history).BuildAsync();
                Array.Clear(input);
                foreach (var output in indicator.Outputs)
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], actual.Latest.Bar);
                if (history != IndicatorHistoryMode.Full) continue;
                int row = 0;
                await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false, 4096)] [InlineData(true, 4096)]
    [InlineData(false, 4097)] [InlineData(true, 4097)]
    public async Task InclusiveGridBoundAndScaleChangesPreserveExactFallback(bool kurtosis, int maximum)
    {
        foreach (int exponent in new[] { -900, 0, 900 })
        {
            // The odd unit fixes the grid; 4096 is accepted, 4097 rejected.
            var bars = Enumerable.Range(0, 8192).Select(i => new Bar(default, 0, 1, -1,
                Math.ScaleB(i % 3 == 0 ? 1 : i % 2 == 0 ? maximum : -maximum, exponent), 1)).ToArray();
            var indicator = new WindowSampleShape(64, kurtosis);
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await Build(bars, indicator).BuildAsync();
            foreach (var output in indicator.Outputs)
                Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                    actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WideSubnormalFallbackAndInvalidUnusedFieldsRetainOriginalBehavior(bool kurtosis)
    {
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 1, -1,
            i % 193 == 0 ? double.Epsilon : i % 2 == 0 ? double.MaxValue : -double.MaxValue, 1)).ToArray();
        var indicator = new WindowSampleShape(4, kurtosis);
        using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
        using var actual = await Build(bars, indicator).BuildAsync();
        foreach (var output in indicator.Outputs)
            Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
        bars[4096] = new Bar(default, 0, 1, -1, bars[4096].Close, double.NaN);
        var originalError = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        var gridError = await Record.ExceptionAsync(() => Build(bars, indicator).BuildAsync());
        Assert.NotNull(originalError);
        Assert.Equal(originalError.GetType(), gridError?.GetType());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, indicator).BuildAsync(cancellation.Token));
    }

    private static StockIndicatorBuilder Build(Bar[] bars, WindowSampleShape indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    private static void CheckCenteredWindow(Bar[] bars, int row, WindowSampleShape indicator, double value, double present)
    {
        var window = bars.Skip(Math.Max(0, row - indicator.Period + 1))
            .Take(Math.Min(row + 1, indicator.Period)).ToArray();
        double? expected = SampleShapeReference.Calculate(window, indicator.Period, indicator.Kurtosis)[^1];
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected ?? 0), BitConverter.DoubleToInt64Bits(value));
        Assert.Equal(expected.HasValue ? 1d : 0, present);
    }
}
