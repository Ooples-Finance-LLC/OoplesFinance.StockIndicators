using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class WindowedSincCompactTests
{
    [Fact]
    public void EveryWindowPreservesIndependentExactConvolutionAndStartup()
    {
        var prices = Enumerable.Range(0, 47).Select(i => 100 + Math.Sin(i * .73) * 20).ToArray();
        foreach (var window in Enum.GetValues<SincWindow>())
            foreach (int taps in new[] { 3, 9, 21 })
                foreach (int period in new[] { 3, 20 })
                    Check(prices, period, taps, window);
    }

    [Fact]
    public void ExponentBoundsCancellationAndZeroSignsPreserveRounding()
    {
        foreach (int exponent in new[] { -1074, -1022, -450, 0, 450, 1020 })
            Check(Enumerable.Range(0, 31).Select(i =>
                Math.ScaleB((i % 2 == 0 ? 1 : -1) * (1.25 + Math.Sin(i) * .2), exponent)).ToArray(),
                20, 21, SincWindow.Hann);
        Check([0, -0d, 0, -0d], 14, 21, SincWindow.Hann);
        Check([1, -1, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue],
            3, 9, SincWindow.Rectangular);
    }

    [Fact]
    public void KernelAndOverflowRejectionRemainUnchanged()
    {
        var weights = WindowedSincSnapshot.CreateWeights(3, 9, SincWindow.Rectangular, out _);
        Check(weights.Select(w => Math.Sign(w) * double.MaxValue).ToArray(), 3, 9, SincWindow.Rectangular);
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowedSincSnapshot.Calculate(
            [new Bar(default, 0, 1, 0, 1, 1)], 14, 2, SincWindow.Hann));
    }

    private static void Check(double[] prices, int period, int taps, SincWindow window)
    {
        // The unchanged published binary64 coefficient bits define the kernel.
        // Independent rational products and division establish the output bits.
        var weights = WindowedSincSnapshot.CreateWeights(period, taps, window, out _)
            .Select(ReferenceFraction.FromDouble).ToArray();
        var mass = weights.Aggregate(new ReferenceFraction(0), (sum, value) => sum + value);
        var expected = new double[prices.Length];
        for (int i = 0; i < prices.Length; i++)
        {
            var sum = new ReferenceFraction(0);
            int start = Math.Max(0, i - taps + 1);
            for (int k = 0; k < taps; k++)
                sum += ReferenceFraction.FromDouble(prices[Math.Min(i, start + k)]) * weights[k];
            expected[i] = (sum / mass).ToDouble();
        }
        var bars = prices.Select(v => new Bar(default, 0, 1, 0, v, 1)).ToArray();
        if (expected.Any(v => !double.IsFinite(v)))
        {
            Assert.Throws<OverflowException>(() => WindowedSincSnapshot.Calculate(bars, period, taps, window));
            return;
        }
        var actual = WindowedSincSnapshot.Calculate(bars, period, taps, window);
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
    }
}
