namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

public sealed class RsiTests : GlobalTestData
{
    [Fact]
    public void CalculateRelativeStrengthIndex_CustomLengthMatchesNaive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        const int length = 14;

        // Act
        var results = stockData.CalculateRelativeStrengthIndex(MovingAvgType.WildersSmoothingMethod, length, signalLength: 3).CustomValuesList;
        var expected = CalculateRsiNaive(stockData.ClosePrices, length);

        // Assert
        RoundList(results).Should().BeEquivalentTo(RoundList(expected));
    }

    private static List<double> CalculateRsiNaive(IReadOnlyList<double> input, int length)
    {
        var gains = new double[input.Count];
        var losses = new double[input.Count];

        for (var i = 0; i < input.Count; i++)
        {
            var currentValue = input[i];
            var prevValue = i >= 1 ? input[i - 1] : 0;
            var priceChg = i >= 1 ? currentValue - prevValue : 0;

            if (priceChg < 0)
            {
                losses[i] = Math.Abs(priceChg);
            }
            else if (priceChg > 0)
            {
                gains[i] = priceChg;
            }
        }

        var avgGains = CalculateWellesWilderAverage(gains, length);
        var avgLosses = CalculateWellesWilderAverage(losses, length);
        var rsi = new List<double>(input.Count);

        for (var i = 0; i < input.Count; i++)
        {
            var avgGain = avgGains[i];
            var avgLoss = avgLosses[i];
            var rs = avgLoss != 0 ? avgGain / avgLoss : 0;
            var value = avgLoss == 0 ? 100 : avgGain == 0 ? 0 : Math.Min(Math.Max(100 - (100 / (1 + rs)), 0), 100);
            rsi.Add(value);
        }

        return rsi;
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
