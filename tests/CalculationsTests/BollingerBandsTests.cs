namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

public sealed class BollingerBandsTests : GlobalTestData
{
    [Fact]
    public void CalculateBollingerBands_CustomLengthMatchesNaive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        const int length = 20;
        const double stdDevMult = 2;

        // Act
        var results = stockData.CalculateBollingerBands(MovingAvgType.SimpleMovingAverage, length, stdDevMult);
        var expected = CalculateBollingerBandsNaive(stockData.ClosePrices, length, stdDevMult);

        // Assert
        RoundList(results.OutputValues["UpperBand"]).Should().BeEquivalentTo(RoundList(expected.UpperBand));
        RoundList(results.OutputValues["MiddleBand"]).Should().BeEquivalentTo(RoundList(expected.MiddleBand));
        RoundList(results.OutputValues["LowerBand"]).Should().BeEquivalentTo(RoundList(expected.LowerBand));
    }

    private static (List<double> UpperBand, List<double> MiddleBand, List<double> LowerBand) CalculateBollingerBandsNaive(
        IReadOnlyList<double> input,
        int length,
        double stdDevMult)
    {
        var middleBand = CalculateSimpleMovingAverageNaive(input, length);
        var stdDevMean = CalculateSimpleMovingAverageNaive(middleBand, length);
        var variance = CalculateSimpleMovingAverageNaive(CalculateDeviationSquared(middleBand, stdDevMean), length);
        var stdDev = new List<double>(variance.Count);

        for (var i = 0; i < variance.Count; i++)
        {
            stdDev.Add(Math.Sqrt(variance[i]));
        }

        var upperBand = new List<double>(input.Count);
        var lowerBand = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++)
        {
            upperBand.Add(middleBand[i] + (stdDev[i] * stdDevMult));
            lowerBand.Add(middleBand[i] - (stdDev[i] * stdDevMult));
        }

        return (upperBand, middleBand, lowerBand);
    }

    private static List<double> CalculateDeviationSquared(IReadOnlyList<double> input, IReadOnlyList<double> mean)
    {
        var output = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++)
        {
            var deviation = input[i] - mean[i];
            output.Add(deviation * deviation);
        }

        return output;
    }

    private static List<double> CalculateSimpleMovingAverageNaive(IReadOnlyList<double> input, int length)
    {
        var output = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++)
        {
            if (i >= length - 1)
            {
                double sum = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    sum += input[j];
                }
                output.Add(sum / length);
            }
            else
            {
                output.Add(0d);
            }
        }

        return output;
    }

    private static List<double> RoundList(IEnumerable<double> values, int decimals = 8)
    {
        return values.Select(value => Math.Round(value, decimals)).ToList();
    }
}
