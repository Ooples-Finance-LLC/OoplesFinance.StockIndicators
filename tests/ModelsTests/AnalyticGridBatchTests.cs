using AiDotNet.Tensors.Engines.Simd;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AnalyticGridBatchTests
{
    private static IndicatorBase Create(bool gaussian, int period, double sigma = 1) =>
        gaussian ? new GaussianWeightedAverage(period, sigma) : new SineWeightedAverage(period);

    private static Bar[] Fixture(int count) => Enumerable.Range(0, count).Select(i =>
        new Bar(default, 1000 + i, 2000 + i, -2000 - i, (i * 37 % 257 - 128) / 8d, i + 1)).ToArray();

    [Theory]
    [InlineData(false, 1)] [InlineData(false, 20)] [InlineData(false, 64)] [InlineData(false, 65)]
    [InlineData(true, 2)] [InlineData(true, 20)] [InlineData(true, 64)] [InlineData(true, 65)]
    public async Task RequestedPrefixOrientationPartialBlocksAndWorkerHalosMatchExactState(bool gaussian, int period)
    {
        foreach (int count in new[] { 1, period - 1, period, 127, 128, 129, 2 * (128 + period - 1) + 7, 513 })
            await Check(Fixture(count), Create(gaussian, period));
        if (period <= 64) await Check(Fixture(8193), Create(gaussian, period));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task GridMissesAndExtremeFinitePrefixesReplayOriginalState(bool gaussian)
    {
        var bars = Fixture(1001);
        for (int i = 0; i < bars.Length; i++)
        {
            double close = (i % 257) switch
            {
                0 => double.MaxValue, 1 => -double.MaxValue,
                129 => double.Epsilon, 130 => -double.Epsilon,
                _ => bars[i].Close
            };
            bars[i] = new(default, bars[i].Open, bars[i].High, bars[i].Low, close, bars[i].Volume);
        }
        await Check(bars, Create(gaussian, 20));
    }

    [Fact]
    public async Task CoefficientGridMissAndZeroAvailableMassRetainRequestedWeights()
    {
        await Check(Fixture(513), new GaussianWeightedAverage(20, .025));
        await Check(Fixture(513), new GaussianWeightedAverage(20, -2));
        var bars = Fixture(129);
        var indicator = new GaussianWeightedAverage(20, 1e-200);
        using var run = await Build(bars, indicator).BuildAsync();
        Assert.All(run[indicator].ToArray().Take(10), value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
        Assert.Equal(bars[0].Close, run[indicator].ToArray()[10]);
        await Check(bars, indicator);
    }

    [Fact]
    public async Task ExactSubnormalPublisherDoesNotRoundTheRatioBeforeScaling()
    {
        // On the measured runtime the latter ten coefficients have just over
        // half the exact mass: ratio-then-scale loses one subnormal unit.
        // Derive the expected bits from this runtime's elementary functions.
        var bars = Enumerable.Range(0, 20).Select(i =>
            new Bar(default, 1, 2, -2, i < 10 ? double.Epsilon : 0, 1)).ToArray();
        var indicator = new SineWeightedAverage(20);
        using var run = await Build(bars, indicator).BuildAsync();
        var expected = NormalizedKernelReference.Values(bars, 20,
            lag => ReferenceFraction.FromDouble(Math.Sin((lag + 1d) * Math.PI / 21d)));
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[19]),
            BitConverter.DoubleToInt64Bits(run[indicator].ToArray()[19]));
        await Check(bars, indicator);
        await Check(bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, -b.Close, b.Volume)).ToArray(), indicator);
        await Check(Enumerable.Repeat(new Bar(default, 1, 2, -2, -0d, 1), 193).ToArray(), indicator);
    }

    [Fact]
    public void InstalledSimdMultiplyAddAllowsExactAccumulatorResultAliasing()
    {
        foreach (int count in new[] { 1, 3, 4, 5, 17, 129 })
        {
            var price = Enumerable.Range(0, count).Select(i => i % 2 == 0 ? 16383d : -16383d).ToArray();
            var sums = new double[count];
            for (int term = 1; term <= 64; term++)
            {
                SimdKernels.ScalarMultiplyAdd(sums, price, uint.MaxValue, sums);
                for (int i = 0; i < count; i++)
                    Assert.Equal((double)((long)price[i] * uint.MaxValue * term), sums[i]);
            }
        }
    }

    [Fact]
    public async Task BufferedRowsRetainAllFieldValidationCancellationAndRecovery()
    {
        var bars = Fixture(513);
        var indicator = new SineWeightedAverage(20);
        var builder = Build(bars, indicator);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        var valid = bars[127];
        bars[127] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        bars[127] = valid;
        await Check(bars, indicator);
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IndicatorBase indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    private static async Task Check(Bar[] bars, IndicatorBase indicator)
    {
        int period = indicator is GaussianWeightedAverage gaussian ? gaussian.Period : ((SineWeightedAverage)indicator).Period;
        double Weight(int lag)
        {
            if (indicator is SineWeightedAverage) return Math.Sin((lag + 1d) * Math.PI / (period + 1d));
            var g = (GaussianWeightedAverage)indicator;
            double x = (lag - period / 2) / (double)(period / 2) / g.Sigma;
            return Math.Exp(-.5 * x * x);
        }
        var expected = NormalizedKernelReference.Values(bars, period, lag => ReferenceFraction.FromDouble(Weight(lag))).ToArray();
        var state = (IIndicatorState)indicator.CreateState();
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
            bars.Select(b => state.Update(b)).Select(BitConverter.DoubleToInt64Bits));
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                using var run = await Build(input, indicator).ConfigureHistory(history).BuildAsync();
                Array.Clear(input);
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
                    run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (history != IndicatorHistoryMode.Full) continue;
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
