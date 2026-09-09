namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

public sealed class MovingAverageTests : GlobalTestData
{
    [Fact]
    public void CalculateSimpleMovingAverage_ReturnsProperValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        var expectedResults = GetCsvData<double>("MovingAverage/Sma");

        // Act
        var results = stockData.CalculateSimpleMovingAverage().CustomValuesList;
        var roundedResults = results.Select(x => Math.Round(x, 4)).ToList();

        // Assert
        results.Should().NotBeNullOrEmpty();
        roundedResults.Should().BeEquivalentTo(expectedResults);
    }

    [Fact]
    public void CalculateExponentialMovingAverage_RoundsOutputs_WhenRequested()
    {
        // Arrange
        var stockData = new StockData(StockTestData)
        {
            Options = new IndicatorOptions
            {
                RoundingDigits = 2
            }
        };

        // Act
        var results = stockData.CalculateExponentialMovingAverage(5);

        // Assert
        foreach (var value in results.CustomValuesList)
        {
            value.Should().Be(Math.Round(value, 2));
        }

        results.OutputValues.Should().ContainKey("Ema");
        foreach (var value in results.OutputValues["Ema"])
        {
            value.Should().Be(Math.Round(value, 2));
        }
    }

    [Fact]
    public void CalculateSimpleMovingAverage_CustomLengthMatchesNaive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        const int length = 5;

        // Act
        var results = stockData.CalculateSimpleMovingAverage(length).CustomValuesList;
        var input = stockData.ClosePrices;
        var expected = CalculateSimpleMovingAverageNaive(input, length);

        // Assert
        RoundList(results).Should().BeEquivalentTo(RoundList(expected));
    }

    [Fact]
    public void CalculateWeightedMovingAverage_CustomLengthMatchesNaive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        const int length = 5;
        var input = stockData.ClosePrices;

        // Act
        var results = stockData.CalculateWeightedMovingAverage(length).CustomValuesList;
        var expected = CalculateWeightedMovingAverageNaive(input, length);

        // Assert
        RoundList(results).Should().BeEquivalentTo(RoundList(expected));
    }

    [Fact]
    public void CalculateExponentialMovingAverage_CustomLengthMatchesNaive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        const int length = 5;
        var input = stockData.ClosePrices;
        var k = Math.Min(Math.Max((double)2 / (length + 1), 0.01), 0.99);

        // Act
        var results = stockData.CalculateExponentialMovingAverage(length).CustomValuesList;
        var expected = new List<double>(input.Count);
        double sum = 0;
        double prevEma = 0;

        for (var i = 0; i < input.Count; i++)
        {
            var currentValue = input[i];
            if (i < length)
            {
                sum += currentValue;
                var ema = sum / (i + 1);
                expected.Add(ema);
                prevEma = ema;
            }
            else
            {
                var ema = (currentValue * k) + (prevEma * (1 - k));
                expected.Add(ema);
                prevEma = ema;
            }
        }

        // Assert
        RoundList(results).Should().BeEquivalentTo(RoundList(expected));
    }

    [Fact]
    public void CalculateTriangularMovingAverage_CustomLengthMatchesNaive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        const int length = 5;
        var input = stockData.ClosePrices;

        // Act
        var results = stockData.CalculateTriangularMovingAverage(MovingAvgType.SimpleMovingAverage, length).CustomValuesList;
        var sma1 = CalculateSimpleMovingAverageNaive(input, length);
        var expected = CalculateSimpleMovingAverageNaive(sma1, length);

        // Assert
        RoundList(results).Should().BeEquivalentTo(RoundList(expected));
    }

    [Fact]
    public void CalculateHullMovingAverage_CustomLengthMatchesNaive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        const int length = 10;
        var input = stockData.ClosePrices;
        var length2 = OoplesFinance.StockIndicators.Helpers.MathHelper.MinOrMax((int)Math.Round((double)length / 2));
        var sqrtLength = OoplesFinance.StockIndicators.Helpers.MathHelper.MinOrMax((int)Math.Round(Math.Sqrt(length)));

        // Act
        var results = stockData.CalculateHullMovingAverage(MovingAvgType.WeightedMovingAverage, length).CustomValuesList;
        var wma1 = CalculateWeightedMovingAverageNaive(input, length);
        var wma2 = CalculateWeightedMovingAverageNaive(input, length2);
        var total = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++)
        {
            total.Add((2 * wma2[i]) - wma1[i]);
        }
        var expected = CalculateWeightedMovingAverageNaive(total, sqrtLength);

        // Assert
        RoundList(results).Should().BeEquivalentTo(RoundList(expected));
    }

    private static List<double> CalculateSimpleMovingAverageNaive(IReadOnlyList<double> input, int length)
    {
        var expected = new List<double>(input.Count);
        for (var i = 0; i < input.Count; i++)
        {
            if (i >= length - 1)
            {
                double sum = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    sum += input[j];
                }
                expected.Add(sum / length);
            }
            else
            {
                expected.Add(0d);
            }
        }

        return expected;
    }

    private static List<double> CalculateWeightedMovingAverageNaive(IReadOnlyList<double> input, int length)
    {
        var denominator = (double)length * (length + 1) / 2;
        var expected = new List<double>(input.Count);

        for (var i = 0; i < input.Count; i++)
        {
            double weightedSum = 0;
            for (var w = 1; w <= length; w++)
            {
                var idx = i - (length - w);
                if (idx >= 0)
                {
                    weightedSum += w * input[idx];
                }
            }

            expected.Add(weightedSum / denominator);
        }

        return expected;
    }

    private static List<double> RoundList(IEnumerable<double> values, int decimals = 8)
    {
        return values.Select(value => Math.Round(value, decimals)).ToList();
    }
}
