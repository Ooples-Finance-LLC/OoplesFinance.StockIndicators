using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AlmaGridBatchTests
{
    private static Bar[] Fixture(int count) => Enumerable.Range(0, count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), i + 1, i + 2, -i - 2,
            (i * 37 % 257 - 128) / 8d, i + 3)).ToArray();

    private static Bar WithClose(Bar bar, double close) =>
        new(bar.Time, bar.Open, bar.High, bar.Low, close, bar.Volume);

    [Theory]
    [InlineData(false, 1)] [InlineData(false, 20)] [InlineData(false, 64)]
    [InlineData(true, 20)] [InlineData(true, 64)]
    public async Task AsymmetricWeightsStartupAndBlockBoundariesMatchIndependentReference(bool full, int period)
    {
        foreach (int count in new[] { Math.Max(1, period - 1), period, period + 127,
            period + 128, period + 129, period + 256 + 7 })
        {
            var bars = Fixture(count);
            // The isolated impulse makes oldest/newest coefficient reversal
            // observable even when the surrounding fixture happens to repeat.
            if (count > period) bars[period] = WithClose(bars[period], 127.875);
            var indicator = new ArnaudLegouxWindow(period, .85, 6, full);
            await Check(bars, indicator, indicator.Reference(bars));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ParallelHalosAndPartialFinalBlocksRetainBothOutputs(bool full)
    {
        var bars = Fixture(8193);
        var indicator = new ArnaudLegouxWindow(20, .85, 6, full);
        await Check(bars, indicator, Streaming(bars, indicator));
    }

    [Theory]
    [InlineData(.85, 6, 20)] [InlineData(.85, 500, 20)]
    [InlineData(-12, 6, 20)] [InlineData(.85, 0, 20)] [InlineData(.85, 6, 65)]
    public async Task CoefficientMissesAndUnsafePriceBlocksReplayOriginal(double offset, double sigma, int period)
    {
        var bars = Fixture(513);
        foreach (int index in new[] { period + 127, period + 128, 400 })
            bars[index] = WithClose(bars[index], index == 400 ? double.Epsilon : double.MaxValue);
        bars[401] = WithClose(bars[401], -double.MaxValue);
        foreach (bool full in new[] { false, true })
        {
            var indicator = new ArnaudLegouxWindow(period, offset, sigma, full);
            await Check(bars, indicator, Streaming(bars, indicator));
        }
    }

    [Fact]
    public async Task SubnormalCancellationAndSignedZeroRoundOnce()
    {
        foreach (double sign in new[] { 1d, -1d })
        {
            var bars = Fixture(385);
            for (int i = 0; i < bars.Length; i++)
                bars[i] = WithClose(bars[i], sign * ((i % 7) switch
                { 0 => double.Epsilon, 1 => -double.Epsilon, 2 => 2 * double.Epsilon, _ => 0d }));
            var indicator = new ArnaudLegouxWindow(20, .85, 6, false);
            await Check(bars, indicator, indicator.Reference(bars));
        }
        var zeros = Fixture(193).Select(b => WithClose(b, -0d)).ToArray();
        var zeroIndicator = new ArnaudLegouxWindow(20);
        await Check(zeros, zeroIndicator, zeroIndicator.Reference(zeros));
    }

    [Fact]
    public async Task PendingRowsRetainAllFieldValidationAndPreCancellationRecovery()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            // A small default-worker region would bypass buffering entirely.
            CpuParallelSettings.MaxDegreeOfParallelism = 1;
            var bars = Fixture(513);
            var indicator = new ArnaudLegouxWindow(20, fullWindowOnly: false);
            var builder = Build(bars, indicator);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            var valid = bars[99];
            bars[99] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[99] = valid;
            await Check(bars, indicator, Streaming(bars, indicator));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static double[][] Streaming(Bar[] bars, ArnaudLegouxWindow indicator)
    {
        var values = new[] { new double[bars.Length], new double[bars.Length] };
        var state = (IMultiOutputState)indicator.CreateState();
        var scratch = new double[2];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(in bars[i], scratch);
            for (int slot = 0; slot < 2; slot++) values[slot][i] = scratch[slot];
        }
        return values;
    }

    private static StockIndicatorBuilder Build(Bar[] bars, ArnaudLegouxWindow indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    private static async Task Check(Bar[] bars, ArnaudLegouxWindow indicator, double[][] expected)
    {
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
                for (int slot = 0; slot < 2; slot++)
                    Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                        actual[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], actual.Latest.Bar);
                if (history != IndicatorHistoryMode.Full) continue;
                int row = 0;
                await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
