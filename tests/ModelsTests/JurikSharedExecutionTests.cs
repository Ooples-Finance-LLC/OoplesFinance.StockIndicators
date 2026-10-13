using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class JurikSharedExecutionTests
{
    private static Bar[] BarsOf(IEnumerable<double> prices) => prices.Select((p, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i + 1, i - 1, p, i + 2)).ToArray();

    [Theory]
    [InlineData(1, 1, 0)] [InlineData(2, 3, -100)] [InlineData(20, 10, 0)]
    [InlineData(7, 1, 100)] [InlineData(3, 20, double.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue, 0)]
    public async Task BoundedHistoriesStartupAndAllOwnedValuesMatchSnapshot(int period, int volatility, double phase)
    {
        foreach (int count in new[] { 0, 1, 2, 19, 20, 21, 65, 129 })
            await Check(BarsOf(Enumerable.Range(0, count).Select(i => i % 53 < 12 ? 100 : 100 + (i * 13 % 101) / 8d)), new(period, phase, volatility));
    }

    [Fact]
    public async Task WideSubnormalAndSignedZeroStagesRetainOwnedOutputs()
    {
        foreach (double scale in new[] { double.MaxValue / 32, double.Epsilon, 1e-200, -0d })
            await Check(BarsOf(Enumerable.Range(0, 65).Select(i => (i % 7 - 3) * scale)), new(5, 0, 4));
        var random = new Random(72191);
        await Check(BarsOf(Enumerable.Range(0, 129).Select(i => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1000)))), new(7, -100, 3));
    }

    [Fact]
    public async Task RawValidationPrecedesPreciseOverflowRejection()
    {
        var bars = BarsOf(Enumerable.Range(0, 33).Select(i => (i % 3 == 0 ? -1 : 1) * double.MaxValue));
        var indicator = new JurikAdaptive(2, 100, 2);
        var kernel = new JurikCpuKernel(2, 100, 2, bars.Length, rejectOverflow: false);
        var scratch = new double[1]; int first = -1; double failure = 0;
        for (int i = 0; i < bars.Length; i++)
        {
            kernel.Next(bars[i], scratch, true);
            if (!double.IsFinite(scratch[0])) { first = i; failure = scratch[0]; break; }
        }
        Assert.InRange(first, 1, bars.Length - 2);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(indicator).ConfigureHistory(mode);
            input[^1] = new(default, 1, 1, 1, 1, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            input[^1] = bars[^1];
            var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(first, error.BarIndex); Assert.Equal(0, error.OutputSlot);
            Assert.Equal(BitConverter.DoubleToInt64Bits(failure), BitConverter.DoubleToInt64Bits(error.Value));
        }
    }

    [Fact]
    public async Task MixedDuplicateRootsAndCancellationRecover()
    {
        var bars = BarsOf(Enumerable.Range(0, 65).Select(i => 100 + i % 7d));
        var first = new JurikAdaptive(3, -100, 2); var second = new JurikAdaptive(7, 100, 4);
        var a = JurikAdaptiveSnapshot.Calculate(bars, 3, -100, 2).ToArray();
        var b = JurikAdaptiveSnapshot.Calculate(bars, 7, 100, 4).ToArray();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(first, second, first).ConfigureHistory(history);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            AssertBits(a, run[first.Outputs[0]].ToArray()); AssertBits(b, run[second.Outputs[0]].ToArray());
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
        }
        await Assert.ThrowsAsync<NotSupportedException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(first).ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    private static async Task Check(Bar[] bars, JurikAdaptive indicator)
    {
        Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
        var expected = JurikAdaptiveSnapshot.Calculate(bars, indicator.Period, indicator.Phase, indicator.VolatilityPeriod).ToArray();
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose(); AssertBits(expected, run[indicator.Outputs[0]].ToArray());
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }
    private static void AssertBits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
