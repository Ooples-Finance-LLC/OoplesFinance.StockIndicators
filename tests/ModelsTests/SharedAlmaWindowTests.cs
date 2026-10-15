using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedAlmaWindowTests
{
    [Theory]
    [InlineData(6d)] [InlineData(500d)]
    public void CompactProductsPreserveWideWindowsAndReset(double sigma)
    {
        double[] values = [double.MaxValue, -double.MaxValue, double.Epsilon,
            -double.Epsilon, 0d, -0d, Math.BitIncrement(1d), -.125];
        var bars = Enumerable.Range(0, 63).Select(i =>
            new Bar(default, 0, 0, 0, values[i % values.Length], 1)).ToArray();
        foreach (bool fullWindow in new[] { false, true })
        {
            var indicator = new ArnaudLegouxWindow(20, .85, sigma, fullWindow);
            var expected = indicator.Reference(bars);
            var state = (IMultiOutputState)indicator.CreateState();
            var output = new double[2];
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (int row = 0; row < bars.Length; row++)
                {
                    state.Update(in bars[row], output);
                    for (int slot = 0; slot < 2; slot++)
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][row]),
                            BitConverter.DoubleToInt64Bits(output[slot]));
                }
            }
        }
    }

    [Theory]
    [InlineData(511, 20)] [InlineData(512, 20)] [InlineData(513, 20)]
    [InlineData(8193, 20)] [InlineData(513, 1)] [InlineData(513, 513)]
    [InlineData(17, 4097)] [InlineData(17, int.MaxValue)]
    public async Task BoundedWorkersPreservePresenceHistoryAndExtremeSeeds(int count, int period)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, 0,
                (i * 13 % 101 - 50) / 8d, 1)).ToArray();
            bars[count / 2] = new Bar(default, 0, 1, 0, double.MaxValue, 1);
            bars[count / 2 + 1] = new Bar(default, 0, 1, 0, -double.MaxValue, 1);
            bars[^1] = new Bar(default, 0, 1, 0, double.Epsilon, 1);
            foreach (bool fullWindow in new[] { false, true })
            {
                var indicator = new ArnaudLegouxWindow(period, fullWindowOnly: fullWindow);
                using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureBehavior(_ => { }).BuildAsync();
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    var input = bars.ToArray();
                    using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                        .ConfigureIndicators(indicator).ConfigureHistory(history).BuildAsync();
                    Array.Clear(input);
                    actual.Dispose();
                    foreach (var output in indicator.Outputs)
                        Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                            actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                    Assert.Equal(bars[^1], actual.Latest.Bar);
                    if (history == IndicatorHistoryMode.Full)
                    {
                        int row = 0;
                        await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
                        Assert.Equal(count, row);
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task BoundedWorkersKeepInputValidationAndCancellationContracts()
    {
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 1, 0, i / 8d, 1)).ToArray();
        bars[512] = new Bar(default, 0, 1, 0, 1, double.NaN);
        var indicator = new ArnaudLegouxWindow(20);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
        var expected = await Record.ExceptionAsync(() => builder.ConfigureBehavior(_ => { }).BuildAsync());
        var actual = await Record.ExceptionAsync(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator).BuildAsync());
        Assert.NotNull(expected); Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync(cancellation.Token));
    }

    [Fact]
    public void CompactWeightGenerationPreservesPeakTiesAndExponentialUnderflow()
    {
        var bars = new[] { 0d, double.MaxValue, -double.MaxValue, double.Epsilon, 1d }
            .Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        double edge = Math.Sqrt(8 * 745d);
        foreach (double sigma in new[] { Math.BitDecrement(edge), edge, Math.BitIncrement(edge), Math.Sqrt(8 * 746d), double.MaxValue })
        foreach (double offset in new[] { 0d, .5, Math.BitIncrement(.5), Math.BitDecrement(.5), 1d })
        {
            var indicator = new ArnaudLegouxWindow(2, offset, sigma, false);
            var expected = indicator.Reference(bars);
            var state = (IMultiOutputState)indicator.CreateState();
            var output = new double[2];
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], output);
                for (int slot = 0; slot < 2; slot++)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i]), BitConverter.DoubleToInt64Bits(output[slot]));
            }
        }
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void OrdinaryCompleteWindowsAllocateNoPerBarObjects(bool fullWindow)
    {
        var state = (IMultiOutputState)new ArnaudLegouxWindow(20, fullWindowOnly: fullWindow).CreateState();
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0, 1, 0,
            (i * 17 % 97 - 48) / 8d, 1)).ToArray();
        var output = new double[2];
        for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length], output);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length], output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1d, output[1]);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void CachedWeightsPreserveExactReferenceAndReset(bool fullWindow)
    {
        double[] values = [0, -0d, double.Epsilon, -double.Epsilon, double.MaxValue,
            -double.MaxValue, .125, -.75, 1, Math.BitIncrement(1d), 4, -3, 2, 8];
        var bars = values.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        foreach (int period in new[] { 1, 3, 9, int.MaxValue })
        foreach (var (offset, sigma) in new[] { (.85, 6d), (.5, 0d), (-2d, -6d), (double.MaxValue, double.Epsilon) })
        {
            var indicator = new ArnaudLegouxWindow(period, offset, sigma, fullWindow);
            var expected = indicator.Reference(bars);
            var state = (IMultiOutputState)indicator.CreateState();
            var output = new double[2];
            foreach (int count in new[] { bars.Length, 2, bars.Length })
            {
                state.Reset();
                for (int i = 0; i < count; i++)
                {
                    state.Update(in bars[i], output);
                    for (int slot = 0; slot < 2; slot++)
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i]), BitConverter.DoubleToInt64Bits(output[slot]));
                }
            }
        }
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task BuilderModesPreserveBothOutputs(bool fullWindow)
    {
        var bars = Enumerable.Range(0, 43).Select(i => new Bar(default, i, i + 1, i - 1, i / 8d, 1)).ToArray();
        var indicator = new ArnaudLegouxWindow(9, fullWindowOnly: fullWindow);
        var expected = indicator.Reference(bars);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool reference in new[] { false, true })
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
            if (reference) builder.ConfigureBehavior(_ => { });
            using var run = await builder.BuildAsync();
            Assert.Equal(expected[0], run[indicator.Value].ToArray());
            Assert.Equal(expected[1], run[indicator.IsDefined].ToArray());
        }
    }
}
