using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class TillsonCoefficientExecutionTests
{
    [Theory]
    [InlineData(TillsonSeed.FirstPrice)]
    [InlineData(TillsonSeed.FollowingPrefix)]
    [InlineData(TillsonSeed.CascadedMeans)]
    public void CachedCoefficientsPreserveExtendedCompositionAndReset(TillsonSeed seed)
    {
        foreach (double factor in new[] { -1d, 0d, .7, double.MaxValue })
        foreach (int shift in new[] { 0, 1100 })
        {
            var state = (IWideAverageState)new TillsonAverage(3, factor, seed, 2).CreateState();
            var values = Enumerable.Range(0, 35).Select(i => ExactVarianceWindow.Units((i * 13 % 31) - 15d) << shift).ToArray();
            var output = new double[2];
            foreach (var input in new[] { values, values.Reverse().ToArray() })
            {
                var prices = input.Select(v => new ReferenceFraction(v) * ReferenceFraction.FromDouble(double.Epsilon)).ToArray();
                var expected = TillsonReference.CalculateExtended(prices, 3, factor, seed, 2);
                state.Reset();
                Assert.Equal(BigInteger.Zero, state.RoundedUnits);
                for (int i = 0; i < input.Length; i++)
                {
                    state.UpdateUnits(input[i], output);
                    Assert.Equal(expected[i].HasValue ? 1d : 0, output[1]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]?.ToDouble() ?? 0),
                        BitConverter.DoubleToInt64Bits(output[0]));
                    if (expected[i].HasValue)
                    {
                        var rounded = new ReferenceFraction(state.RoundedUnits) * ReferenceFraction.FromDouble(double.Epsilon);
                        Assert.Equal(0, rounded.CompareTo(expected[i]!.Value));
                    }
                }
            }
        }
    }
}
