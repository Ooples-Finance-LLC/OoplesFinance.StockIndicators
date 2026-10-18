using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AffineConvolutionTests
{
    [Fact]
    public async Task IntegerProgressionsPreserveExactStartupEvictionResetAndPublication()
    {
        double[] values = [-double.MaxValue, double.MaxValue, -double.Epsilon, double.Epsilon, -0d, 0d, -1, 1, .125, 19];
        var bars = Enumerable.Range(0, 83).Select(i => new Bar(default, 0, 1, -1, values[i % values.Length], 1)).ToArray();
        double[][] kernels = [[1], [-1], [0d, -0d, 0d], [2, 2, 2], [1, 2, 3, 4], [4, 3, 2, 1],
            [1, -1], [-3, 0, 3], [6, 3, 0, -3, -6], [int.MaxValue, int.MaxValue - 1],
            [int.MinValue, int.MinValue + 1], [int.MaxValue, -1], [int.MinValue, -1]];
        foreach (var kernel in kernels)
        {
            var indicator = new NormalizedConvolution(kernel);
            Assert.True(indicator.HasAffineKernel);
            var expected = NormalizedKernelReference.Values(bars, kernel.Length, lag => ReferenceFraction.FromDouble(kernel[lag]));
            var state = (IIndicatorState)indicator.CreateState();
            Assert.IsType<AffineConvolutionState>(state);
            for (int pass = 0; pass < 3; pass++)
            {
                state.Reset();
                Bits(expected, bars.Select(b => state.Update(in b)));
            }
            // Some signed kernels legitimately overflow. Direct state references
            // cover those values; publication must preserve the ordinary rejection.
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
                if (expected.Any(v => !double.IsFinite(v)))
                {
                    int index = Enumerable.Range(0, expected.Count).First(i => !double.IsFinite(expected[i]));
                    var referenceError = Assert.Throws<IndicatorOutputException>(() =>
                        IndicatorOutputPolicy.Validate(indicator, 0, index, expected[index]));
                    var actualError = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
                    Assert.Equal(referenceError.Message, actualError.Message);
                }
                else
                {
                    using var run = await builder.BuildAsync();
                    Bits(expected, run[indicator].ToArray());
                }
            }
        }
    }

    [Fact]
    public void NonprogressionsAndUnrepresentableCoefficientsRetainGeneralKernel()
    {
        double[][] kernels = [[1, 2, Math.BitIncrement(3)], [.5, 1, 1.5], [double.Epsilon, 2 * double.Epsilon],
            [double.MaxValue], [int.MinValue, int.MaxValue], [int.MaxValue + 1d, int.MaxValue + 2d]];
        foreach (var kernel in kernels)
        {
            var indicator = new NormalizedConvolution(kernel);
            Assert.False(indicator.HasAffineKernel);
            Assert.IsType<NormalizedKernelState>(indicator.CreateState());
        }
    }

    [Fact]
    public void RecognitionAndExecutionUseTheOwnedCoefficientSnapshot()
    {
        double[] kernel = [1, 2, 3];
        var indicator = new NormalizedConvolution(kernel);
        Array.Clear(kernel);
        var state = (IIndicatorState)indicator.CreateState();
        Assert.Equal(2d, state.Update(new Bar(default, 0, 0, 0, 2, 1)));
        Assert.Equal(8d / 3, state.Update(new Bar(default, 0, 0, 0, 4, 1)));
        Assert.Equal(new[] { 1d, 2d, 3d }, indicator.Kernel);
    }

    private static void Bits(IEnumerable<double> expected, IEnumerable<double> actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
