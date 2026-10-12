using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedAlmaWindowTests
{
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
