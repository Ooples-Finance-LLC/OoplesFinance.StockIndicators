using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class GeometricProductBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RandomBinaryFactorsMatchIndependentRationalRoots(bool positiveOnly)
    {
        var random = new Random(24439);
        var prices = new List<double> { double.Epsilon, double.MaxValue, 0, -1,
            BitConverter.Int64BitsToDouble(0x000fffffffffffff),
            BitConverter.Int64BitsToDouble(0x0010000000000000),
            BitConverter.Int64BitsToDouble(0x0010000000000001) };
        var bytes = new byte[8];
        for (var i = 0; i < 80; i++)
        {
            random.NextBytes(bytes);
            var fraction = BitConverter.ToInt64(bytes, 0) & 0x000fffffffffffffL;
            var exponent = new[] { 0, 1, 2, 511, 1022, 1023, 1535, 2045, 2046 }[i % 9];
            prices.Add(BitConverter.Int64BitsToDouble(((long)exponent << 52) | fraction));
        }
        var bars = prices.Select(p => new Bar(default, p, p, p, p, 0)).ToArray();
        foreach (var period in new[] { 1, 2, 3, 7, 14, 31 })
        {
            var expected = BuiltInFormulaReferences.RoundedGeometricMean(bars, period, positiveOnly);
            using var state = new RollingGeometricMean(period, positiveOnly);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < prices.Count; i++)
                {
                    // A discarded alternative preview must not change factors or root degree.
                    state.Next(i % 2 == 0 ? double.MaxValue : 0, false);
                    Assert.Equal(expected[i], state.Next(prices[i], false));
                    Assert.Equal(expected[i], state.Next(prices[i], true));
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedNonfiniteFactorsPreserveTheWindow(bool positiveOnly)
    {
        using var state = new RollingGeometricMean(3, positiveOnly);
        state.Next(1, true);
        state.Next(8, true);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var commit in new[] { false, true })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Next(invalid, commit));
            Assert.Equal(6d, state.Next(27, false));
        }
        Assert.Equal(6d, state.Next(27, true));
        Assert.Equal(24d, state.Next(64, true));
    }
}
