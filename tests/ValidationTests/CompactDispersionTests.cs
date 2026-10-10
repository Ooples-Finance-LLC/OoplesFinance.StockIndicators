using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactDispersionTests
{
    public static IEnumerable<object[]> Configurations =>
        from period in new[] { 1, 2, 7, int.MaxValue }
        from sample in new[] { false, true }
        from scale in new[] { 0d, 1d, -2d, double.Epsilon, double.MaxValue }
        select new object[] { period, sample, scale };

    [Theory, MemberData(nameof(Configurations))]
    public void EveryReadingMatchesIndependentRationalsAcrossGridChangesAndReset(int period, bool sample, double scale)
    {
        var state = new WindowDispersion.State(period, WindowDispersionOutput.StandardDeviation, sample, scale);
        var fixtures = new[]
        {
            new[] { 0d, -0d, 12.5, 13.25, 14d, 13.125, 0, -13.125, 1, 2, 3, 4, 5, 6, 7, 8 },
            new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0, 1, 2, 3, 4, 5, 6, 7, 8 },
            new[] { double.Epsilon, 3 * double.Epsilon, 0, -double.Epsilon, 1e-160, -1e-160, 1e160, -1e160 },
            new[] { 0d, -0d, 0d, -0d, 0d },
            Enumerable.Range(0, 24).Select(i => Math.ScaleB((i % 7 - 3) * 1.23456789012345, i * 83 - 1000)).ToArray()
        };
        foreach (var input in fixtures)
        {
            state.Reset();
            for (var i = 0; i < input.Length; i++)
            {
                var actual = state.Update(new Bar(default, input[i], input[i], input[i], input[i], 1));
                var values = input.Skip(Math.Max(0, i - period + 1)).Take(Math.Min(i + 1, period))
                    .Select(ReferenceFraction.FromDouble).ToArray();
                var n = new ReferenceFraction(values.Length);
                var mean = values.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n;
                AssertBits(mean.ToDouble(), state.Mean);
                var variance = values.Aggregate(new ReferenceFraction(0), (sum, v) => sum + (v - mean) * (v - mean));
                Assert.Equal(variance.Sign != 0, state.HasVariance);
                if (values.Length > 1) variance /= new ReferenceFraction(sample ? values.Length - 1 : values.Length);
                var factor = ReferenceFraction.FromDouble(scale);
                foreach (var output in Enum.GetValues<WindowDispersionOutput>())
                {
                    var expected = 0d;
                    if (values.Length > 1 && factor.Sign != 0)
                    {
                        if (output == WindowDispersionOutput.Variance) expected = (variance * factor).ToDouble();
                        else if (output == WindowDispersionOutput.StandardDeviation)
                            expected = factor.Sign * (variance * factor * factor).SqrtToDouble();
                        else if (variance.Sign != 0)
                        {
                            var deviation = (values[^1] - mean) * factor;
                            expected = deviation.Sign * (deviation * deviation / variance).SqrtToDouble();
                        }
                    }
                    AssertBits(expected, state.Reading(output));
                    if (output == WindowDispersionOutput.StandardDeviation) AssertBits(expected, actual);
                }
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SharedConsumersKeepTheirIndependentFormulaContracts(int consumer)
    {
        IIndicator Create() => consumer switch
        {
            0 => new StandardDeviationWithDetails(3),
            1 => new WindowDeviationBands(3),
            _ => new ClassicDeviationBands(3)
        };
        var report = await IndicatorValidation.ValidateAsync(new(Create().GetType(), "compact-dispersion", Create));
        report.ThrowIfInvalid();
    }

    [Fact]
    public void ScaledRootAndRatioRoundSubnormalMidpointsAndOverflow()
    {
        foreach (var power in new[] { -4300, -2149, -2148, -2147, -1074, -1, 0, 1, 2047, 4090 })
        foreach (var numerator in new[] { 1, 3, 5, 49 })
        foreach (var denominator in new[] { 1, 2, 7 })
        {
            var rational = new ReferenceFraction(numerator) / new ReferenceFraction(denominator);
            var scale = new ReferenceFraction(BigInteger.One << Math.Abs(power));
            rational = power < 0 ? rational / scale : rational * scale;
            AssertBits(rational.ToDouble(), ExactMeanAccumulator.ScaledRatio(numerator, denominator, power));
            AssertBits(rational.SqrtToDouble(), ExactPopulationDeviation.ScaledRootRatio(numerator, denominator, power));
        }
    }

    [Fact]
    public void SmallRootCertificateMatchesIndependentRounding()
    {
        var random = new Random(813);
        for (var i = 0; i < 512; i++)
        {
            var numerator = (ulong)random.NextInt64(1, 1L << 52);
            var denominator = (uint)random.Next(1, 4097);
            var power = random.Next(-900, 901);
            Assert.True(ExactPopulationDeviation.TrySmallScaledRoot(numerator, denominator, power, out var actual));
            var rational = new ReferenceFraction(new BigInteger(numerator)) / new ReferenceFraction(denominator);
            var scale = new ReferenceFraction(BigInteger.One << Math.Abs(power));
            rational = power < 0 ? rational / scale : rational * scale;
            AssertBits(rational.SqrtToDouble(), actual);
        }
        foreach (var numerator in new ulong[] { 1, 4, 9, 16, 49, 64, 256, 4096, 1UL << 52, 1UL << 53 })
        foreach (var denominator in new uint[] { 1, 2, 4, 7, 64, 4096 })
        {
            Assert.True(ExactPopulationDeviation.TrySmallScaledRoot(numerator, denominator, 0, out var actual));
            AssertBits((new ReferenceFraction(new BigInteger(numerator)) / new ReferenceFraction(denominator)).SqrtToDouble(), actual);
        }
        Assert.False(ExactPopulationDeviation.TrySmallScaledRoot(1, 1, -2148, out _));
        Assert.False(ExactPopulationDeviation.TrySmallScaledRoot(1, 1, 4090, out _));
        Assert.False(ExactPopulationDeviation.TrySmallScaledRoot(1UL << 53, 1, 1, out _));
        Assert.False(ExactPopulationDeviation.TrySmallScaledRoot(1, 4097, 0, out _));
    }

    private static void AssertBits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
