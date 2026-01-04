namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

public sealed class WilderTests : GlobalTestData
{
    [Fact]
    public void CalculateAverageTrueRange_CustomLengthMatchesNaive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        const int length = 14;

        // Act
        var results = stockData.CalculateAverageTrueRange(MovingAvgType.WildersSmoothingMethod, length).CustomValuesList;
        var expected = CalculateAtrNaive(stockData, length);

        // Assert
        RoundList(results).Should().BeEquivalentTo(RoundList(expected));
    }

    private static List<double> CalculateAtrNaive(StockData stockData, int length)
    {
        var count = stockData.ClosePrices.Count;
        var trValues = new double[count];

        for (var i = 0; i < count; i++)
        {
            var currentHigh = stockData.HighPrices[i];
            var currentLow = stockData.LowPrices[i];
            var prevClose = i >= 1 ? stockData.ClosePrices[i - 1] : 0;
            var range1 = currentHigh - currentLow;
            var range2 = Math.Abs(currentHigh - prevClose);
            var range3 = Math.Abs(currentLow - prevClose);
            trValues[i] = Math.Max(range1, Math.Max(range2, range3));
        }

        var atrValues = CalculateWellesWilderAverage(trValues, length);
        return new List<double>(atrValues);
    }

    private static double[] CalculateWellesWilderAverage(IReadOnlyList<double> input, int length)
    {
        var output = new double[input.Count];
        var k = (double)1 / length;
        double prev = 0;

        for (var i = 0; i < input.Count; i++)
        {
            var wwma = (input[i] * k) + (prev * (1 - k));
            output[i] = wwma;
            prev = wwma;
        }

        return output;
    }

    private static List<double> RoundList(IEnumerable<double> values, int decimals = 10)
    {
        return values.Select(value => Math.Round(value, decimals)).ToList();
    }
}
