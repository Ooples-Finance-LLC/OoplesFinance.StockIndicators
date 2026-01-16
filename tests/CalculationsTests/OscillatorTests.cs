namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

public sealed class OscillatorTests : GlobalTestData
{
    [Fact]
    public void CalculateUlcerIndex_CustomLengthMatchesNaive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        const int length = 14;

        // Act
        var results = stockData.CalculateUlcerIndex(length).CustomValuesList;
        var expected = CalculateUlcerIndexNaive(stockData.ClosePrices, length);

        // Assert
        RoundList(results).Should().BeEquivalentTo(RoundList(expected));
    }

    [Fact]
    public void CalculateVaradiOscillator_DefaultMatchesNaive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        const int length = 14;

        // Act
        var results = stockData.CalculateVaradiOscillator(length: length).CustomValuesList;
        var expected = CalculateVaradiOscillatorNaive(stockData, length);

        // Assert
        RoundList(results).Should().BeEquivalentTo(RoundList(expected));        
    }

    [Fact]
    public void CalculateSpearmanIndicator_DefaultMatchesNaive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        const int length = 10;

        // Act
        var results = stockData.CalculateSpearmanIndicator(length: length).CustomValuesList;
        var expected = CalculateSpearmanIndicatorNaive(stockData.ClosePrices, length);

        // Assert
        RoundList(results).Should().BeEquivalentTo(RoundList(expected));
    }

    private static List<double> CalculateUlcerIndexNaive(IReadOnlyList<double> input, int length)
    {
        var output = new List<double>(input.Count);
        var drawdownSquared = new List<double>(input.Count);

        for (var i = 0; i < input.Count; i++)
        {
            var start = Math.Max(0, i - length + 1);
            double maxValue = 0;
            for (var j = start; j <= i; j++)
            {
                maxValue = Math.Max(maxValue, input[j]);
            }

            var pctDrawdownSquared = maxValue != 0 ? Math.Pow((input[i] - maxValue) / maxValue * 100, 2) : 0;
            drawdownSquared.Add(pctDrawdownSquared);

            var count = Math.Min(length, i + 1);
            double sum = 0;
            for (var j = i - count + 1; j <= i; j++)
            {
                sum += drawdownSquared[j];
            }

            var squaredAvg = count > 0 ? sum / count : 0;
            output.Add(Math.Sqrt(Math.Max(squaredAvg, 0)));
        }

        return output;
    }

    private static List<double> CalculateVaradiOscillatorNaive(StockData stockData, int length)
    {
        var count = stockData.Count;
        var ratio = new double[count];

        for (var i = 0; i < count; i++)
        {
            var median = (stockData.HighPrices[i] + stockData.LowPrices[i]) / 2;
            ratio[i] = median != 0 ? stockData.ClosePrices[i] / median : 0;
        }

        var aList = SimpleMovingAverageNaive(ratio, length);
        var output = new List<double>(count);
        var window = new Queue<double>(length);

        for (var i = 0; i < count; i++)
        {
            var prevA = i >= 1 ? aList[i - 1] : 0;
            window.Enqueue(prevA);
            if (window.Count > length)
            {
                window.Dequeue();
            }

            var countLe = 0;
            foreach (var value in window)
            {
                if (value <= aList[i])
                {
                    countLe++;
                }
            }

            var dvo = Math.Min(100, Math.Max(0, countLe / (double)length * 100));
            output.Add(dvo);
        }

        return output;
    }

    private static double[] SimpleMovingAverageNaive(IReadOnlyList<double> input, int length)
    {
        var output = new double[input.Count];
        double sum = 0;

        for (var i = 0; i < input.Count; i++)
        {
            sum += input[i];
            if (i >= length)
            {
                sum -= input[i - length];
            }

            output[i] = i >= length - 1 ? sum / length : 0;
        }

        return output;
    }

    private static List<double> CalculateSpearmanIndicatorNaive(IReadOnlyList<double> input, int length)
    {
        var count = input.Count;
        var windowLimit = Math.Max(1, length);
        var window = new List<double>(windowLimit);
        var output = new List<double>(count);

        for (var i = 0; i < count; i++)
        {
            window.Add(input[i]);
            if (window.Count > windowLimit)
            {
                window.RemoveAt(0);
            }

            var sorted = new List<double>(window);
            sorted.Sort();

            var rankByValue = new Dictionary<double, double>();
            double sumY = 0;
            double sumY2 = 0;
            var rank = 1;
            for (var j = 0; j < sorted.Count; )
            {
                var value = sorted[j];
                var k = j + 1;
                while (k < sorted.Count && sorted[k] == value)
                {
                    k++;
                }

                var span = k - j;
                var avgRank = (rank + (rank + span - 1)) / 2.0;
                rankByValue[value] = avgRank;
                sumY += avgRank * span;
                sumY2 += avgRank * avgRank * span;
                rank += span;
                j = k;
            }

            double sumX = 0;
            double sumX2 = 0;
            double sumXY = 0;
            for (var j = 0; j < window.Count; j++)
            {
                var valueX = window[j];
                var rankX = rankByValue[valueX];
                var rankY = rankByValue[sorted[j]];

                sumX += rankX;
                sumX2 += rankX * rankX;
                sumXY += rankX * rankY;
            }

            var n = (double)window.Count;
            var numerator = (n * sumXY) - (sumX * sumY);
            var denomLeft = (n * sumX2) - (sumX * sumX);
            var denomRight = (n * sumY2) - (sumY * sumY);
            var denom = Math.Sqrt(denomLeft * denomRight);
            var sc = denom != 0 ? numerator / denom : 0;
            if (double.IsNaN(sc) || double.IsInfinity(sc))
            {
                sc = 0;
            }

            output.Add(sc * 100);
        }

        return output;
    }

    private static List<double> RoundList(IEnumerable<double> values, int decimals = 8)
    {
        return values.Select(value => Math.Round(value, decimals)).ToList();    
    }
}
