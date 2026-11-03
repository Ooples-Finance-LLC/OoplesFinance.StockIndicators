using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactKlingerSchaffEmaTests
{
    [Fact]
    public void SeededAveragesMatchIntegerReferenceAcrossOrdinaryAndExtendedStages()
    {
        foreach (int period in new[] { 1, 2, 13, 34, 55, 2047, 2048 })
        {
            var actual = new EmaDifferenceSignal.CompactAverage(period, false);
            var expected = new EmaDifferenceSignal.Average(period, false);
            var random = new Random(77123);
            for (int replay = 0; replay < 2; replay++)
            {
                actual.Reset(); expected.Reset();
                for (int i = 0; i < period + 96; i++)
                {
                    var stage = JurikDyadic.FromDouble(i % 11 == 0 ? double.Epsilon :
                        (random.NextDouble() * 2 - 1) * 1e6);
                    if (i % 17 == 0) stage.ScaleByPowerOfTwo(1050);
                    var input = CompactMacdValue.FromRounded(stage);
                    Assert.Equal(stage.RoundedUnits(), input.Units);
                    Assert.Equal(input.Units, input.ToRoundedDyadic().RoundedUnits());
                    var reference = expected.Add(stage.RoundedUnits());
                    var result = actual.Add(input);
                    Assert.Equal(reference.HasValue, result.HasValue);
                    if (reference.HasValue) Assert.Equal(reference.Value, result!.Value.Units);
                }
            }
        }
    }

    [Fact]
    public void ExtendedOppositeStagesRecoverExactZeroAndOrdinaryResiduals()
    {
        var positive = JurikDyadic.FromDouble(double.MaxValue);
        positive.ScaleByPowerOfTwo(1024);
        var negative = positive; negative.Multiply(-1);
        var actual = new EmaDifferenceSignal.CompactAverage(2, false);
        var expected = new EmaDifferenceSignal.Average(2, false);
        foreach (var stage in new[] { positive, negative, JurikDyadic.FromDouble(double.Epsilon),
                     positive, negative, JurikDyadic.FromDouble(-double.Epsilon) })
        {
            var reference = expected.Add(stage.RoundedUnits());
            var result = actual.Add(CompactMacdValue.FromRounded(stage));
            Assert.Equal(reference.HasValue, result.HasValue);
            if (reference.HasValue) Assert.Equal(reference.Value, result!.Value.Units);
        }
    }
}
