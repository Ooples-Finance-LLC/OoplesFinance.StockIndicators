using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CommodityIndexWindowTests
{
    public static IEnumerable<object[]> Cases => new[] { 1, 3, 20 }.SelectMany(length => new[] {
        MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }
        .Select(kind => new object[] { length, kind }));

    [Theory, MemberData(nameof(Cases))]
    public void MatchesIndependentMeanDeviationDefinitionsWithPreviewResetAndWideResiduals(int length, MovingAvgType kind)
    {
        var prices = Enumerable.Repeat(-double.MaxValue, 24).Concat(new[] { double.MaxValue, 0d, double.Epsilon, -double.Epsilon, 1d, -2, 3, 3, 8, 2, 7, 1, 9 }).ToArray();
        var referenceKind = kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
        foreach (var constant in new[] { .015, -.015 })
        {
            var expected = BuiltInFormulaReferences.CommodityValues(prices, length, referenceKind, constant);
            using var state = new CommodityIndexWindow(kind, length, constant);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < prices.Length; i++)
                {
                    Assert.Equal(expected[i], state.Next(prices[i], false));
                    Assert.Equal(expected[i], state.Next(prices[i], false));
                    Assert.Equal(expected[i], state.Next(prices[i], true));
                }
            }
        }
    }

    [Fact]
    public void SubnormalWindowRetainsNonzeroClassicalDeviation()
    {
        using var state = new CommodityIndexWindow(MovingAvgType.SimpleMovingAverage, 2, .015);
        Assert.Equal(0, state.Next(0, true));
        Assert.Equal(66.66666666666667, state.Next(double.Epsilon, true));
        foreach (var constant in new[] { 0d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new CommodityIndexWindow(MovingAvgType.SimpleMovingAverage, 2, constant));
    }
}
