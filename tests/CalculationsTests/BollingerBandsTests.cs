namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

public sealed class BollingerBandsTests : GlobalTestData
{
    /// <summary>
    /// Bollinger Bands against an independently written reference implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference this test used to compare against encoded the defect described in issue #145
    /// rather than the definition of the indicator. It read:
    /// </para>
    /// <code>
    /// var middleBand = CalculateSimpleMovingAverageNaive(input, length);
    /// var stdDevMean = CalculateSimpleMovingAverageNaive(middleBand, length);
    /// var variance   = CalculateSimpleMovingAverageNaive(
    ///                      CalculateDeviationSquared(middleBand, stdDevMean), length);
    /// </code>
    /// <para>
    /// - the dispersion of <c>middleBand</c> about an average of <c>middleBand</c>. Price appears
    /// nowhere in it. That mirrored what the implementation did, so the pair agreed with each other
    /// while both disagreed with the indicator.
    /// </para>
    /// <para><b>Why the reference below is the correct one.</b></para>
    /// <list type="bullet">
    /// <item><description>
    /// John Bollinger's own definition: the middle band is an N-period moving average of price, and
    /// the outer bands sit K standard deviations of <i>price</i> above and below it. Price is the
    /// series whose dispersion is being measured; the average is only the centre.
    /// </description></item>
    /// <item><description>
    /// TA-Lib computes <c>BBANDS</c> as <c>TA_MA</c> plus and minus a multiple of <c>TA_STDDEV</c>,
    /// where <c>TA_STDDEV</c> is the population standard deviation of the input series about each
    /// window's own mean. pandas-ta and TradingView's <c>ta.bb</c> both do the same.
    /// </description></item>
    /// <item><description>
    /// Numerically: with the old code only 0.6% of bars fell outside the two-sigma envelope on the
    /// 200-bar AAPL fixture, where a correctly scaled two-sigma band leaves about 10% outside on this
    /// data. The upper band differed from an independently computed population sigma by up to 150.7.
    /// </description></item>
    /// </list>
    /// <para>
    /// Two separate defects were involved, and both had to be fixed for this test to pass:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// The moving average published onto <c>StockData.CustomValuesList</c>, and the standard deviation
    /// that followed resolved its input from that same property, so it measured the average rather
    /// than price. That is issue #145.
    /// </description></item>
    /// <item><description>
    /// <c>CalculateStandardDeviationVolatility</c> is not a rolling standard deviation. It squares
    /// each bar's deviation from <i>its own</i> contemporaneous moving average and averages those,
    /// <c>mean of (x[k] - sma[k])^2</c>, which is the mean squared residual from the moving-average
    /// line and runs larger whenever price trends. Bollinger needs <c>mean of (x[k] - mean[i])^2</c>.
    /// Fixing only the first defect still left the upper band up to 11.0 away from the reference.
    /// </description></item>
    /// </list>
    /// </remarks>
    [Fact]
    public void CalculateBollingerBands_MatchesAnIndependentReference()
    {
        var stockData = new StockData(StockTestData);
        const int length = 20;
        const double stdDevMult = 2;

        var results = stockData.CalculateBollingerBands(MovingAvgType.SimpleMovingAverage, length, stdDevMult);
        var expected = CalculateBollingerBandsReference(stockData.ClosePrices, length, stdDevMult);

        RoundList(results.OutputValues["UpperBand"]).Should().BeEquivalentTo(RoundList(expected.UpperBand));
        RoundList(results.OutputValues["MiddleBand"]).Should().BeEquivalentTo(RoundList(expected.MiddleBand));
        RoundList(results.OutputValues["LowerBand"]).Should().BeEquivalentTo(RoundList(expected.LowerBand));
    }

    /// <summary>
    /// The bands must be symmetric about the middle band, and the middle band must be the plain
    /// moving average of price - properties that hold for any correct implementation.
    /// </summary>
    [Fact]
    public void CalculateBollingerBands_BandsAreSymmetricAboutThePriceAverage()
    {
        var stockData = new StockData(StockTestData);
        const int length = 20;

        var results = stockData.CalculateBollingerBands(MovingAvgType.SimpleMovingAverage, length, 2);
        var upper = results.OutputValues["UpperBand"];
        var middle = results.OutputValues["MiddleBand"];
        var lower = results.OutputValues["LowerBand"];
        var sma = CalculateSimpleMovingAverageNaive(stockData.ClosePrices, length);

        for (var i = length - 1; i < middle.Count; i++)
        {
            middle[i].Should().BeApproximately(sma[i], 1e-10,
                $"the middle band is the moving average of price, index {i}");
            (upper[i] - middle[i]).Should().BeApproximately(middle[i] - lower[i], 1e-10,
                $"the bands are equidistant from the middle band, index {i}");
        }
    }

    /// <summary>
    /// Doubling the multiplier must double the distance from the middle band, which only holds when the
    /// dispersion term is independent of the multiplier.
    /// </summary>
    [Fact]
    public void CalculateBollingerBands_WidthScalesWithTheMultiplier()
    {
        const int length = 20;
        var oneSigma = new StockData(StockTestData).CalculateBollingerBands(MovingAvgType.SimpleMovingAverage, length, 1);
        var twoSigma = new StockData(StockTestData).CalculateBollingerBands(MovingAvgType.SimpleMovingAverage, length, 2);

        var oneUpper = oneSigma.OutputValues["UpperBand"];
        var oneMiddle = oneSigma.OutputValues["MiddleBand"];
        var twoUpper = twoSigma.OutputValues["UpperBand"];
        var twoMiddle = twoSigma.OutputValues["MiddleBand"];

        for (var i = length - 1; i < oneUpper.Count; i++)
        {
            (twoUpper[i] - twoMiddle[i]).Should().BeApproximately(2 * (oneUpper[i] - oneMiddle[i]), 1e-10,
                $"two sigma is exactly twice one sigma, index {i}");
        }
    }

    /// <summary>
    /// Bollinger Bands as defined: an N-period moving average of price, plus and minus K population
    /// standard deviations of price measured about each window's own mean.
    /// </summary>
    private static (List<double> UpperBand, List<double> MiddleBand, List<double> LowerBand) CalculateBollingerBandsReference(
        IReadOnlyList<double> input,
        int length,
        double stdDevMult)
    {
        var middleBand = CalculateSimpleMovingAverageNaive(input, length);
        var upperBand = new List<double>(input.Count);
        var lowerBand = new List<double>(input.Count);

        for (var i = 0; i < input.Count; i++)
        {
            if (i < length - 1)
            {
                // Warm-up follows MovingAverageCore.SimpleMovingAverage, which emits zero until the
                // window is full.
                upperBand.Add(0);
                lowerBand.Add(0);
                continue;
            }

            var mean = middleBand[i];
            double sumOfSquaredDeviations = 0;
            for (var k = i - length + 1; k <= i; k++)
            {
                var deviation = input[k] - mean;
                sumOfSquaredDeviations += deviation * deviation;
            }

            var stdDev = Math.Sqrt(sumOfSquaredDeviations / length);
            upperBand.Add(mean + (stdDev * stdDevMult));
            lowerBand.Add(mean - (stdDev * stdDevMult));
        }

        return (upperBand, middleBand, lowerBand);
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
