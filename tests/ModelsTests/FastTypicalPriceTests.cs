using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators.Fast;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// The fast typical price must be the same double as the exact route's, bit for bit: indicators compare
/// typical prices for equality and order, so a last-bit difference moves a whole bar.
/// </summary>
public sealed class FastTypicalPriceTests
{
    [Theory]
    [InlineData(100.01, 99.37, 99.99)]
    [InlineData(0.1, 0.2, 0.3)]
    [InlineData(1, 2, 4)]
    [InlineData(-5.5, 3.25, 2.25)]
    [InlineData(0, 0, 0)]
    [InlineData(1e300, 1e300, 1e300)]
    [InlineData(5e-324, 5e-324, 1e-320)]
    [InlineData(1, 1e-17, -1)]
    [InlineData(127.99, 128.01, 64.5)]
    public void MatchesTheExactRouteOnChosenBars(double high, double low, double close) =>
        AssertSame(high, low, close);

    [Fact]
    public void MatchesTheExactRouteOnPricesWithTwoDecimals()
    {
        var random = RandomHelper.CreateSeededRandom(2287);
        for (var i = 0; i < 200_000; i++)
        {
            var close = Math.Round(1 + random.NextDouble() * 500, 2);
            AssertSame(Math.Round(close + random.NextDouble() * 3, 2), Math.Round(close - random.NextDouble() * 3, 2), close);
        }
    }

    [Fact]
    public void MatchesTheExactRouteAcrossMagnitudesAndSigns()
    {
        var random = RandomHelper.CreateSeededRandom(9131);
        for (var i = 0; i < 200_000; i++)
            AssertSame(Draw(random), Draw(random), Draw(random));
    }

    [Fact]
    public void MatchesTheExactRouteNearRoundingTies()
    {
        // Three times an odd multiple of half a unit, split unevenly across three doubles, puts the mean
        // exactly between two doubles or one residual unit to either side of that.
        var random = RandomHelper.CreateSeededRandom(4409);
        for (var i = 0; i < 100_000; i++)
        {
            var mean = 64 + random.NextDouble() * 64;
            var unit = Math.BitIncrement(mean) - mean;
            var middle = mean + unit / 2;
            var offset = (random.Next(-2, 3)) * unit / 8;
            var high = Math.Round(random.NextDouble() * 60, 3);
            var low = random.NextDouble() * 30;
            var close = 3 * middle - high - low + offset;
            AssertSame(high, low, close);
        }
    }

    private static double Draw(Random random)
    {
        var magnitude = Math.Pow(2, random.Next(-40, 41)) * (1 + random.NextDouble());
        return random.Next(4) == 0 ? -magnitude : magnitude;
    }

    private static void AssertSame(double high, double low, double close) =>
        Assert.Equal(
            BitConverter.DoubleToInt64Bits(ExactTypicalPrice.Calculate(high, low, close)),
            BitConverter.DoubleToInt64Bits(FastMath.TypicalPrice(high, low, close)));
}
