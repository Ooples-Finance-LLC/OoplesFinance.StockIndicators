using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class MeanDiagnosticsCertifiedTests
{
    [Fact]
    public void CountPublicationMatchesIndependentFractionsAcrossWidthsAndScales()
    {
        var random = new Random(472051);
        int accepted = 0;
        foreach (int width in new[] { 1, 31, 52, 53, 64, 84, 110, 128 })
        foreach (int count in new[] { 1, 2, 3, 14, 255, 65537, int.MaxValue })
        foreach (int scale in new[] { -1000, -500, 0, 500, 900 })
        {
            UInt128 mask = width == 128 ? UInt128.MaxValue : ((UInt128)1 << width) - 1;
            UInt128 numerator = (((UInt128)(ulong)random.NextInt64() << 64)
                | (ulong)random.NextInt64()) & mask;
            numerator |= (UInt128)1 << (width - 1);
            if (!CertifiedMeanRelativeError.TryDivideCount(numerator, count, scale, out double actual)) continue;
            accepted++;
            Assert.Equal(BitConverter.DoubleToInt64Bits(CountReference(numerator, count, scale)),
                BitConverter.DoubleToInt64Bits(actual));
        }
        Assert.True(accepted > 250, $"Only {accepted} count divisions certified");
    }

    [Fact]
    public void CountPublicationRetainsDiscardedBitsAndEvenMidpointTies()
    {
        foreach (int shift in new[] { 1, 2, 20, 64 })
        foreach (int count in new[] { 1, 3, 17 })
        foreach (long significand in new[] { 1L << 52, (1L << 52) + 1, (1L << 53) - 1 })
        foreach (int offset in new[] { -1, 0, 1 })
        {
            UInt128 numerator = (((UInt128)(ulong)significand << shift) + ((UInt128)1 << (shift - 1))) * (uint)count;
            numerator = offset < 0 ? numerator - 1 : numerator + (uint)offset;
            Assert.True(CertifiedMeanRelativeError.TryDivideCount(numerator, count, -shift, out double actual));
            Assert.Equal(BitConverter.DoubleToInt64Bits(CountReference(numerator, count, -shift)),
                BitConverter.DoubleToInt64Bits(actual));
        }
    }

    [Fact]
    public void CountPublicationKeepsUnsupportedBoundariesOnFallback()
    {
        Assert.False(CertifiedMeanRelativeError.TryDivideCount(1, 0, 0, out _));
        Assert.False(CertifiedMeanRelativeError.TryDivideCount(1, -1, 0, out _));
        Assert.True(CertifiedMeanRelativeError.TryDivideCount(0, 1, int.MinValue, out double zero));
        Assert.Equal(0, BitConverter.DoubleToInt64Bits(zero));
        Assert.False(CertifiedMeanRelativeError.TryDivideCount(1, 1, -1074, out _));
        Assert.False(CertifiedMeanRelativeError.TryDivideCount(1, 1, int.MaxValue, out _));
        Assert.False(CertifiedMeanRelativeError.TryDivideCount(1, 1, int.MinValue, out _));
        Assert.False(CertifiedMeanRelativeError.TryDivideCount(((UInt128)1 << 54) - 1, 1, 970, out _));
    }

    private static double CountReference(UInt128 numerator, int count, int scale)
    {
        var exact = new ReferenceFraction((BigInteger)numerator) / new ReferenceFraction(count);
        var factor = new ReferenceFraction(BigInteger.One << Math.Abs(scale));
        return (scale < 0 ? exact / factor : exact * factor).ToDouble();
    }

    [Fact]
    public void CachedReciprocalsCertifyBothExponentShiftBoundariesAndNegativeCenters()
    {
        foreach (double sign in new[] { 1d, -1d })
        {
            double[] values = [.125 * sign, 1 * sign, 8 * sign];
            var samples = Samples(values);
            Assert.True(CertifiedMeanRelativeError.TryCompute(samples, sign, out double actual));
            Assert.Equal(BitConverter.DoubleToInt64Bits(Reference(values, sign)), BitConverter.DoubleToInt64Bits(actual));
        }
        double[] mixed = [.125, -1, 8];
        Assert.True(CertifiedMeanRelativeError.TryCompute(Samples(mixed), -1, out double result));
        Assert.Equal(BitConverter.DoubleToInt64Bits(Reference(mixed, -1)), BitConverter.DoubleToInt64Bits(result));
    }

    [Fact]
    public void RetainedSamplesSupportChangingMeansWithoutRecomputingTheirValue()
    {
        double[] values = [-4.125, -2.25, -1.5, -.875, .625, 1.125, 2.75, 4.25];
        var samples = Samples(values);
        var window = new RoundedMeanErrorWindow(values.Length);
        foreach (double value in values) window.Add(value);
        int accepted = 0;
        foreach (double mean in new[] { -2d, -1.25, -.75, 0, .75, 1.25, 2 })
        {
            long expected = BitConverter.DoubleToInt64Bits(Reference(values, mean));
            if (CertifiedMeanRelativeError.TryCompute(samples, mean, out double actual))
            {
                accepted++;
                Assert.Equal(expected, BitConverter.DoubleToInt64Bits(actual));
            }
            Assert.Equal(expected, BitConverter.DoubleToInt64Bits(window.SignedRelativeError(mean).Value));
        }
        Assert.True(accepted >= 4);
    }

    [Fact]
    public void CachedErrorsRetainBitsThroughExactMaterializationEvictionAndReset()
    {
        var window = new RoundedMeanErrorWindow(3);
        var retained = new Queue<double>();
        double[] prices = [1.125, 1.25, 1.5, double.MaxValue, -double.MaxValue, 1.125, 2.25, 4.5, -.75, -1.25, -2.5];
        for (int repeat = 0; repeat < 2; repeat++)
        {
            window.Reset(); retained.Clear();
            foreach (double price in prices)
            {
                if (retained.Count == 3) retained.Dequeue();
                retained.Enqueue(price); window.Add(price);
                _ = window.SquaredError(0); // Materialize the preserved exact moments.
                var result = window.SignedRelativeError(1);
                Assert.True(result.Defined);
                Assert.Equal(BitConverter.DoubleToInt64Bits(Reference(retained.ToArray(), 1)), BitConverter.DoubleToInt64Bits(result.Value));
            }
        }
    }

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
    public void FusedDiagnosticsMatchIndependentFractionsForSignedInputsAndCenters()
    {
        var random = new Random(24102026);
        int accepted = 0;
        for (int trial = 0; trial < 500; trial++)
        {
            double scale = Math.ScaleB(1, random.Next(-400, 401));
            double mean = (trial % 2 == 0 ? 1.25 : -1.25) * scale;
            var values = Enumerable.Range(0, random.Next(1, 40))
                .Select(_ => (1 + random.NextDouble()) * scale * (random.Next(2) == 0 ? -1 : 1)).ToArray();
            if (!CertifiedMeanRelativeError.TryDiagnostics(Samples(values), mean,
                    out double absolute, out double squared, out double relative)) continue;
            accepted++;
            var absSum = new ReferenceFraction(0);
            var squaredSum = new ReferenceFraction(0);
            var relativeSum = new ReferenceFraction(0);
            foreach (double value in values)
            {
                var input = ReferenceFraction.FromDouble(value);
                var distance = (input - ReferenceFraction.FromDouble(mean)).Abs();
                absSum += distance;
                squaredSum += distance * distance;
                relativeSum += distance / input;
            }
            var count = new ReferenceFraction(values.Length);
            Assert.Equal(BitConverter.DoubleToInt64Bits((absSum / count).ToDouble()), BitConverter.DoubleToInt64Bits(absolute));
            Assert.Equal(BitConverter.DoubleToInt64Bits((squaredSum / count).ToDouble()), BitConverter.DoubleToInt64Bits(squared));
            Assert.Equal(BitConverter.DoubleToInt64Bits((relativeSum / count).ToDouble()), BitConverter.DoubleToInt64Bits(relative));
        }
        Assert.True(accepted > 450, $"Only {accepted} fused diagnostics certified");
    }

    [Fact]
    public void FusedDiagnosticsRetainZeroFlagsAndUnsupportedBoundaries()
    {
        foreach (double value in new[] { -5d, 5d })
        {
            Assert.True(CertifiedMeanRelativeError.TryDiagnostics(Samples([value, value]), value,
                out double absolute, out double squared, out double relative));
            Assert.Equal(0, BitConverter.DoubleToInt64Bits(absolute));
            Assert.Equal(0, BitConverter.DoubleToInt64Bits(squared));
            Assert.Equal(0, BitConverter.DoubleToInt64Bits(relative));
        }
        Assert.False(CertifiedMeanRelativeError.TryDiagnostics(Samples([]), 1, out _, out _, out _));
        Assert.False(CertifiedMeanRelativeError.TryDiagnostics(Samples([0, 1]), 1, out _, out _, out _));
        Assert.False(CertifiedMeanRelativeError.TryDiagnostics(Samples([1, -1]), 0, out _, out _, out _));
        Assert.False(CertifiedMeanRelativeError.TryDiagnostics(Samples([double.Epsilon, 1]), 1, out _, out _, out _));
        Assert.False(CertifiedMeanRelativeError.TryDiagnostics(Samples([1, Math.ScaleB(1, 40)]), 1, out _, out _, out _));
        Assert.False(CertifiedMeanRelativeError.TryDiagnostics(Samples(Enumerable.Repeat(15d, 4096)), 1, out _, out _, out _));
        foreach (int exponent in new[] { -600, 600 })
            Assert.False(CertifiedMeanRelativeError.TryDiagnostics(Samples([Math.ScaleB(1, exponent)]),
                Math.ScaleB(1.5, exponent), out _, out _, out _));
    }

    [Fact]
    public void ExactCompactErrorsMatchIndependentFractionsAcrossScales()
    {
        var random = new Random(7232027);
        int accepted = 0;
        for (int trial = 0; trial < 500; trial++)
        {
            double scale = Math.ScaleB(1, random.Next(-400, 401));
            double mean = 1.25 * scale;
            var values = Enumerable.Range(0, random.Next(1, 40))
                .Select(_ => (1 + random.NextDouble()) * scale * (random.Next(2) == 0 ? -1 : 1)).ToArray();
            foreach (bool squared in new[] { false, true })
            {
                var sum = new ReferenceFraction(0);
                foreach (double value in values)
                {
                    var difference = (ReferenceFraction.FromDouble(value) - ReferenceFraction.FromDouble(mean)).Abs();
                    sum += squared ? difference * difference : difference;
                }
                double expected = (sum / new ReferenceFraction(values.Length)).ToDouble();
                var window = new RoundedMeanErrorWindow(values.Length);
                foreach (double value in values) window.Add(value);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected),
                    BitConverter.DoubleToInt64Bits(squared ? window.SquaredError(mean) : window.AbsoluteError(mean)));
                var combined = window.AbsoluteAndSquaredError(mean);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected),
                    BitConverter.DoubleToInt64Bits(squared ? combined.Squared : combined.Absolute));
                if (CertifiedMeanRelativeError.TryError(Samples(values), mean, squared, out double actual))
                {
                    accepted++;
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
                }
            }
        }
        Assert.True(accepted > 950, $"Only {accepted} compact errors certified");
    }

    [Fact]
    public void CompactErrorBoundariesRetainFallbackAndExactZero()
    {
        foreach (bool squared in new[] { false, true })
        {
            Assert.True(CertifiedMeanRelativeError.TryError(Samples([5, 5]), 5, squared, out double zero));
            Assert.Equal(0, BitConverter.DoubleToInt64Bits(zero));
            Assert.False(CertifiedMeanRelativeError.TryError(Samples([0, 1]), 1, squared, out _));
            Assert.False(CertifiedMeanRelativeError.TryError(Samples([double.Epsilon, 1]), 1, squared, out _));
            Assert.False(CertifiedMeanRelativeError.TryError(Samples([1, Math.ScaleB(1, 40)]), 1, squared, out _));
            Assert.False(CertifiedMeanRelativeError.TryError(Samples([1, -1]), 0, squared, out _));
        }
        var large = Samples(Enumerable.Repeat(15d, 4096));
        Assert.False(CertifiedMeanRelativeError.TryError(large, 1, true, out _));
        var window = new RoundedMeanErrorWindow(4096);
        for (int i = 0; i < 4096; i++) window.Add(15);
        Assert.Equal(196d, window.SquaredError(1));
        Assert.False(CertifiedMeanRelativeError.TryErrors(large, 1, out _, out _));
        Assert.Equal((14d, 196d), window.AbsoluteAndSquaredError(1));
        Assert.False(CertifiedMeanRelativeError.TryError(Samples([Math.ScaleB(1, -600)]), Math.ScaleB(1.5, -600), true, out _));
        Assert.False(CertifiedMeanRelativeError.TryError(Samples([Math.ScaleB(1, 600)]), Math.ScaleB(1.5, 600), true, out _));
    }

    [Fact]
    public void CompactAbsoluteAndSquaredEvaluationAllocateNoManagedMemory()
    {
        var values = Samples(Enumerable.Range(0, 20).Select(i => 100 + i / 8d));
        for (int i = 0; i < 32; i++)
        {
            Assert.True(CertifiedMeanRelativeError.TryError(values, 101.1875, false, out _));
            Assert.True(CertifiedMeanRelativeError.TryError(values, 101.1875, true, out _));
            Assert.True(CertifiedMeanRelativeError.TryErrors(values, 101.1875, out _, out _));
        }
        int accepted = 0;
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            if (CertifiedMeanRelativeError.TryError(values, 101.1875, false, out _)) accepted++;
            if (CertifiedMeanRelativeError.TryError(values, 101.1875, true, out _)) accepted++;
            if (CertifiedMeanRelativeError.TryErrors(values, 101.1875, out _, out _)) accepted++;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(3000, accepted);
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void CompactWindowMeanPreservesBitsAcrossEvictionResetAndExactFallback()
    {
        var random = new Random(7232028);
        double[][] cases =
        [
            Enumerable.Range(0, 160).Select(i => 100 + (i * 17 % 73) / 8d).ToArray(),
            [0, -0d, 2, -2, 8, -8, Math.BitIncrement(1), 1, 0, -4, 4],
            [double.Epsilon, -double.Epsilon, 2 * double.Epsilon, Math.ScaleB(1, -1022),
                Math.ScaleB(-1, -1022), 1, 2, 0, double.MaxValue, -double.MaxValue,
                Math.ScaleB(1, -900), Math.ScaleB(1, 900)],
            Enumerable.Range(0, 128).Select(_ =>
            {
                double value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
                return double.IsFinite(value) ? value : 0;
            }).ToArray()
        ];
        foreach (double[] prices in cases)
        foreach (int period in new[] { 1, 2, 20, int.MaxValue })
        {
            var window = new RoundedMeanErrorWindow(period);
            var history = new Queue<double>();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                window.Reset(); history.Clear();
                foreach (double price in prices)
                {
                    if (history.Count == period) history.Dequeue();
                    history.Enqueue(price); window.Add(price);
                    var sum = new ReferenceFraction(0);
                    foreach (double value in history) sum += ReferenceFraction.FromDouble(value);
                    double expected = (sum / new ReferenceFraction(history.Count)).ToDouble();
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(window.Mean));
                }
            }
        }
    }

    [Fact]
    public void SquaredFallbackMaterializesTheCurrentWindowAndKeepsEvictionExact()
    {
        var window = new RoundedMeanErrorWindow(3);
        var history = new Queue<double>();
        foreach (double price in new[] { 1d, 2, 3, 4, -8, 16, -32, 64, 0, 1 })
        {
            if (history.Count == 3) history.Dequeue();
            history.Enqueue(price); window.Add(price);
            var sum = new ReferenceFraction(0); var squares = new ReferenceFraction(0);
            foreach (double value in history)
            {
                var exact = ReferenceFraction.FromDouble(value);
                sum += exact; squares += exact * exact;
            }
            // Zero center deliberately uses the preserved exact moment path.
            Assert.Equal(BitConverter.DoubleToInt64Bits((squares / new ReferenceFraction(history.Count)).ToDouble()),
                BitConverter.DoubleToInt64Bits(window.SquaredError(0)));
            Assert.Equal(BitConverter.DoubleToInt64Bits((sum / new ReferenceFraction(history.Count)).ToDouble()),
                BitConverter.DoubleToInt64Bits(window.Mean));
        }
    }

    [Fact]
    public void InvalidInputLeavesCompactHistoryUnchanged()
    {
        var window = new RoundedMeanErrorWindow(3);
        window.Add(1); window.Add(2); window.Add(3);
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => window.Add(invalid));
            Assert.Equal(3, window.Count);
            Assert.Equal(2d, window.Mean);
        }
        window.Add(4);
        Assert.Equal(3d, window.Mean);
    }

    [Fact]
    public void OrdinaryWindowUpdatesAndMeanAllocateNoManagedMemoryAfterReset()
    {
        var window = new RoundedMeanErrorWindow(20);
        window.Add(0); window.SquaredError(0); window.Reset();
        for (int i = 0; i < 64; i++) { window.Add(100 + i % 7 / 8d); _ = window.Mean; }
        double total = 0;
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            window.Add(100 + i % 7 / 8d);
            total += window.Mean;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.True(total > 100_000);
        Assert.Equal(0, bytes);
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
