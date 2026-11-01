using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TillsonCertifiedSnapshotTests
{
    [Theory]
    [InlineData(TillsonSeed.FirstPrice, 1)]
    [InlineData(TillsonSeed.FirstPrice, 3)]
    [InlineData(TillsonSeed.FollowingPrefix, 1)]
    [InlineData(TillsonSeed.FollowingPrefix, 3)]
    [InlineData(TillsonSeed.CascadedMeans, 1)]
    [InlineData(TillsonSeed.CascadedMeans, 3)]
    public void CertifiedAndWideTransitionsPreserveBitsRoundedUnitsAndReset(TillsonSeed seed, int period)
    {
        BigInteger grid = BigInteger.One << 1074;
        var inputs = Enumerable.Range(0, 192).Select(i => i == 0 ? grid + 1
            : i is >= 48 and < 72 ? (BigInteger.One << 2110) + i
            : ExactVarianceWindow.Units(100 + (i % 17) * .125)).ToArray();
        var prices = inputs.Select(n => new ReferenceFraction(n) / new ReferenceFraction(grid)).ToArray();
        foreach (double factor in new[] { .25, .7, -1, 2, Math.BitIncrement(2) })
        {
            var expected = TillsonCompactSnapshotTests.Reference(prices, period, factor, seed, 0);
            var expectedRounded = TillsonReference.CalculateExtended(prices, period, factor, seed, 0);
            var state = (IWideAverageState)new TillsonAverage(period, factor, seed).CreateState();
            var output = new double[2];
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                Assert.Equal(BigInteger.Zero, state.RoundedUnits);
                for (int i = 0; i < inputs.Length; i++)
                {
                    state.UpdateUnits(inputs[i], output);
                    Assert.Equal(expected[i].HasValue ? 1d : 0d, output[1]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0),
                        BitConverter.DoubleToInt64Bits(output[0]));
                    BigInteger roundedUnits = 0;
                    if (expectedRounded[i] is { } rounded)
                    {
                        var (numerator, denominator) = rounded.Components;
                        roundedUnits = BigInteger.DivRem(numerator << 1074, denominator, out var remainder);
                        Assert.Equal(BigInteger.Zero, remainder);
                    }
                    Assert.Equal(roundedUnits, state.RoundedUnits);
                }
            }
        }
    }
}
