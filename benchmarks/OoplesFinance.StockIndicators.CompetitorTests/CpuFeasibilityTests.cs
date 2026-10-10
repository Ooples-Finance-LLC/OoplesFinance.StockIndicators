using System.Runtime.Intrinsics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Range = System.Range;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class CpuFeasibilityTests
{
    [Fact]
    public void BenchmarkSetupVerifiesNativeStatusOutputsAndCertificateEligibility()
    {
        new AsinFeasibilityBenchmarks { Count = 1000 }.Setup();
        new AsinOwnershipFeasibilityBenchmarks { Count = 1000 }.Setup();
        new SmaFeasibilityBenchmarks { Count = 1000, Grid = true }.Setup();
        new SmaFeasibilityBenchmarks { Count = 1000, Grid = false }.Setup();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task OwnedPipelineMatchesBuilderAndRetainsHistory(bool parallel, bool direct)
    {
        var bars = Enumerable.Range(0, 2051).Select(i => new Bar(default, i, i + 1, i - 1, (i % 13 - 6) / 4d, 0)).ToArray();
        bars[1024] = new Bar(default, 0, 0, 0, -0d, 0);
        var indicator = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        var result = CpuFeasibilityPrototypes.AsinOwnedPipeline(bars, parallel, direct);
        AsinFeasibilityBenchmarks.RequireSame(run[indicator.Value].ToArray(), result.Values);
        Assert.Equal(run[indicator.IsDefined].ToArray(), result.Presence);
        Assert.Equal(bars, result.History.SelectMany(chunk => chunk).ToArray());
        var first = bars[0]; bars[0] = default;
        Assert.Equal(first, result.History[0][0]);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => CpuFeasibilityPrototypes.IngestArray(bars, cancelled.Token, direct));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ArrayIngestionPreservesEveryFieldDiagnostic(int field)
    {
        var fields = new[] { 1d, 2, 0, 1, 0 }; fields[field] = double.NaN;
        var invalid = new Bar(default, fields[0], fields[1], fields[2], fields[3], fields[4]);
        var expected = Assert.Throws<ArgumentOutOfRangeException>(() => OoplesFinance.StockIndicators.Validation.IndicatorInputDomain.Finite.Validate(invalid));
        var bars = Enumerable.Repeat(new Bar(default, 1, 1, 1, 1, 0), 1024).Append(invalid).ToArray();
        var actual = Assert.Throws<ArgumentOutOfRangeException>(() => CpuFeasibilityPrototypes.IngestArray(bars));
        Assert.Equal(expected.Message, actual.Message);
        actual = Assert.Throws<ArgumentOutOfRangeException>(() => CpuFeasibilityPrototypes.IngestArray(bars, direct: true));
        Assert.Equal(expected.Message, actual.Message);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(7)] [InlineData(20)] [InlineData(127)]
    public void VectorWindowsMatchIndependentIntegerSumsAndPreserveTail(int period)
    {
        var random = new Random(6547 + period);
        foreach (var count in new[] { 0, 1, period - 1, period, period + 1, period + 4, 2051 })
        {
            var units = Enumerable.Range(0, count).Select(_ => (long)random.Next(-1000000, 1000000)).ToArray();
            var input = units.Select(v => v / 1024d).ToArray();
            var output = Enumerable.Repeat(123d, count + 2).ToArray();
            var accepted = CpuFeasibilityPrototypes.TrySmaVector(input, output, period);
            Assert.Equal(Vector256.IsHardwareAccelerated, accepted);
            if (!accepted) continue;
            for (var i = 0; i < count; i++)
            {
                long sum = 0;
                if (i >= period - 1)
                    for (var j = i - period + 1; j <= i; j++) sum += units[j];
                Assert.Equal(BitConverter.DoubleToInt64Bits(sum / 1024d / period), BitConverter.DoubleToInt64Bits(output[i]));
            }
            Assert.All(output.Skip(count), v => Assert.Equal(123d, v));
        }
    }

    [Fact]
    public void LateRejectionLeavesOutputUntouchedAndFallbackPreservesExtremeValues()
    {
        foreach (var last in new[] { double.Epsilon, double.MaxValue, double.NaN, double.PositiveInfinity, 1d / 3 })
        {
            var input = Enumerable.Repeat(1d, 100).Append(last).ToArray();
            var output = Enumerable.Repeat(321d, input.Length).ToArray();
            Assert.False(CpuFeasibilityPrototypes.TrySmaVector(input, output, 20));
            Assert.All(output, v => Assert.Equal(321d, v));
        }
        foreach (var input in new[] { new[] { 1e100, 1d, -1e100 }, new[] { double.MaxValue, double.MaxValue, double.MaxValue }, new double[35] })
        {
            var expected = new double[input.Length]; var actual = new double[input.Length];
            CpuFeasibilityPrototypes.CurrentSma(input, expected, 3);
            CpuFeasibilityPrototypes.SmaVector(input, actual, 3);
            AsinFeasibilityBenchmarks.RequireSame(expected, actual);
        }
        var cancel = new double[3];
        CpuFeasibilityPrototypes.SmaVector(new[] { 1e100, 1d, -1e100 }, cancel, 3);
        Assert.Equal(1d / 3, cancel[2]);
    }

    [Fact]
    public void CertificateCoversExponentAndPrecisionBoundaries()
    {
        if (!Vector256.IsHardwareAccelerated) return;
        foreach (var exponent in new[] { -512, -100, 0, 450 })
        {
            var units = Enumerable.Range(0, 103).Select(i => (long)(i % 17 - 8)).ToArray();
            var input = units.Select(v => Math.ScaleB(v, exponent)).ToArray();
            var actual = new double[input.Length];
            Assert.True(CpuFeasibilityPrototypes.TrySmaVector(input, actual, 7));
            for (var i = 6; i < input.Length; i++)
            {
                var sum = units.Skip(i - 6).Take(7).Sum();
                Assert.Equal(BitConverter.DoubleToInt64Bits(Math.ScaleB(sum, exponent) / 7), BitConverter.DoubleToInt64Bits(actual[i]));
            }
        }
        foreach (var exponent in new[] { 47, 48 })
        {
            var input = Enumerable.Range(0, 103).Select(i => (i % 2 == 0 ? 1 : -1) * (Math.ScaleB(1d, exponent) + 1)).ToArray();
            var output = Enumerable.Repeat(321d, input.Length).ToArray();
            Assert.Equal(exponent == 47, CpuFeasibilityPrototypes.TrySmaVector(input, output, 20));
            if (exponent == 48) Assert.All(output, v => Assert.Equal(321d, v));
            else Assert.All(output, v => Assert.Equal(0d, v));
        }
    }

    [Fact]
    public void AsinCandidatesMatchNativeAcrossParallelPartitionAndUnrollBoundaries()
    {
        var input = Enumerable.Range(0, 2051).Select(i => Math.Sin(i * .12345)).ToArray();
        var native = new double[input.Length]; var actual = new double[input.Length];
        Assert.Equal(TALib.Core.RetCode.Success, TALib.Functions.Asin<double>(input, Range.All, native, out _));
        CpuFeasibilityPrototypes.AsinUnrolled(input, actual);
        Check(native, actual);
        for (var repeat = 0; repeat < 3; repeat++)
        {
            CpuFeasibilityPrototypes.AsinParallel(input, actual);
            Check(native, actual);
        }
    }

    [Fact]
    public void VectorCertificateRejectsAliasingAndInsufficientOutput()
    {
        var input = new[] { 1d, 2, 3, 4, 5, 6 };
        Assert.False(CpuFeasibilityPrototypes.TrySmaVector(input, input, 3));
        Assert.False(CpuFeasibilityPrototypes.TrySmaVector(input, new double[2], 3));
        Assert.Equal(new[] { 1d, 2, 3, 4, 5, 6 }, input);
    }

    [Fact]
    public void AsinCandidatesPreserveEndpointsSignedZeroAndUndefinedInputs()
    {
        var input = new[] { -1d, Math.BitIncrement(-1d), -0d, 0d, double.Epsilon, Math.BitDecrement(1d), 1d,
            Math.BitIncrement(1d), -2d, double.NaN, double.PositiveInfinity, double.NegativeInfinity };
        foreach (var count in Enumerable.Range(0, input.Length + 1))
        {
            var source = input.Take(count).ToArray();
            var output = new double[count]; var native = new double[count];
            // Pinned TA-Lib rejects a singleton range (endIdx must be > 0).
            // Our exact contract still includes zero/one-element inputs.
            if (count > 1)
                Assert.Equal(TALib.Core.RetCode.Success, TALib.Functions.Asin<double>(source, Range.All, native, out _));
            else if (count == 1) native[0] = Math.Asin(source[0]);
            CpuFeasibilityPrototypes.AsinUnrolled(source, output);
            Check(native, output);
            CpuFeasibilityPrototypes.AsinParallel(source, output);
            Check(native, output);
            var presence = Enumerable.Repeat(7d, count).ToArray();
            CpuFeasibilityPrototypes.AsinDefined(source, output, presence);
            for (var i = 0; i < count; i++)
            {
                var defined = source[i] is >= -1 and <= 1;
                Assert.Equal(defined ? 1d : 0d, presence[i]);
                Assert.Equal(BitConverter.DoubleToInt64Bits(defined ? native[i] : 0), BitConverter.DoubleToInt64Bits(output[i]));
            }
        }
    }

    private static void Check(double[] expected, double[] actual)
    {
        for (var i = 0; i < expected.Length; i++)
            if (double.IsNaN(expected[i])) Assert.True(double.IsNaN(actual[i]));
            else Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(actual[i]));
    }
}
