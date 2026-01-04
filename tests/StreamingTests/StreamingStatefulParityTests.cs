using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class StreamingStatefulParityTests : GlobalTestData
{
    private const int SampleSize = 200;

    public static IEnumerable<object[]> StatefulIndicators
    {
        get
        {
            yield return new object[]
            {
                new StatefulIndicatorSpec("UlcerIndex",
                    () => new UlcerIndexState(14),
                    data => data.CalculateUlcerIndex(14).CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("VortexIndicator.ViPlus",
                    () => new VortexIndicatorState(14),
                    data => data.CalculateVortexIndicator(14).OutputValues["ViPlus"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AwesomeOscillator",
                    () => new AwesomeOscillatorState(),
                    data => data.CalculateAwesomeOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AcceleratorOscillator",
                    () => new AcceleratorOscillatorState(),
                    data => data.CalculateAcceleratorOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Trix",
                    () => new TrixState(),
                    data => data.CalculateTrix().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("StochasticOscillator.FastK",
                    () => new StochasticOscillatorState(),
                    data => data.CalculateStochasticOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("WilliamsR",
                    () => new WilliamsRState(),
                    data => data.CalculateWilliamsR().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChaikinMoneyFlow",
                    () => new ChaikinMoneyFlowState(),
                    data => data.CalculateChaikinMoneyFlow().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("CommodityChannelIndex",
                    () => new CommodityChannelIndexState(),
                    data => data.CalculateCommodityChannelIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("StochasticRelativeStrengthIndex",
                    () => new StochasticRelativeStrengthIndexState(),
                    data => data.CalculateStochasticRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ConnorsRelativeStrengthIndex",
                    () => new ConnorsRelativeStrengthIndexState(),
                    data => data.CalculateConnorsRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("StochasticConnorsRelativeStrengthIndex",
                    () => new StochasticConnorsRelativeStrengthIndexState(),
                    data => data.CalculateStochasticConnorsRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("StochasticMomentumIndex",
                    () => new StochasticMomentumIndexState(),
                    data => data.CalculateStochasticMomentumIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AccumulationDistributionLine",
                    () => new AccumulationDistributionLineState(),
                    data => data.CalculateAccumulationDistributionLine().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChaikinOscillator",
                    () => new ChaikinOscillatorState(),
                    data => data.CalculateChaikinOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("TrueStrengthIndex",
                    () => new TrueStrengthIndexState(),
                    data => data.CalculateTrueStrengthIndex().CustomValuesList)
            };
        }
    }

    [Theory]
    [MemberData(nameof(StatefulIndicators))]
    public void StatefulStreamingMatchesBatchOutputs(StatefulIndicatorSpec spec)
    {
        var data = StockTestData;
        data.Should().NotBeNullOrEmpty();

        var batchData = new StockData(data);
        var batchValues = spec.Calculate(batchData);
        batchValues.Should().NotBeNullOrEmpty();

        var maxCount = Math.Min(SampleSize, Math.Min(data.Count, batchValues.Count));
        maxCount.Should().BeGreaterThan(0);

        var state = spec.CreateState();
        var streamingValues = new List<double>(maxCount);
        for (var i = 0; i < maxCount; i++)
        {
            var ticker = data[i];
            var bar = new OhlcvBar("AAPL", BarTimeframe.Tick, ticker.Date, ticker.Date,
                ticker.Open, ticker.High, ticker.Low, ticker.Close, ticker.Volume, isFinal: true);
            var result = state.Update(bar, isFinal: true, includeOutputs: false);
            streamingValues.Add(result.Value);
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

    public sealed record StatefulIndicatorSpec(string Name,
        Func<IStreamingIndicatorState> CreateState,
        Func<StockData, List<double>> Calculate);
}
