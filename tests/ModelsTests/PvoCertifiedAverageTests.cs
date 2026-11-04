using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PvoCertifiedAverageTests
{
    [Fact]
    public void AcceptedCellsMatchIndependentIntegerRoundingAtBinadeBoundaries()
    {
        int accepted = 0;
        foreach (int exponent in new[] { -1074, -1022, -503, -450, -449, -1, 0, 1, 400, 449, 450, 451, 1023 })
        foreach (int sign in new[] { -1, 1 })
        {
            double prior = sign * Math.ScaleB(1d, exponent);
            foreach (double input in new[] { 0d, -0d, prior, -prior, Math.BitDecrement(prior), Math.BitIncrement(prior) })
            foreach (int period in new[] { 1, 2, 3, 9, 12, 26, 2047, int.MaxValue })
            {
                if (!EmaDifferenceSignal.VolumeAverage.TryBlend(prior, input, period, out var result)) continue;
                var reference = new EmaDifferenceSignal.Average(period, true);
                reference.Add(JurikDyadic.FromDouble(prior).RoundedUnits());
                var expected = reference.Add(JurikDyadic.FromDouble(input).RoundedUnits());
                Assert.Equal(expected!.Value, CompactMacdValue.Of(result).Units);
                accepted++;
            }
        }
        Assert.True(accepted > 0);
    }

    [Fact]
    public void BenchmarkScaleIntegerVolumesExerciseBothCertificateAndFallback()
    {
        foreach (int period in new[] { 12, 26 })
        {
            var actual = new EmaDifferenceSignal.VolumeAverage(period, false);
            var reference = new EmaDifferenceSignal.Average(period, false);
            var random = new Random(42);
            double? previous = null;
            int accepted = 0, fallback = 0;
            for (int i = 0; i < 1000; i++)
            {
                // Match the four price draws preceding each benchmark volume.
                for (int draw = 0; draw < 4; draw++) random.NextDouble();
                double input = random.Next(1000, 500000);
                if (previous.HasValue)
                {
                    if (EmaDifferenceSignal.VolumeAverage.TryBlend(previous.Value, input, period, out _)) accepted++;
                    else fallback++;
                }
                var expected = reference.Add(JurikDyadic.FromDouble(input).RoundedUnits());
                var result = actual.Add(input);
                Assert.Equal(expected.HasValue, result.HasValue);
                if (expected.HasValue)
                {
                    Assert.Equal(expected.Value, result!.Value.Units);
                    previous = result.Value.Publish();
                }
            }
            Assert.True(accepted > 0);
            Assert.True(fallback > 0);
        }
    }

    [Fact]
    public void AcceptedMixedSignAndScaleCellsMatchIntegerReference()
    {
        var random = new Random(32817);
        int accepted = 0;
        int[] periods = [1, 2, 3, 9, 12, 26, 2047, int.MaxValue];
        for (int i = 0; i < 5000; i++)
        {
            double prior = Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-500, 501));
            double input = Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-500, 501));
            int period = periods[i % periods.Length];
            if (!EmaDifferenceSignal.VolumeAverage.TryBlend(prior, input, period, out var result)) continue;
            var reference = new EmaDifferenceSignal.Average(period, true);
            reference.Add(JurikDyadic.FromDouble(prior).RoundedUnits());
            Assert.Equal(reference.Add(JurikDyadic.FromDouble(input).RoundedUnits())!.Value,
                CompactMacdValue.Of(result).Units);
            accepted++;
        }
        Assert.True(accepted > 0);
    }

    [Fact]
    public void MidpointTiesCancellationAndUnsafeProductsUseExactFallback()
    {
        foreach (int exponent in new[] { -449, -1, 0, 1, 400, 449 })
        foreach (int sign in new[] { -1, 1 })
        {
            double prior = sign * Math.ScaleB(1d, exponent);
            Assert.False(EmaDifferenceSignal.VolumeAverage.TryBlend(prior, Math.BitIncrement(prior), 3, out _));
            Assert.False(EmaDifferenceSignal.VolumeAverage.TryBlend(prior, -prior, 3, out _));
        }
        foreach (double value in new[] { double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue })
            Assert.False(EmaDifferenceSignal.VolumeAverage.TryBlend(value, value, 26, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeedingFallbackTransitionsAndResetMatchIntegerReference(bool first)
    {
        foreach (int period in new[] { 1, 2, 3, 12, 26, 2047, int.MaxValue })
        {
            var actual = new EmaDifferenceSignal.VolumeAverage(period, first);
            var reference = new EmaDifferenceSignal.Average(period, first);
            for (int replay = 0; replay < 2; replay++)
            {
                actual.Reset(); reference.Reset();
                var random = new Random(81729);
                int length = period == int.MaxValue ? 128 : period + 128;
                int ordinary = period == int.MaxValue ? 64 : period + 64;
                for (int i = 0; i < length; i++)
                {
                    double input = i < ordinary ? (random.NextDouble() * 2 - 1) * 1e6 : (i % 17) switch
                    {
                        0 => double.Epsilon,
                        1 => -double.Epsilon,
                        2 => double.MaxValue,
                        3 => -double.MaxValue,
                        4 => 0d,
                        5 => -0d,
                        _ => (random.NextDouble() * 2 - 1) * 1e6,
                    };
                    var expected = reference.Add(JurikDyadic.FromDouble(input).RoundedUnits());
                    var result = actual.Add(input);
                    Assert.Equal(expected.HasValue, result.HasValue);
                    if (expected.HasValue) Assert.Equal(expected.Value, result!.Value.Units);
                }
            }
        }
    }
}
