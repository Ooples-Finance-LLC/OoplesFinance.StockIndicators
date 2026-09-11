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

    /// <summary>
    /// Bollinger's definition, written out: the middle band is the SMA of the last <paramref name="length"/>
    /// prices, and the band width is the population standard deviation of those same prices around that
    /// same mean.
    /// </summary>
    /// <remarks>
    /// This reference used to take the standard deviation of the MIDDLE BAND around an average of the middle
    /// band. That is not Bollinger Bands; it is what the batch computed while GetMovingAverageList left the
    /// middle band on CustomValuesList for the standard deviation to read, and a reference copied from the
    /// implementation can only ever agree with it. At bar 200 of this fixture it made the bands 55% too wide.
    /// </remarks>
    private static (List<double> UpperBand, List<double> MiddleBand, List<double> LowerBand) CalculateBollingerBandsNaive(
        IReadOnlyList<double> input,
        int length,
        double stdDevMult)
    {
        var middleBand = CalculateSimpleMovingAverageNaive(input, length);
        var stdDev = new List<double>(input.Count);

        for (var i = 0; i < input.Count; i++)
        {
            if (i < length - 1)
            {
                stdDev.Add(0d);
                continue;
            }

            var window = input.Skip(i - length + 1).Take(length).ToList();
            var mean = window.Average();
            stdDev.Add(Math.Sqrt(window.Sum(value => (value - mean) * (value - mean)) / length));
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
