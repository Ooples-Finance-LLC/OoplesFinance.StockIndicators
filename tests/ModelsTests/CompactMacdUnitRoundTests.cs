using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactMacdUnitRoundTests
{
    [Fact]
    public void UnitDivisorRetainsExactMidpointsCarriesSubnormalsAndUnpublishedOverflow()
    {
        foreach (int width in new[] { 1, 52, 53, 54, 63, 64, 105, 106, 107, 126, 127, 128 })
        foreach (int scale in new[] { 0, 1, 52, 970, 1022, 1074, 2044, 2045, 2200 })
        foreach (int sign in new[] { -1, 1 })
        foreach (int offset in new[] { -3, -2, -1, 0, 1, 2, 3 })
        {
            BigInteger units = sign * (((BigInteger.One << width) + offset) << scale);
            var input = CompactMacdValue.FromUnits(units);
            Assert.Equal(RocBankValue.RoundUnits(units, 1), input.Round(1).Units);
        }
        Assert.Equal(BigInteger.Zero, default(CompactMacdValue).Round(1).Units);
        BigInteger half = BigInteger.One << 126;
        var minimum = CompactMacdValue.FromUnits(-half + 1)
            + CompactMacdValue.FromUnits(-half - 1);
        Assert.Equal(RocBankValue.RoundUnits(-(BigInteger.One << 127), 1), minimum.Round(1).Units);
    }

    [Fact]
    public void RoundedOscillatorAndHistogramDifferencesMatchIndependentWideUnits()
    {
        var random = new Random(281028);
        foreach (int exponent in new[] { -1074, -1022, -450, 0, 450, 1020 })
        {
            BigInteger signalUnits = 0;
            var signal = default(CompactMacdValue);
            for (int i = 0; i < 257; i++)
            {
                var fast = CompactMacdValue.Of(Math.ScaleB(random.NextDouble() * 2 - 1, exponent));
                var slow = CompactMacdValue.Of(Math.ScaleB(random.NextDouble() * 2 - 1, exponent));
                BigInteger oscillatorUnits = RocBankValue.RoundUnits(fast.Units - slow.Units, 1);
                var oscillator = (fast - slow).Round(1);
                Assert.Equal(oscillatorUnits, oscillator.Units);
                signalUnits = RocBankValue.RoundUnits(signalUnits * 8 + oscillatorUnits * 2, 10);
                signal = signal.AdvanceEma(oscillator, 9);
                Assert.Equal(signalUnits, signal.Units);
                Assert.Equal(RocBankValue.RoundUnits(oscillatorUnits - signalUnits, 1),
                    (oscillator - signal).Round(1).Units);
            }
        }
    }
}
