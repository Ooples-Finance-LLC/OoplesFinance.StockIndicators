using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CpuKernelTests
{
    public static IEnumerable<object[]> Cases => CpuKernelPilots.Ids.Select(id => new object[] { id });

    [Theory, MemberData(nameof(Cases))]
    public void PublicRouteBatchStreamingResetAndRepeatedPreviewAgree(string id)
    {
        var data = ComparisonVerifier.BenchmarkFixture(ComparisonPairs.Get(id), 160);
        var kernel = CpuKernelPilots.Create(id);
        var batch = new double[data.Count * kernel.OutputCount];
        CpuKernelPilots.Verify(id, data, kernel, batch);
        var row = new double[kernel.OutputCount];
        var preview = new double[kernel.OutputCount];
        foreach (var pass in Enumerable.Range(0, 2))
        {
            kernel.Reset();
            for (var i = 0; i < data.Count; i++)
            {
                // Preview a different observation first, so accidental commits are observable.
                kernel.Preview(data.IndicatorBars[(i + 7) % data.Count], preview);
                kernel.Preview(data.IndicatorBars[i], preview);
                kernel.Preview(data.IndicatorBars[i], row);
                Assert.Equal(preview, row);
                kernel.Update(data.IndicatorBars[i], row);
                Assert.Equal(preview, row);
                Assert.Equal(batch.AsSpan(i * kernel.OutputCount, kernel.OutputCount).ToArray(), row);
            }
        }
        kernel.Reset();
        var chunked = new double[batch.Length];
        kernel.Process(data.IndicatorBars.AsSpan(0, 37), chunked);
        kernel.Process(data.IndicatorBars.AsSpan(37), chunked.AsSpan(37 * kernel.OutputCount));
        Assert.Equal(batch, chunked);
    }

    [Theory, MemberData(nameof(Cases))]
    public void InvalidInputAndShortBuffersDoNotConsumeState(string id)
    {
        var kernel = CpuKernelPilots.Create(id);
        var data = ComparisonVerifier.BenchmarkFixture(ComparisonPairs.Get(id), 80);
        var expected = new double[data.Count * kernel.OutputCount];
        kernel.Process(data.IndicatorBars, expected);
        kernel.Reset();
        var output = new double[expected.Length];
        Assert.Throws<ArgumentException>(() => kernel.Process(data.IndicatorBars, new double[1]));
        Assert.Throws<ArgumentException>(() => kernel.Update(data.IndicatorBars[0], Array.Empty<double>()));
        var invalid = data.IndicatorBars.ToArray();
        invalid[^1] = new Bar(default, 0, 0, 0, double.NaN, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => kernel.Process(invalid, output));
        kernel.Process(data.IndicatorBars, output);
        Assert.Equal(expected, output);
    }

    [Theory, MemberData(nameof(Cases))]
    public void OrdinarySteadyStateAllocationIsExplicit(string id)
    {
        var kernel = CpuKernelPilots.Create(id);
        var bars = Enumerable.Range(0, 160).Select(i => new Bar(default,
            100 + i % 17 * .25, 105 + i % 17 * .25, 95 + i % 17 * .25,
            100 + i % 19 * .25, 1)).ToArray();
        var output = new double[bars.Length * kernel.OutputCount];
        for (var i = 0; i < 4; i++) { kernel.Reset(); kernel.Process(bars, output); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4; i++) { kernel.Reset(); kernel.Process(bars, output); }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    [Theory, MemberData(nameof(Cases))]
    public void BenchmarkFixtureSteadyStateAllocatesNoMemory(string id)
    {
        var data = ComparisonVerifier.BenchmarkFixture(ComparisonPairs.Get(id), 10_000);
        var kernel = CpuKernelPilots.Create(id);
        var output = new double[data.Count * kernel.OutputCount];
        for (var i = 0; i < 4; i++) { kernel.Reset(); kernel.Process(data.IndicatorBars, output); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        kernel.Reset(); kernel.Process(data.IndicatorBars, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData(PivotLevelStyle.Standard)]
    [InlineData(PivotLevelStyle.Camarilla)]
    [InlineData(PivotLevelStyle.Demark)]
    [InlineData(PivotLevelStyle.Fibonacci)]
    [InlineData(PivotLevelStyle.Woodie)]
    public void PivotOffsetsStylesAndPreviewMatchIndependentReference(PivotLevelStyle style)
    {
        var data = CompetitorData.Create(100);
        foreach (var period in new[] { 1, 3, 20 })
        foreach (var offset in new[] { 0, 1, 7 })
        {
            var expected = PivotLevelComparison.Pair(false, style, offset).Reference!(data, period);
            var kernel = IndicatorKernels.RollingPivots(period, offset, style);
            var output = new double[9];
            for (var i = 0; i < data.Count; i++)
            {
                kernel.Preview(data.IndicatorBars[i], output);
                for (var j = 0; j < 9; j++)
                {
                    var column = expected.Outputs[PivotLevelComparison.Names[j]];
                    Assert.Equal(column.Present![i] ? column.Values[i] : double.NaN, output[j]);
                }
                kernel.Update(data.IndicatorBars[i], output);
            }
        }
    }

    [Fact]
    public void FractalTiesAndAsymmetricDelayedConfirmationMatchIndependentReference()
    {
        var bars = Enumerable.Range(0, 100).Select(i => new Bar(default, 0, i % 9, -(i % 7), i % 4, 0)).ToArray();
        foreach (var close in new[] { false, true })
        {
            var kernel = IndicatorKernels.Fractal(2, 5, close);
            var output = new double[200]; kernel.Process(bars, output);
            for (var i = 0; i < bars.Length; i++)
            {
                var center = i - 5;
                double? high = null, low = null;
                if (center >= 2)
                {
                    var h = close ? bars[center].Close : bars[center].High;
                    var l = close ? bars[center].Close : bars[center].Low;
                    var others = Enumerable.Range(center - 2, 8).Where(j => j != center);
                    if (others.All(j => h > (close ? bars[j].Close : bars[j].High))) high = h;
                    if (others.All(j => l < (close ? bars[j].Close : bars[j].Low))) low = l;
                }
                Assert.Equal(high ?? double.NaN, output[2 * i]);
                Assert.Equal(low ?? double.NaN, output[2 * i + 1]);
            }
        }
    }

    [Fact]
    public void JurikCompactStagesRetainWideTinyAndCancellationRounding()
    {
        foreach (var scale in new[] { double.Epsilon, 1e-200, 1d, double.MaxValue / 32 })
        foreach (var period in new[] { 1, 2, 7, 20 })
        {
            var prices = Enumerable.Range(0, 70).Select(i => (i % 11 - 5) * scale).ToArray();
            var bars = prices.Select(p => new Bar(default, 0, 0, 0, p, 0)).ToArray();
            var expected = JurikComparison.Reference(prices, period, -50, 3, false);
            var kernel = IndicatorKernels.Jurik(period, -50, 3);
            var output = new double[prices.Length]; kernel.Process(bars, output);
            Assert.Equal(expected, output);
        }
    }

    [Fact]
    public void JurikMixedExponentSpansFallBackWithoutChangingTheRecurrence()
    {
        var random = new Random(819);
        var exponents = new[] { -1074, -1022, -200, -1, 0, 100, 800, 1000 };
        var prices = Enumerable.Range(0, 96).Select(_ =>
            Math.ScaleB(random.Next(-7, 8) / 8d, exponents[random.Next(exponents.Length)])).ToArray();
        var bars = prices.Select(p => new Bar(default, 0, 0, 0, p, 0)).ToArray();
        var expected = JurikComparison.Reference(prices, 7, 100, 4, false);
        var kernel = IndicatorKernels.Jurik(7, 100, 4);
        var output = new double[prices.Length]; kernel.Process(bars, output);
        Assert.Equal(expected, output);
        kernel.Reset();
        var preview = new double[1];
        for (var i = 0; i < bars.Length; i++)
        {
            kernel.Preview(bars[i], preview); Assert.Equal(expected[i], preview[0]);
            kernel.Update(bars[i], preview); Assert.Equal(expected[i], preview[0]);
        }
    }

    [Fact]
    public void FusedJurikStagesMatchTheIndependentIntegerReference()
    {
        var random = new Random(1074);
        var prices = Enumerable.Range(0, 800).Select(i => i % 3 == 0
            ? Math.BitIncrement(100d) : 100 + random.NextDouble() * .0001).ToArray();
        var expected = JurikComparison.Reference(prices, 13, 27, 7, false);
        var bars = prices.Select(p => new Bar(default, 0, 0, 0, p, 0)).ToArray();
        var kernel = IndicatorKernels.Jurik(13, 27, 7);
        var output = new double[prices.Length]; kernel.Process(bars, output);
        Assert.Equal(expected, output);
    }

    [Fact]
    public void TrueRangeWideIntermediateAndRejectedOverflowDoNotCorruptState()
    {
        var wide = new Bar(default, 0, double.MaxValue, -double.MaxValue, 0, 0);
        var kernel = IndicatorKernels.ScaledTrueRange(2);
        var output = new double[1]; kernel.Update(wide, output);
        Assert.Equal(double.MaxValue, output[0]);
        kernel = IndicatorKernels.ScaledTrueRange(1);
        Assert.Throws<OverflowException>(() => kernel.Update(wide, output));
        kernel.Update(new Bar(default, 1, 2, 1, 2, 0), output);
        Assert.Equal(1, output[0]);
    }
}
