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
            yield return new object[]
            {
                new StatefulIndicatorSpec("WeightedMovingAverage",
                    () => new WeightedMovingAverageState(14),
                    data => data.CalculateWeightedMovingAverage(14).CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("WellesWilderMovingAverage",
                    () => new WellesWilderMovingAverageState(14),
                    data => data.CalculateWellesWilderMovingAverage(14).CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("TriangularMovingAverage",
                    () => new TriangularMovingAverageState(MovingAvgType.SimpleMovingAverage, 20),
                    data => data.CalculateTriangularMovingAverage(MovingAvgType.SimpleMovingAverage, 20)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HullMovingAverage",
                    () => new HullMovingAverageState(MovingAvgType.WeightedMovingAverage, 20),
                    data => data.CalculateHullMovingAverage(MovingAvgType.WeightedMovingAverage, 20)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("PercentagePriceOscillator",
                    () => new PercentagePriceOscillatorState(MovingAvgType.ExponentialMovingAverage, 12, 26, 9),
                    data => data.CalculatePercentagePriceOscillator(MovingAvgType.ExponentialMovingAverage, 12, 26, 9)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("PercentageVolumeOscillator",
                    () => new PercentageVolumeOscillatorState(MovingAvgType.ExponentialMovingAverage, 12, 26, 9),
                    data => data.CalculatePercentageVolumeOscillator(MovingAvgType.ExponentialMovingAverage, 12, 26, 9)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DonchianChannels.MiddleChannel",
                    () => new DonchianChannelsState(20),
                    data => data.CalculateDonchianChannels(20).OutputValues["MiddleChannel"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChoppinessIndex",
                    () => new ChoppinessIndexState(14),
                    data => data.CalculateChoppinessIndex(MovingAvgType.ExponentialMovingAverage, 14)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeMomentumOscillator",
                    () => new ChandeMomentumOscillatorState(MovingAvgType.ExponentialMovingAverage, 14, 3),
                    data => data.CalculateChandeMomentumOscillator(MovingAvgType.ExponentialMovingAverage, 14, 3)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AveragePrice",
                    () => new AveragePriceState(),
                    data => data.CalculateAveragePrice().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FullTypicalPrice",
                    () => new FullTypicalPriceState(),
                    data => data.CalculateFullTypicalPrice().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MedianPrice",
                    () => new MedianPriceState(),
                    data => data.CalculateMedianPrice().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("TypicalPrice",
                    () => new TypicalPriceState(),
                    data => data.CalculateTypicalPrice().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("WeightedClose",
                    () => new WeightedCloseState(),
                    data => data.CalculateWeightedClose().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Midpoint",
                    () => new MidpointState(14),
                    data => data.CalculateMidpoint(14).CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Midprice",
                    () => new MidpriceState(14),
                    data => data.CalculateMidprice(14).CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("StandardDeviationVolatility",
                    () => new StandardDeviationVolatilityState(MovingAvgType.SimpleMovingAverage, 20),
                    data => data.CalculateStandardDeviationVolatility(MovingAvgType.SimpleMovingAverage, 20)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("StandardDeviation",
                    () => new StandardDeviationState(MovingAvgType.SimpleMovingAverage, 14),
                    data => data.CalculateStandardDevation(MovingAvgType.SimpleMovingAverage, 14)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AverageTrueRangeChannel.MiddleBand",
                    () => new AverageTrueRangeChannelState(MovingAvgType.SimpleMovingAverage, 14, 2.5),
                    data => data.CalculateAverageTrueRangeChannel(MovingAvgType.SimpleMovingAverage, 14, 2.5)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Dema2Lines.Dema1",
                    () => new Dema2LinesState(MovingAvgType.ExponentialMovingAverage, 10, 40),
                    data => data.CalculateDema2Lines(MovingAvgType.ExponentialMovingAverage, 10, 40)
                        .OutputValues["Dema1"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DynamicSupportAndResistance.MiddleBand",
                    () => new DynamicSupportAndResistanceState(MovingAvgType.WildersSmoothingMethod, 25),
                    data => data.CalculateDynamicSupportAndResistance(MovingAvgType.WildersSmoothingMethod, 25)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DailyAveragePriceDelta.UpperBand",
                    () => new DailyAveragePriceDeltaState(MovingAvgType.SimpleMovingAverage, 21),
                    data => data.CalculateDailyAveragePriceDelta(MovingAvgType.SimpleMovingAverage, 21)
                        .OutputValues["UpperBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DEnvelope.MiddleBand",
                    () => new DEnvelopeState(20, 2),
                    data => data.CalculateDEnvelope(20, 2).OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("PriceChannel.MiddleChannel",
                    () => new PriceChannelState(MovingAvgType.ExponentialMovingAverage, 21, 0.06),
                    data => data.CalculatePriceChannel(MovingAvgType.ExponentialMovingAverage, 21, 0.06)
                        .OutputValues["MiddleChannel"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MovingAverageChannel.MiddleBand",    
                    () => new MovingAverageChannelState(MovingAvgType.SimpleMovingAverage, 20),
                    data => data.CalculateMovingAverageChannel(MovingAvgType.SimpleMovingAverage, 20)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("PeriodicChannel.K",
                    () => new PeriodicChannelState(500, 2),
                    data => data.CalculatePeriodicChannel(500, 2).OutputValues["K"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("PriceLineChannel.MiddleBand",
                    () => new PriceLineChannelState(MovingAvgType.WildersSmoothingMethod, 100),
                    data => data.CalculatePriceLineChannel(MovingAvgType.WildersSmoothingMethod, 100)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("PriceCurveChannel.MiddleBand",
                    () => new PriceCurveChannelState(MovingAvgType.WildersSmoothingMethod, 100),
                    data => data.CalculatePriceCurveChannel(MovingAvgType.WildersSmoothingMethod, 100)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("RangeBands.MiddleBand",
                    () => new RangeBandsState(1, MovingAvgType.SimpleMovingAverage, 14),
                    data => data.CalculateRangeBands(1, MovingAvgType.SimpleMovingAverage, 14)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("RangeIdentifier.MiddleBand",
                    () => new RangeIdentifierState(34),
                    data => data.CalculateRangeIdentifier(34).OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MovingAverageEnvelope.MiddleBand",   
                    () => new MovingAverageEnvelopeState(MovingAvgType.SimpleMovingAverage, 20, 0.025),
                    data => data.CalculateMovingAverageEnvelope(MovingAvgType.SimpleMovingAverage, 20, 0.025)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LinearChannels.UpperBand",
                    () => new LinearChannelsState(14, 50),
                    data => data.CalculateLinearChannels(14, 50).OutputValues["UpperBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NarrowSidewaysChannel.MiddleBand",
                    () => new NarrowSidewaysChannelState(MovingAvgType.SimpleMovingAverage, 14, 3),
                    data => data.CalculateNarrowSidewaysChannel(MovingAvgType.SimpleMovingAverage, 14, 3)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("RateOfChangeBands.MiddleBand",
                    () => new RateOfChangeBandsState(MovingAvgType.ExponentialMovingAverage, 12, 3),
                    data => data.CalculateRateOfChangeBands(MovingAvgType.ExponentialMovingAverage, 12, 3)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GChannels.MiddleBand",
                    () => new GChannelsState(100),
                    data => data.CalculateGChannels(100).OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HighLowMovingAverage.MiddleBand",
                    () => new HighLowMovingAverageState(MovingAvgType.WeightedMovingAverage, 14),
                    data => data.CalculateHighLowMovingAverage(MovingAvgType.WeightedMovingAverage, 14)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HighLowBands.MiddleBand",
                    () => new HighLowBandsState(MovingAvgType.SimpleMovingAverage, 14, 1),
                    data => data.CalculateHighLowBands(MovingAvgType.SimpleMovingAverage, 14, 1)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KeltnerChannels.MiddleBand",
                    () => new KeltnerChannelsState(MovingAvgType.ExponentialMovingAverage, 20, 10, 2),
                    data => data.CalculateKeltnerChannels(MovingAvgType.ExponentialMovingAverage, 20, 10, 2)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ExtendedRecursiveBands.MiddleBand",
                    () => new ExtendedRecursiveBandsState(100),
                    data => data.CalculateExtendedRecursiveBands(100).OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("StollerAverageRangeChannels.MiddleBand",
                    () => new StollerAverageRangeChannelsState(MovingAvgType.SimpleMovingAverage, 14, 2),
                    data => data.CalculateStollerAverageRangeChannels(MovingAvgType.SimpleMovingAverage, 14, 2)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("UniChannel.MiddleBand",
                    () => new UniChannelState(MovingAvgType.SimpleMovingAverage, 10, 0.02, 0.02, false),
                    data => data.CalculateUniChannel(MovingAvgType.SimpleMovingAverage, 10, 0.02, 0.02, false)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("PriceHeadleyAccelerationBands.MiddleBand",
                    () => new PriceHeadleyAccelerationBandsState(MovingAvgType.SimpleMovingAverage, 20, 0.001),
                    data => data.CalculatePriceHeadleyAccelerationBands(MovingAvgType.SimpleMovingAverage, 20, 0.001)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("PseudoPolynomialChannel.MiddleBand",
                    () => new PseudoPolynomialChannelState(MovingAvgType.SimpleMovingAverage, 14, 0.9),
                    data => data.CalculatePseudoPolynomialChannel(MovingAvgType.SimpleMovingAverage, 14, 0.9)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ProjectedSupportAndResistance.MiddleBand",
                    () => new ProjectedSupportAndResistanceState(25),
                    data => data.CalculateProjectedSupportAndResistance(25)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("RootMovingAverageSquaredErrorBands.MiddleBand",
                    () => new RootMovingAverageSquaredErrorBandsState(1, MovingAvgType.SimpleMovingAverage, 14),
                    data => data.CalculateRootMovingAverageSquaredErrorBands(1, MovingAvgType.SimpleMovingAverage, 14)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MovingAverageBands.MiddleBand",
                    () => new MovingAverageBandsState(MovingAvgType.ExponentialMovingAverage, 10, 50, 1),
                    data => data.CalculateMovingAverageBands(MovingAvgType.ExponentialMovingAverage, 10, 50, 1)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MovingAverageSupportResistance.MiddleBand",
                    () => new MovingAverageSupportResistanceState(MovingAvgType.SimpleMovingAverage, 10, 2),
                    data => data.CalculateMovingAverageSupportResistance(MovingAvgType.SimpleMovingAverage, 10, 2)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MotionToAttractionChannels.MiddleBand",
                    () => new MotionToAttractionChannelsState(14),
                    data => data.CalculateMotionToAttractionChannels(14)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MeanAbsoluteErrorBands.MiddleBand",
                    () => new MeanAbsoluteErrorBandsState(1, MovingAvgType.SimpleMovingAverage, 14),
                    data => data.CalculateMeanAbsoluteErrorBands(1, MovingAvgType.SimpleMovingAverage, 14)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MeanAbsoluteDeviationBands.MiddleBand",
                    () => new MeanAbsoluteDeviationBandsState(2, MovingAvgType.SimpleMovingAverage, 20),
                    data => data.CalculateMeanAbsoluteDeviationBands(2, MovingAvgType.SimpleMovingAverage, 20)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MovingAverageDisplacedEnvelope.MiddleBand",
                    () => new MovingAverageDisplacedEnvelopeState(MovingAvgType.ExponentialMovingAverage, 9, 13, 0.5),
                    data => data.CalculateMovingAverageDisplacedEnvelope(MovingAvgType.ExponentialMovingAverage, 9, 13, 0.5)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("VerticalHorizontalFilter",
                    () => new VerticalHorizontalFilterState(MovingAvgType.WeightedMovingAverage, 18, 6),
                    data => data.CalculateVerticalHorizontalFilter(MovingAvgType.WeightedMovingAverage, 18, 6)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("SigmaSpikes",
                    () => new SigmaSpikesState(MovingAvgType.ExponentialMovingAverage, 20),
                    data => data.CalculateSigmaSpikes(MovingAvgType.ExponentialMovingAverage, 20)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("StatisticalVolatility",
                    () => new StatisticalVolatilityState(MovingAvgType.ExponentialMovingAverage, 30, 253),
                    data => data.CalculateStatisticalVolatility(MovingAvgType.ExponentialMovingAverage, 30, 253)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("VolatilitySwitchIndicator",
                    () => new VolatilitySwitchIndicatorState(MovingAvgType.WeightedMovingAverage, 14),
                    data => data.CalculateVolatilitySwitchIndicator(MovingAvgType.WeightedMovingAverage, 14)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("UltimateVolatilityIndicator",
                    () => new UltimateVolatilityIndicatorState(MovingAvgType.ExponentialMovingAverage, 14),
                    data => data.CalculateUltimateVolatilityIndicator(MovingAvgType.ExponentialMovingAverage, 14)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("VolatilityBasedMomentum",
                    () => new VolatilityBasedMomentumState(MovingAvgType.WildersSmoothingMethod, 22, 65),
                    data => data.CalculateVolatilityBasedMomentum(MovingAvgType.WildersSmoothingMethod, 22, 65)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("VolatilityQualityIndex",
                    () => new VolatilityQualityIndexState(MovingAvgType.SimpleMovingAverage, 9, 200),
                    data => data.CalculateVolatilityQualityIndex(MovingAvgType.SimpleMovingAverage, 9, 200)
                        .CustomValuesList)
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
