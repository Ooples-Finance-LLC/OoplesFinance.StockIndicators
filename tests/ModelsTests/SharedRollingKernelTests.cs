using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedRollingKernelTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(7, false)]
    [InlineData(1025, false)]
    [InlineData(int.MaxValue, false)]
    [InlineData(7, true)]
    public async Task SumReusesExactGridProofAndReplaysRejectedBatches(int period, bool reject)
    {
        var bars = Enumerable.Range(0, 4097).Select(i =>
        {
            var value = i % 13 == 0 ? -0d : (i % 31 - 15) / 8d;
            return new Bar(default, value, value, value, value, 1);
        }).ToArray();
        if (reject) bars[4095] = new Bar(default, 0, 0, 0, double.Epsilon, 1);
        var indicator = new RollingPriceSum(period);
        var reference = (IIndicatorState)indicator.CreateState();
        var expected = bars.Select(b => reference.Update(b)).ToArray();
        var history = new OwnedBarHistory();
        history.AppendValidated(bars, default);
        var state = (IOwnedHistoryBatchState)indicator.CreateState();
        var output = new[] { new double[bars.Length] };
        Assert.Equal(!reject, state.TryComputeBatch(history, output));
        if (!reject) AssertBits(expected, output[0]);
        foreach (var mode in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            AssertBits(expected, run[indicator].ToArray());
        }
        // A speculative batch must leave the scalar fallback unconsumed.
        var scalar = (IIndicatorState)state;
        AssertBits(expected, bars.Select(b => scalar.Update(b)).ToArray());
    }

    [Fact]
    public async Task CachedCoefficientPrefixesPreserveIndependentFormulaContracts()
    {
        var factories = new Func<IIndicator>[]
        {
            () => new NormalizedConvolution(new[] { 1d, -1d, 2d, -2d }),
            () => new NormalizedConvolution(new[] { double.Epsilon, double.MaxValue, -double.MaxValue }),
            () => new GaussianWeightedAverage(7, .4),
            () => new SineWeightedAverage(7)
        };
        foreach (var factory in factories)
        {
            var report = await IndicatorValidation.ValidateAsync(new(factory().GetType(), "cached-coefficient-prefix", factory));
            report.ThrowIfInvalid();
        }
    }

    private static void AssertBits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
