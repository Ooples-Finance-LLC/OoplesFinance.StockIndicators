using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class KlingerPrivateBlendTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(13)]
    [InlineData(34)]
    [InlineData(55)]
    [InlineData(2047)]
    [InlineData(2048)]
    public void OrdinarySubnormalAndExtendedStagesMatchExactReferenceAfterReset(int period)
    {
        var actual = new KlingerVolumeWithSignal.CertifiedAverage(period);
        var expected = new EmaDifferenceSignal.Average(period, false);
        foreach (bool extended in new[] { false, true })
        for (int replay = 0; replay < 2; replay++)
        {
            actual.Reset(); expected.Reset();
            var random = new Random(91037);
            for (int i = 0; i < period + 128; i++)
            {
                var stage = JurikDyadic.FromDouble(i % 11 == 0 ? double.Epsilon :
                    (random.NextDouble() * 2 - 1) * 1e6);
                if (extended && i % 17 == 0) stage.ScaleByPowerOfTwo(1050);
                var input = CompactMacdValue.FromRounded(stage);
                var reference = expected.Add(stage.RoundedUnits());
                var result = actual.Add(input);
                Assert.Equal(reference.HasValue, result.HasValue);
                if (reference.HasValue) Assert.Equal(reference.Value, result!.Value.Units);
            }
        }
    }

    [Fact]
    public void OverflowedCachedDoubleDoesNotReplaceTheExtendedStage()
    {
        var positive = JurikDyadic.FromDouble(double.MaxValue);
        positive.ScaleByPowerOfTwo(64);
        var negative = positive; negative.Multiply(-1);
        var actual = new KlingerVolumeWithSignal.CertifiedAverage(3);
        var expected = new EmaDifferenceSignal.Average(3, false);
        var stages = new[] { positive, positive, positive, negative,
            JurikDyadic.FromDouble(double.Epsilon), JurikDyadic.FromDouble(1) };
        for (int replay = 0; replay < 2; replay++)
        {
            actual.Reset(); expected.Reset();
            for (int i = 0; i < stages.Length; i++)
            {
                var reference = expected.Add(stages[i].RoundedUnits());
                var result = actual.Add(CompactMacdValue.FromRounded(stages[i]));
                Assert.Equal(reference.HasValue, result.HasValue);
                if (reference.HasValue) Assert.Equal(reference.Value, result!.Value.Units);
                if (i == 2) Assert.Equal(double.PositiveInfinity, result!.Value.Publish());
                if (i == 3) Assert.Equal(0d, result!.Value.Publish());
                if (i == 5) Assert.Equal(.5, result!.Value.Publish());
            }
        }
    }
}
