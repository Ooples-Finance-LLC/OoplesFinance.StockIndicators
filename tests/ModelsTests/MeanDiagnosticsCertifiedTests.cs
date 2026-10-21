using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class MeanDiagnosticsCertifiedTests
{
    private static Queue<RoundedMeanErrorWindow.Sample> Samples(IEnumerable<double> values) =>
        new(values.Select(v => new RoundedMeanErrorWindow.Sample(v, ExactVarianceWindow.Units(v))));

    private static double Reference(double[] values, double mean)
    {
        var center = ReferenceFraction.FromDouble(mean);
        var sum = new ReferenceFraction(0);
        foreach (double value in values)
        {
            var price = ReferenceFraction.FromDouble(value);
            sum += (price - center).Abs() / price;
        }
        return (sum / new ReferenceFraction(values.Length)).ToDouble();
    }

    [Fact]
    public void CertifiedSignedQuotientsMatchIndependentFractionsAcrossNormalExponents()
    {
        var random = new Random(7232026);
        int accepted = 0;
        for (int trial = 0; trial < 1000; trial++)
        {
            double scale = Math.ScaleB(1, random.Next(-900, 901));
            double mean = 1.25 * scale;
            var values = Enumerable.Range(0, random.Next(1, 25))
                .Select(_ => (1 + random.NextDouble()) * scale * (random.Next(2) == 0 ? -1 : 1)).ToArray();
            var expected = Reference(values, mean);
            if (CertifiedMeanRelativeError.TryCompute(Samples(values), mean, out double actual))
            {
                accepted++;
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
            }
            var window = new RoundedMeanErrorWindow(values.Length);
            foreach (double value in values) window.Add(value);
            var fallbackOrCertified = window.SignedRelativeError(mean);
            Assert.True(fallbackOrCertified.Defined);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(fallbackOrCertified.Value));
        }
        Assert.True(accepted > 950, $"Only {accepted} normal windows certified");
    }

    [Fact]
    public void UncertainCancellationUsesExactFallback()
    {
        double[] values = [3, 6, -2];
        Assert.False(CertifiedMeanRelativeError.TryCompute(Samples(values), 1, out _));
        var window = new RoundedMeanErrorWindow(3);
        foreach (double value in values) window.Add(value);
        var result = window.SignedRelativeError(1);
        Assert.True(result.Defined);
        Assert.Equal(0, BitConverter.DoubleToInt64Bits(result.Value));
        Assert.Equal(0, Reference(values, 1));
    }

    [Fact]
    public void TinyDistanceRetainsExactFallbackBits()
    {
        double[] values = [1, Math.BitIncrement(1)];
        Assert.False(CertifiedMeanRelativeError.TryCompute(Samples(values), 1, out _));
        var window = new RoundedMeanErrorWindow(2);
        foreach (double value in values) window.Add(value);
        Assert.Equal(BitConverter.DoubleToInt64Bits(Reference(values, 1)),
            BitConverter.DoubleToInt64Bits(window.SignedRelativeError(1).Value));
    }

    [Fact]
    public void UnsupportedScalesAndZeroPricesDoNotProduceCertificates()
    {
        foreach (double[] values in new[]
        {
            new[] { double.Epsilon, 1d }, new[] { 1d, Math.ScaleB(1, 40) },
            new[] { 0d, 1d }, new[] { -0d, -1d }
        })
            Assert.False(CertifiedMeanRelativeError.TryCompute(Samples(values), 1, out _));
        var zeroWindow = new RoundedMeanErrorWindow(2);
        zeroWindow.Add(0); zeroWindow.Add(1);
        Assert.Equal((false, 0d), zeroWindow.SignedRelativeError(zeroWindow.Mean));
    }

    [Fact]
    public void ZeroCenterAndConstantWindowsCertifyExactZero()
    {
        Assert.True(CertifiedMeanRelativeError.TryCompute(Samples([2, -3]), -0d, out double cancelled));
        Assert.Equal(0, BitConverter.DoubleToInt64Bits(cancelled));
        Assert.True(CertifiedMeanRelativeError.TryCompute(Samples([5, 5, 5]), 5, out double constant));
        Assert.Equal(0, BitConverter.DoubleToInt64Bits(constant));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    [InlineData(33)]
    [InlineData(int.MaxValue)]
    public void FullDiagnosticsPreserveAllBitsPresenceAndReset(int period)
    {
        var bars = Enumerable.Range(0, 160).Select(i =>
        {
            double value = i % 53 == 0 ? 0 : 10 + (i * 17 % 73) / 8d;
            return new Bar(default, value, value, value, value, 1);
        }).ToArray();
        var expected = MeanErrorReference.Values(bars, period, true);
        var state = new MovingAverageDiagnostics.State(period);
        var actual = new double[8];
        for (int repeat = 0; repeat < 2; repeat++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(bars[i], actual);
                for (int slot = 0; slot < actual.Length; slot++)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i]), BitConverter.DoubleToInt64Bits(actual[slot]));
            }
        }
    }

    [Fact]
    public void OrdinaryCertificateEvaluationAllocatesNoManagedMemory()
    {
        var values = Samples(Enumerable.Range(0, 20).Select(i => 100 + i / 8d));
        const double mean = 101.1875;
        for (int i = 0; i < 32; i++) Assert.True(CertifiedMeanRelativeError.TryCompute(values, mean, out _));
        int accepted = 0;
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            if (CertifiedMeanRelativeError.TryCompute(values, mean, out _)) accepted++;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(1000, accepted);
        Assert.Equal(0, bytes);
    }
}
