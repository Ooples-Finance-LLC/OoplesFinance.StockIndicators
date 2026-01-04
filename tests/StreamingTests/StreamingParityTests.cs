namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class StreamingParityTests : GlobalTestData
{
    private const int SampleSize = 200;

    public static IEnumerable<object[]> StreamingIndicators
    {
        get
        {
            yield return new object[]
            {
                new IndicatorSpec("SimpleMovingAverage", data =>
                    data.CalculateSimpleMovingAverage(20).CustomValuesList)
            };
            yield return new object[]
            {
                new IndicatorSpec("ExponentialMovingAverage", data =>
                    data.CalculateExponentialMovingAverage(20).CustomValuesList)
            };
            yield return new object[]
            {
                new IndicatorSpec("RelativeStrengthIndex", data =>
                    data.CalculateRelativeStrengthIndex(length: 14).CustomValuesList)
            };
            yield return new object[]
            {
                new IndicatorSpec("MovingAverageConvergenceDivergence", data =>
                    data.CalculateMovingAverageConvergenceDivergence().CustomValuesList)
            };
            yield return new object[]
            {
                new IndicatorSpec("AverageTrueRange", data =>
                    data.CalculateAverageTrueRange(length: 14).CustomValuesList)
            };
            yield return new object[]
            {
                new IndicatorSpec("AverageDirectionalIndex", data =>
                    data.CalculateAverageDirectionalIndex(length: 14).CustomValuesList)
            };
            yield return new object[]
            {
                new IndicatorSpec("StochasticOscillator", data =>
                    data.CalculateStochasticOscillator(length: 14, smoothLength1: 3, smoothLength2: 3)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new IndicatorSpec("MoneyFlowIndex", data =>
                    data.CalculateMoneyFlowIndex(length: 14).CustomValuesList)
            };
            yield return new object[]
            {
                new IndicatorSpec("OnBalanceVolume", data =>
                    data.CalculateOnBalanceVolume(length: 20).CustomValuesList)
            };
            yield return new object[]
            {
                new IndicatorSpec("RateOfChange", data =>
                    data.CalculateRateOfChange(length: 12).CustomValuesList)
            };
        }
    }

    [Theory]
    [MemberData(nameof(StreamingIndicators))]
    public void StreamingMatchesBatchOutputs(IndicatorSpec spec)
    {
        var data = StockTestData;
        data.Should().NotBeNullOrEmpty();

        var batchData = new StockData(data);
        var batchValues = spec.Calculate(batchData);
        batchValues.Should().NotBeNullOrEmpty();

        var maxCount = Math.Min(SampleSize, data.Count);
        maxCount.Should().BeLessThanOrEqualTo(batchValues.Count);

        var prefix = new List<TickerData>(maxCount);
        var streamingValues = new List<double>(maxCount);

        for (var i = 0; i < maxCount; i++)
        {
            prefix.Add(data[i]);
            var streamingData = new StockData(prefix);
            var values = spec.Calculate(streamingData);
            values.Should().NotBeNullOrEmpty();
            streamingValues.Add(values[values.Count - 1]);
        }

        for (var i = 0; i < maxCount; i++)
        {
            AssertEqual(batchValues[i], streamingValues[i], spec.Name, i);
        }
    }

    private static void AssertEqual(double expected, double actual, string name, int index)
    {
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue($"{name} at index {index} should be NaN");
            return;
        }

        actual.Should().BeApproximately(expected, 1e-10, $"{name} mismatch at index {index}");
    }

    public sealed record IndicatorSpec(string Name, Func<StockData, List<double>> Calculate);
}
