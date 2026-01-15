using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Helpers;

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
                new StatefulIndicatorSpec("AbsoluteStrengthIndex",
                    () => new AbsoluteStrengthIndexState(10, 21, 34),
                    data => data.CalculateAbsoluteStrengthIndex(10, 21, 34).CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AccumulativeSwingIndex",
                    () => new AccumulativeSwingIndexState(MovingAvgType.SimpleMovingAverage, 14),
                    data => data.CalculateAccumulativeSwingIndex(MovingAvgType.SimpleMovingAverage, 14)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BalanceOfPower",
                    () => new BalanceOfPowerState(MovingAvgType.ExponentialMovingAverage, 14),
                    data => data.CalculateBalanceOfPower(MovingAvgType.ExponentialMovingAverage, 14)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BelkhayateTiming",
                    () => new BelkhayateTimingState(),
                    data => data.CalculateBelkhayateTiming().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DetrendedPriceOscillator",
                    () => new DetrendedPriceOscillatorState(MovingAvgType.SimpleMovingAverage, 20),
                    data => data.CalculateDetrendedPriceOscillator(MovingAvgType.SimpleMovingAverage, 20)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChartmillValueIndicator.Cmvc",
                    () => new ChartmillValueIndicatorState(MovingAvgType.SimpleMovingAverage, InputName.MedianPrice, 5),
                    data => data.CalculateChartmillValueIndicator(MovingAvgType.SimpleMovingAverage, InputName.MedianPrice, 5)
                        .OutputValues["Cmvc"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ConditionalAccumulator",
                    () => new ConditionalAccumulatorState(MovingAvgType.ExponentialMovingAverage, 14, 1),
                    data => data.CalculateConditionalAccumulator(MovingAvgType.ExponentialMovingAverage, 14, 1)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AdaptiveErgodicCandlestickOscillator",
                    () => new AdaptiveErgodicCandlestickOscillatorState(MovingAvgType.ExponentialMovingAverage, 5, 14, 9),
                    data => data.CalculateAdaptiveErgodicCandlestickOscillator(MovingAvgType.ExponentialMovingAverage, 5, 14, 9)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AbsoluteStrengthMTFIndicator.Bulls",
                    () => new AbsoluteStrengthMTFIndicatorState(MovingAvgType.SimpleMovingAverage, 50, 25),
                    data => data.CalculateAbsoluteStrengthMTFIndicator(MovingAvgType.SimpleMovingAverage, 50, 25)
                        .OutputValues["Bulls"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AroonOscillator",
                    () => new AroonOscillatorState(25),
                    data => data.CalculateAroonOscillator(25).CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BearPowerIndicator",
                    () => new BearPowerIndicatorState(MovingAvgType.ExponentialMovingAverage, 14),
                    data => data.CalculateBearPowerIndicator(MovingAvgType.ExponentialMovingAverage, 14)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BullPowerIndicator",
                    () => new BullPowerIndicatorState(MovingAvgType.ExponentialMovingAverage, 14),
                    data => data.CalculateBullPowerIndicator(MovingAvgType.ExponentialMovingAverage, 14)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ContractHighLow.Ch",
                    () => new ContractHighLowState(),
                    data => data.CalculateContractHighLow().OutputValues["Ch"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChopZone",
                    () => new ChopZoneState(MovingAvgType.ExponentialMovingAverage, InputName.TypicalPrice, 30, 34),
                    data => data.CalculateChopZone(MovingAvgType.ExponentialMovingAverage, InputName.TypicalPrice, 30, 34)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("CenterOfLinearity",
                    () => new CenterOfLinearityState(14),
                    data => data.CalculateCenterOfLinearity(14).CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChaikinVolatility",
                    () => new ChaikinVolatilityState(MovingAvgType.ExponentialMovingAverage, 10, 12),
                    data => data.CalculateChaikinVolatility(MovingAvgType.ExponentialMovingAverage, 10, 12)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("CoppockCurve",
                    () => new CoppockCurveState(MovingAvgType.WeightedMovingAverage, 10, 11, 14),
                    data => data.CalculateCoppockCurve(MovingAvgType.WeightedMovingAverage, 10, 11, 14)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("CommoditySelectionIndex",
                    () => new CommoditySelectionIndexState(MovingAvgType.WildersSmoothingMethod, 14, 50, 3000, 10),
                    data => data.CalculateCommoditySelectionIndex(MovingAvgType.WildersSmoothingMethod, 14, 50, 3000, 10)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DeltaMovingAverage",
                    () => new DeltaMovingAverageState(MovingAvgType.SimpleMovingAverage, 10, 5),
                    data => data.CalculateDeltaMovingAverage(MovingAvgType.SimpleMovingAverage, 10, 5)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DetrendedSyntheticPrice",
                    () => new DetrendedSyntheticPriceState(14),
                    data => data.CalculateDetrendedSyntheticPrice(14).CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DerivativeOscillator",
                    () => new DerivativeOscillatorState(MovingAvgType.ExponentialMovingAverage, 14, 9, 5, 3),
                    data => data.CalculateDerivativeOscillator(MovingAvgType.ExponentialMovingAverage, 14, 9, 5, 3)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DemandOscillator",
                    () => new DemandOscillatorState(MovingAvgType.ExponentialMovingAverage, 10, 2, 20),
                    data => data.CalculateDemandOscillator(MovingAvgType.ExponentialMovingAverage, 10, 2, 20)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DoubleSmoothedMomenta",
                    () => new DoubleSmoothedMomentaState(MovingAvgType.ExponentialMovingAverage, 2, 5, 25),
                    data => data.CalculateDoubleSmoothedMomenta(MovingAvgType.ExponentialMovingAverage, 2, 5, 25)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DidiIndex.Curta",
                    () => new DidiIndexState(MovingAvgType.SimpleMovingAverage, 3, 8, 20),
                    data => data.CalculateDidiIndex(MovingAvgType.SimpleMovingAverage, 3, 8, 20)
                        .OutputValues["Curta"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DisparityIndex",
                    () => new DisparityIndexState(MovingAvgType.SimpleMovingAverage, 14),
                    data => data.CalculateDisparityIndex(MovingAvgType.SimpleMovingAverage, 14)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DampingIndex",
                    () => new DampingIndexState(MovingAvgType.SimpleMovingAverage, 5, 1.5),
                    data => data.CalculateDampingIndex(MovingAvgType.SimpleMovingAverage, 5, 1.5)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DirectionalTrendIndex",
                    () => new DirectionalTrendIndexState(MovingAvgType.ExponentialMovingAverage, 14, 10, 5),
                    data => data.CalculateDirectionalTrendIndex(MovingAvgType.ExponentialMovingAverage, 14, 10, 5)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DTOscillator",
                    () => new DTOscillatorState(MovingAvgType.WildersSmoothingMethod, 13, 8, 5, 3),
                    data => data.CalculateDTOscillator(MovingAvgType.WildersSmoothingMethod, 13, 8, 5, 3)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ElderRayIndex.BullPower",
                    () => new ElderRayIndexState(MovingAvgType.ExponentialMovingAverage, 13),
                    data => data.CalculateElderRayIndex(MovingAvgType.ExponentialMovingAverage, 13)
                        .OutputValues["BullPower"])
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
                new StatefulIndicatorSpec("AbsolutePriceOscillator",
                    () => new AbsolutePriceOscillatorState(MovingAvgType.ExponentialMovingAverage, 10, 20),
                    data => data.CalculateAbsolutePriceOscillator(MovingAvgType.ExponentialMovingAverage, 10, 20)
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
                new StatefulIndicatorSpec("ClosedFormDistanceVolatility",
                    () => new ClosedFormDistanceVolatilityState(MovingAvgType.ExponentialMovingAverage, 14),
                    data => data.CalculateClosedFormDistanceVolatility(MovingAvgType.ExponentialMovingAverage, 14)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DonchianChannelWidth",
                    () => new DonchianChannelWidthState(MovingAvgType.SimpleMovingAverage, 20, 22),
                    data => data.CalculateDonchianChannelWidth(MovingAvgType.SimpleMovingAverage, 20, 22)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HistoricalVolatility",
                    () => new HistoricalVolatilityState(MovingAvgType.ExponentialMovingAverage, 20),
                    data => data.CalculateHistoricalVolatility(MovingAvgType.ExponentialMovingAverage, 20)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GarmanKlassVolatility",
                    () => new GarmanKlassVolatilityState(MovingAvgType.WeightedMovingAverage, 14, 7),
                    data => data.CalculateGarmanKlassVolatility(MovingAvgType.WeightedMovingAverage, 14, 7)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GopalakrishnanRangeIndex",
                    () => new GopalakrishnanRangeIndexState(MovingAvgType.WeightedMovingAverage, 5),
                    data => data.CalculateGopalakrishnanRangeIndex(MovingAvgType.WeightedMovingAverage, 5)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HistoricalVolatilityPercentile",
                    () => new HistoricalVolatilityPercentileState(MovingAvgType.ExponentialMovingAverage, 21, 252),
                    data => data.CalculateHistoricalVolatilityPercentile(MovingAvgType.ExponentialMovingAverage, 21, 252)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FastZScore",
                    () => new FastZScoreState(MovingAvgType.SimpleMovingAverage, 200),
                    data => data.CalculateFastZScore(MovingAvgType.SimpleMovingAverage, 200)
                        .CustomValuesList)
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
                new StatefulIndicatorSpec("LinearRegression",
                    () => new LinearRegressionState(14),
                    data => data.CalculateLinearRegression(14).CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("StandardDeviationChannel.MiddleBand",
                    () => new StandardDeviationChannelState(40, 2),
                    data => data.CalculateStandardDeviationChannel(40, 2)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("TimeSeriesForecast.MiddleBand",
                    () => new TimeSeriesForecastState(500),
                    data => data.CalculateTimeSeriesForecast(500)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("SmartEnvelope.MiddleBand",
                    () => new SmartEnvelopeState(14, 1),
                    data => data.CalculateSmartEnvelope(14, 1)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("SupportResistance.Support",
                    () => new SupportResistanceState(MovingAvgType.SimpleMovingAverage, 20),
                    data => data.CalculateSupportResistance(MovingAvgType.SimpleMovingAverage, 20)
                        .OutputValues["Support"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("StationaryExtrapolatedLevels.MiddleBand",
                    () => new StationaryExtrapolatedLevelsState(MovingAvgType.SimpleMovingAverage, 50),
                    data => data.CalculateStationaryExtrapolatedLevels(MovingAvgType.SimpleMovingAverage, 50)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ScalpersChannel.MiddleBand",
                    () => new ScalpersChannelState(MovingAvgType.SimpleMovingAverage, 15, 20),
                    data => data.CalculateScalpersChannel(MovingAvgType.SimpleMovingAverage, 15, 20)
                        .OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("SmoothedVolatilityBands.MiddleBand",
                    () => new SmoothedVolatilityBandsState(MovingAvgType.ExponentialMovingAverage, 20, 21, 2.4, 0.9),
                    data => data.CalculateSmoothedVolatilityBands(MovingAvgType.ExponentialMovingAverage, 20, 21, 2.4, 0.9)
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
                new StatefulIndicatorSpec("ProjectionBands.MiddleBand",
                    () => new ProjectionBandsState(14),
                    data => data.CalculateProjectionBands(14).OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ProjectionOscillator",
                    () => new ProjectionOscillatorState(MovingAvgType.WeightedMovingAverage, 14, 4),
                    data => data.CalculateProjectionOscillator(MovingAvgType.WeightedMovingAverage, 14, 4)
                        .CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ProjectionBandwidth",
                    () => new ProjectionBandwidthState(MovingAvgType.WeightedMovingAverage, 14),
                    data => data.CalculateProjectionBandwidth(MovingAvgType.WeightedMovingAverage, 14)
                        .CustomValuesList)
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
            yield return new object[]
            {
                new StatefulIndicatorSpec("_1LCLeastSquaresMovingAverage",
                    () => new _1LCLeastSquaresMovingAverageState(),
                    data => data.Calculate1LCLeastSquaresMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("_3HMA",
                    () => new _3HMAState(),
                    data => data.Calculate3HMA().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("_4MovingAverageConvergenceDivergence.Macd1",
                    () => new _4MovingAverageConvergenceDivergenceState(),
                    data => data.Calculate4MovingAverageConvergenceDivergence().OutputValues["Macd1"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("_4PercentagePriceOscillator.Ppo1",
                    () => new _4PercentagePriceOscillatorState(),
                    data => data.Calculate4PercentagePriceOscillator().OutputValues["Ppo1"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AdaptiveAutonomousRecursiveMovingAverage",
                    () => new AdaptiveAutonomousRecursiveMovingAverageState(),
                    data => data.CalculateAdaptiveAutonomousRecursiveMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AdaptiveAutonomousRecursiveTrailingStop",
                    () => new AdaptiveAutonomousRecursiveTrailingStopState(),
                    data => data.CalculateAdaptiveAutonomousRecursiveTrailingStop().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AdaptiveExponentialMovingAverage",
                    () => new AdaptiveExponentialMovingAverageState(),
                    data => data.CalculateAdaptiveExponentialMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AdaptiveLeastSquares",
                    () => new AdaptiveLeastSquaresState(),
                    data => data.CalculateAdaptiveLeastSquares().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AdaptiveMovingAverage",
                    () => new AdaptiveMovingAverageState(),
                    data => data.CalculateAdaptiveMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AdaptivePriceZoneIndicator.MiddleBand",
                    () => new AdaptivePriceZoneIndicatorState(),
                    data => data.CalculateAdaptivePriceZoneIndicator().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AdaptiveRelativeStrengthIndex",
                    () => new AdaptiveRelativeStrengthIndexState(),
                    data => data.CalculateAdaptiveRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AdaptiveStochastic",
                    () => new AdaptiveStochasticState(),
                    data => data.CalculateAdaptiveStochastic().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AdaptiveTrailingStop",
                    () => new AdaptiveTrailingStopState(),
                    data => data.CalculateAdaptiveTrailingStop().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AhrensMovingAverage",
                    () => new AhrensMovingAverageState(),
                    data => data.CalculateAhrensMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AlligatorIndex.Lips",
                    () => new AlligatorIndexState(),
                    data => data.CalculateAlligatorIndex().OutputValues["Lips"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AlphaDecreasingExponentialMovingAverage",
                    () => new AlphaDecreasingExponentialMovingAverageState(),
                    data => data.CalculateAlphaDecreasingExponentialMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AnchoredMomentum",
                    () => new AnchoredMomentumState(),
                    data => data.CalculateAnchoredMomentum().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ApirineSlowRelativeStrengthIndex",
                    () => new ApirineSlowRelativeStrengthIndexState(),
                    data => data.CalculateApirineSlowRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ArnaudLegouxMovingAverage",
                    () => new ArnaudLegouxMovingAverageState(),
                    data => data.CalculateArnaudLegouxMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AsymmetricalRelativeStrengthIndex",  
                    () => new AsymmetricalRelativeStrengthIndexState(),
                    data => data.CalculateAsymmetricalRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AtrFilteredExponentialMovingAverage",
                    () => new AtrFilteredExponentialMovingAverageState(),
                    data => data.CalculateAtrFilteredExponentialMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AutoDispersionBands.MiddleBand",
                    () => new AutoDispersionBandsState(),
                    data => data.CalculateAutoDispersionBands().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AutoFilter",
                    () => new AutoFilterState(),
                    data => data.CalculateAutoFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AutoLine",
                    () => new AutoLineState(),
                    data => data.CalculateAutoLine().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AutoLineWithDrift",
                    () => new AutoLineWithDriftState(),
                    data => data.CalculateAutoLineWithDrift().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AutonomousRecursiveMovingAverage",
                    () => new AutonomousRecursiveMovingAverageState(),
                    data => data.CalculateAutonomousRecursiveMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AverageAbsoluteErrorNormalization",
                    () => new AverageAbsoluteErrorNormalizationState(),
                    data => data.CalculateAverageAbsoluteErrorNormalization().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AverageMoneyFlowOscillator",
                    () => new AverageMoneyFlowOscillatorState(),
                    data => data.CalculateAverageMoneyFlowOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AverageTrueRangeTrailingStops",
                    () => new AverageTrueRangeTrailingStopsState(),
                    data => data.CalculateAverageTrueRangeTrailingStops().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BayesianOscillator.ProbPrime",
                    () => new BayesianOscillatorState(),
                    data => data.CalculateBayesianOscillator().OutputValues["ProbPrime"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BetterVolumeIndicator",
                    () => new BetterVolumeIndicatorState(),
                    data => data.CalculateBetterVolumeIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BilateralStochasticOscillator",
                    () => new BilateralStochasticOscillatorState(),
                    data => data.CalculateBilateralStochasticOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BollingerBandsAverageTrueRange",
                    () => new BollingerBandsAverageTrueRangeState(),
                    data => data.CalculateBollingerBandsAvgTrueRange().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BollingerBandsFibonacciRatios.MiddleBand",
                    () => new BollingerBandsFibonacciRatiosState(),
                    data => data.CalculateBollingerBandsFibonacciRatios().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BollingerBandsPercentB",
                    () => new BollingerBandsPercentBState(),
                    data => data.CalculateBollingerBandsPercentB().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BollingerBandsWidth",
                    () => new BollingerBandsWidthState(),
                    data => data.CalculateBollingerBandsWidth().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BollingerBandsWithAtrPct.MiddleBand",
                    () => new BollingerBandsWithAtrPctState(),
                    data => data.CalculateBollingerBandsWithAtrPct().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BreakoutRelativeStrengthIndex",
                    () => new BreakoutRelativeStrengthIndexState(),
                    data => data.CalculateBreakoutRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BryantAdaptiveMovingAverage",
                    () => new BryantAdaptiveMovingAverageState(),
                    data => data.CalculateBryantAdaptiveMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BuffAverage.FastBuff",
                    () => new BuffAverageState(),
                    data => data.CalculateBuffAverage().OutputValues["FastBuff"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("CalmarRatio",
                    () => new CalmarRatioState(),
                    data => data.CalculateCalmarRatio().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("CamarillaPivotPoints.Pivot",
                    () => new CamarillaPivotPointsState(),
                    data => data.CalculateCamarillaPivotPoints().OutputValues["Pivot"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("CCTStochRelativeStrengthIndex.Type1",
                    () => new CCTStochRelativeStrengthIndexState(),
                    data => data.CalculateCCTStochRSI().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeCompositeMomentumIndex",
                    () => new ChandeCompositeMomentumIndexState(),
                    data => data.CalculateChandeCompositeMomentumIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeForecastOscillator",
                    () => new ChandeForecastOscillatorState(),
                    data => data.CalculateChandeForecastOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeIntradayMomentumIndex",
                    () => new ChandeIntradayMomentumIndexState(),
                    data => data.CalculateChandeIntradayMomentumIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeKrollRSquaredIndex",
                    () => new ChandeKrollRSquaredIndexState(),
                    data => data.CalculateChandeKrollRSquaredIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandelierExit.ExitLong",
                    () => new ChandelierExitState(),
                    data => data.CalculateChandelierExit().OutputValues["ExitLong"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeMomentumOscillatorAbsolute",
                    () => new ChandeMomentumOscillatorAbsoluteState(),
                    data => data.CalculateChandeMomentumOscillatorAbsolute().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeMomentumOscillatorAbsoluteAverage",
                    () => new ChandeMomentumOscillatorAbsoluteAverageState(),
                    data => data.CalculateChandeMomentumOscillatorAbsoluteAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeMomentumOscillatorAverage",
                    () => new ChandeMomentumOscillatorAverageState(),
                    data => data.CalculateChandeMomentumOscillatorAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeMomentumOscillatorAverageDisparityIndex",
                    () => new ChandeMomentumOscillatorAverageDisparityIndexState(),
                    data => data.CalculateChandeMomentumOscillatorAverageDisparityIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeMomentumOscillatorFilter",
                    () => new ChandeMomentumOscillatorFilterState(),
                    data => data.CalculateChandeMomentumOscillatorFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeQuickStick",
                    () => new ChandeQuickStickState(),
                    data => data.CalculateChandeQuickStick().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeTrendScore",
                    () => new ChandeTrendScoreState(),
                    data => data.CalculateChandeTrendScore().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ChandeVolatilityIndexDynamicAverageIndicator.Cvida1",
                    () => new ChandeVolatilityIndexDynamicAverageIndicatorState(),
                    data => data.CalculateChandeVolatilityIndexDynamicAverageIndicator().OutputValues["Cvida1"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("CompoundRatioMovingAverage",
                    () => new CompoundRatioMovingAverageState(),
                    data => data.CalculateCompoundRatioMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ConstanceBrownCompositeIndex",
                    () => new ConstanceBrownCompositeIndexState(),
                    data => data.CalculateConstanceBrownCompositeIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("CorrectedMovingAverage",
                    () => new CorrectedMovingAverageState(),
                    data => data.CalculateCorrectedMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("CubedWeightedMovingAverage",
                    () => new CubedWeightedMovingAverageState(),
                    data => data.CalculateCubedWeightedMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("CoralTrendIndicator",
                    () => new CoralTrendIndicatorState(),
                    data => data.CalculateCoralTrendIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DecisionPointPriceMomentumOscillator",
                    () => new DecisionPointPriceMomentumOscillatorState(),
                    data => data.CalculateDecisionPointPriceMomentumOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Demarker",
                    () => new DemarkerState(),
                    data => data.CalculateDemarker().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DemarkPivotPoints.Pivot",
                    () => new DemarkPivotPointsState(),
                    data => data.CalculateDemarkPivotPoints().OutputValues["Pivot"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DemarkPressureRatioV1",
                    () => new DemarkPressureRatioV1State(),
                    data => data.CalculateDemarkPressureRatioV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DemarkPressureRatioV2",
                    () => new DemarkPressureRatioV2State(),
                    data => data.CalculateDemarkPressureRatioV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DemarkRangeExpansionIndex",
                    () => new DemarkRangeExpansionIndexState(),
                    data => data.CalculateDemarkRangeExpansionIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DemarkReversalPoints",
                    () => new DemarkReversalPointsState(),
                    data => data.CalculateDemarkReversalPoints().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DemarkSetupIndicator",
                    () => new DemarkSetupIndicatorState(),
                    data => data.CalculateDemarkSetupIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DiNapoliMovingAverageConvergenceDivergence.Macd",
                    () => new DiNapoliMovingAverageConvergenceDivergenceState(),
                    data => data.CalculateDiNapoliMovingAverageConvergenceDivergence().OutputValues["Macd"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DiNapoliPercentagePriceOscillator",
                    () => new DiNapoliPercentagePriceOscillatorState(),
                    data => data.CalculateDiNapoliPercentagePriceOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DiNapoliPreferredStochasticOscillator",
                    () => new DiNapoliPreferredStochasticOscillatorState(),
                    data => data.CalculateDiNapoliPreferredStochasticOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DistanceWeightedMovingAverage",
                    () => new DistanceWeightedMovingAverageState(),
                    data => data.CalculateDistanceWeightedMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DMIStochastic",
                    () => new DMIStochasticState(),
                    data => data.CalculateDMIStochastic().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DoubleExponentialMovingAverage",
                    () => new DoubleExponentialMovingAverageState(),
                    data => data.CalculateDoubleExponentialMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DoubleExponentialSmoothing",
                    () => new DoubleExponentialSmoothingState(),
                    data => data.CalculateDoubleExponentialSmoothing().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DoubleSmoothedRelativeStrengthIndex",
                    () => new DoubleSmoothedRelativeStrengthIndexState(),
                    data => data.CalculateDoubleSmoothedRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DoubleSmoothedStochastic",
                    () => new DoubleSmoothedStochasticState(),
                    data => data.CalculateDoubleSmoothedStochastic().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DoubleStochasticOscillator",
                    () => new DoubleStochasticOscillatorState(),
                    data => data.CalculateDoubleStochasticOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EaseOfMovement",
                    () => new EaseOfMovementState(),
                    data => data.CalculateEaseOfMovement().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ConfluenceIndicator",
                    () => new ConfluenceIndicatorState(),
                    data => data.CalculateConfluenceIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DampedSineWaveWeightedFilter",
                    () => new DampedSineWaveWeightedFilterState(),
                    data => data.CalculateDampedSineWaveWeightedFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DecisionPointBreadthSwenlinTradingOscillator",
                    () => new DecisionPointBreadthSwenlinTradingOscillatorState(),
                    data => data.CalculateDecisionPointBreadthSwenlinTradingOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DominantCycleTunedRelativeStrengthIndex",
                    () => new DominantCycleTunedRelativeStrengthIndexState(),
                    data => data.CalculateDominantCycleTunedRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DrunkardWalk.UpWalk",
                    () => new DrunkardWalkState(),
                    data => data.CalculateDrunkardWalk().OutputValues["UpWalk"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DynamicallyAdjustableFilter",
                    () => new DynamicallyAdjustableFilterState(),
                    data => data.CalculateDynamicallyAdjustableFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DynamicallyAdjustableMovingAverage",
                    () => new DynamicallyAdjustableMovingAverageState(),
                    data => data.CalculateDynamicallyAdjustableMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DynamicMomentumIndex",
                    () => new DynamicMomentumIndexState(),
                    data => data.CalculateDynamicMomentumIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("DynamicMomentumOscillator",
                    () => new DynamicMomentumOscillatorState(),
                    data => data.CalculateDynamicMomentumOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EarningSupportResistanceLevels",
                    () => new EarningSupportResistanceLevelsState(),
                    data => data.CalculateEarningSupportResistanceLevels().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EdgePreservingFilter",
                    () => new EdgePreservingFilterState(),
                    data => data.CalculateEdgePreservingFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EfficientAutoLine",
                    () => new EfficientAutoLineState(),
                    data => data.CalculateEfficientAutoLine().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EfficientPrice",
                    () => new EfficientPriceState(),
                    data => data.CalculateEfficientPrice().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EfficientTrendStepChannel.MiddleBand",
                    () => new EfficientTrendStepChannelState(),
                    data => data.CalculateEfficientTrendStepChannel().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Ehlers2PoleButterworthFilterV1",
                    () => new Ehlers2PoleButterworthFilterV1State(),
                    data => data.CalculateEhlers2PoleButterworthFilterV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Ehlers2PoleButterworthFilterV2",
                    () => new Ehlers2PoleButterworthFilterV2State(),
                    data => data.CalculateEhlers2PoleButterworthFilterV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Ehlers2PoleSuperSmootherFilterV1",
                    () => new Ehlers2PoleSuperSmootherFilterV1State(),
                    data => data.CalculateEhlers2PoleSuperSmootherFilterV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Ehlers2PoleSuperSmootherFilterV2",
                    () => new Ehlers2PoleSuperSmootherFilterV2State(),
                    data => data.CalculateEhlers2PoleSuperSmootherFilterV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Ehlers3PoleButterworthFilterV1",
                    () => new Ehlers3PoleButterworthFilterV1State(),
                    data => data.CalculateEhlers3PoleButterworthFilterV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Ehlers3PoleButterworthFilterV2",
                    () => new Ehlers3PoleButterworthFilterV2State(),
                    data => data.CalculateEhlers3PoleButterworthFilterV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("Ehlers3PoleSuperSmootherFilter",
                    () => new Ehlers3PoleSuperSmootherFilterState(),
                    data => data.CalculateEhlers3PoleSuperSmootherFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveBandPassFilter",
                    () => new EhlersAdaptiveBandPassFilterState(),
                    data => data.CalculateEhlersAdaptiveBandPassFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveCenterOfGravityOscillator",
                    () => new EhlersAdaptiveCenterOfGravityOscillatorState(),
                    data => data.CalculateEhlersAdaptiveCenterOfGravityOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveCommodityChannelIndexV1",
                    () => new EhlersAdaptiveCommodityChannelIndexV1State(),
                    data => data.CalculateEhlersAdaptiveCommodityChannelIndexV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveCommodityChannelIndexV2",
                    () => new EhlersAdaptiveCommodityChannelIndexV2State(),
                    data => data.CalculateEhlersAdaptiveCommodityChannelIndexV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveCyberCycle",
                    () => new EhlersAdaptiveCyberCycleState(),
                    data => data.CalculateEhlersAdaptiveCyberCycle().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveLaguerreFilter",
                    () => new EhlersAdaptiveLaguerreFilterState(),
                    data => data.CalculateEhlersAdaptiveLaguerreFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveRelativeStrengthIndexV1",
                    () => new EhlersAdaptiveRelativeStrengthIndexV1State(),
                    data => data.CalculateEhlersAdaptiveRelativeStrengthIndexV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveRelativeStrengthIndexV2",
                    () => new EhlersAdaptiveRelativeStrengthIndexV2State(),
                    data => data.CalculateEhlersAdaptiveRelativeStrengthIndexV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveRsiFisherTransformV1",
                    () => new EhlersAdaptiveRsiFisherTransformV1State(),
                    data => data.CalculateEhlersAdaptiveRsiFisherTransformV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveRsiFisherTransformV2",
                    () => new EhlersAdaptiveRsiFisherTransformV2State(),
                    data => data.CalculateEhlersAdaptiveRsiFisherTransformV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveStochasticIndicatorV1",
                    () => new EhlersAdaptiveStochasticIndicatorV1State(),
                    data => data.CalculateEhlersAdaptiveStochasticIndicatorV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveStochasticIndicatorV2",
                    () => new EhlersAdaptiveStochasticIndicatorV2State(),
                    data => data.CalculateEhlersAdaptiveStochasticIndicatorV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAdaptiveStochasticInverseFisherTransform",
                    () => new EhlersAdaptiveStochasticInverseFisherTransformState(),
                    data => data.CalculateEhlersAdaptiveStochasticInverseFisherTransform().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAllPassPhaseShifter",
                    () => new EhlersAllPassPhaseShifterState(),
                    data => data.CalculateEhlersAllPassPhaseShifter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAlternateSignalToNoiseRatio",
                    () => new EhlersAlternateSignalToNoiseRatioState(),
                    data => data.CalculateEhlersAlternateSignalToNoiseRatio().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAMDetector",
                    () => new EhlersAMDetectorState(),
                    data => data.CalculateEhlersAMDetector().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAnticipateIndicator",
                    () => new EhlersAnticipateIndicatorState(),
                    data => data.CalculateEhlersAnticipateIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAutoCorrelationIndicator",
                    () => new EhlersAutoCorrelationIndicatorState(),
                    data => data.CalculateEhlersAutoCorrelationIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAutoCorrelationPeriodogram",
                    () => new EhlersAutoCorrelationPeriodogramState(),
                    data => data.CalculateEhlersAutoCorrelationPeriodogram().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAutoCorrelationReversals",
                    () => new EhlersAutoCorrelationReversalsState(),
                    data => data.CalculateEhlersAutoCorrelationReversals().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersAverageErrorFilter",
                    () => new EhlersAverageErrorFilterState(),
                    data => data.CalculateEhlersAverageErrorFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersBandPassFilterV1",
                    () => new EhlersBandPassFilterV1State(),
                    data => data.CalculateEhlersBandPassFilterV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersBandPassFilterV2",
                    () => new EhlersBandPassFilterV2State(),
                    data => data.CalculateEhlersBandPassFilterV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersBetterExponentialMovingAverage",
                    () => new EhlersBetterExponentialMovingAverageState(),
                    data => data.CalculateEhlersBetterExponentialMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersCenterofGravityOscillator",
                    () => new EhlersCenterofGravityOscillatorState(),
                    data => data.CalculateEhlersCenterofGravityOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersChebyshevLowPassFilter",
                    () => new EhlersChebyshevLowPassFilterState(),
                    data => data.CalculateEhlersChebyshevLowPassFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersClassicHilbertTransformer.Real",
                    () => new EhlersClassicHilbertTransformerState(),
                    data => data.CalculateEhlersClassicHilbertTransformer().OutputValues["Real"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersCombFilterSpectralEstimate",
                    () => new EhlersCombFilterSpectralEstimateState(),
                    data => data.CalculateEhlersCombFilterSpectralEstimate().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersCommodityChannelIndexInverseFisherTransform",
                    () => new EhlersCommodityChannelIndexInverseFisherTransformState(),
                    data => data.CalculateEhlersCommodityChannelIndexInverseFisherTransform().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersConvolutionIndicator",
                    () => new EhlersConvolutionIndicatorState(),
                    data => data.CalculateEhlersConvolutionIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersCorrelationAngleIndicator",
                    () => new EhlersCorrelationAngleIndicatorState(),
                    data => data.CalculateEhlersCorrelationAngleIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersCorrelationCycleIndicator.Real",
                    () => new EhlersCorrelationCycleIndicatorState(),
                    data => data.CalculateEhlersCorrelationCycleIndicator().OutputValues["Real"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersCorrelationTrendIndicator",
                    () => new EhlersCorrelationTrendIndicatorState(),
                    data => data.CalculateEhlersCorrelationTrendIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersCyberCycle",
                    () => new EhlersCyberCycleState(),
                    data => data.CalculateEhlersCyberCycle().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersCycleAmplitude",
                    () => new EhlersCycleAmplitudeState(),
                    data => data.CalculateEhlersCycleAmplitude().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersCycleBandPassFilter",
                    () => new EhlersCycleBandPassFilterState(),
                    data => data.CalculateEhlersCycleBandPassFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersDecycler",
                    () => new EhlersDecyclerState(),
                    data => data.CalculateEhlersDecycler().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersDecyclerOscillatorV1.SlowEdo",
                    () => new EhlersDecyclerOscillatorV1State(),
                    data => data.CalculateEhlersDecyclerOscillatorV1().OutputValues["SlowEdo"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersDecyclerOscillatorV2",
                    () => new EhlersDecyclerOscillatorV2State(),
                    data => data.CalculateEhlersDecyclerOscillatorV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersDetrendedLeadingIndicator",
                    () => new EhlersDetrendedLeadingIndicatorState(),
                    data => data.CalculateEhlersDetrendedLeadingIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersDeviationScaledMovingAverage",
                    () => new EhlersDeviationScaledMovingAverageState(),
                    data => data.CalculateEhlersDeviationScaledMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersDeviationScaledSuperSmoother",
                    () => new EhlersDeviationScaledSuperSmootherState(),
                    data => data.CalculateEhlersDeviationScaledSuperSmoother().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersDiscreteFourierTransform",
                    () => new EhlersDiscreteFourierTransformState(),
                    data => data.CalculateEhlersDiscreteFourierTransform().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersDiscreteFourierTransformSpectralEstimate",
                    () => new EhlersDiscreteFourierTransformSpectralEstimateState(),
                    data => data.CalculateEhlersDiscreteFourierTransformSpectralEstimate().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersDistanceCoefficientFilter",
                    () => new EhlersDistanceCoefficientFilterState(),
                    data => data.CalculateEhlersDistanceCoefficientFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersDominantCycleTunedBypassFilter.V2",
                    () => new EhlersDominantCycleTunedBypassFilterState(),
                    data => data.CalculateEhlersDominantCycleTunedBypassFilter().OutputValues["V2"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersDualDifferentiatorDominantCycle",
                    () => new EhlersDualDifferentiatorDominantCycleState(),
                    data => data.CalculateEhlersDualDifferentiatorDominantCycle().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersEarlyOnsetTrendIndicator",     
                    () => new EhlersEarlyOnsetTrendIndicatorState(),
                    data => data.CalculateEhlersEarlyOnsetTrendIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersEmpiricalModeDecomposition.Trend",
                    () => new EhlersEmpiricalModeDecompositionState(),
                    data => data.CalculateEhlersEmpiricalModeDecomposition().OutputValues["Trend"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersEnhancedSignalToNoiseRatio",   
                    () => new EhlersEnhancedSignalToNoiseRatioState(),
                    data => data.CalculateEhlersEnhancedSignalToNoiseRatio().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersEvenBetterSineWaveIndicator",
                    () => new EhlersEvenBetterSineWaveIndicatorState(),
                    data => data.CalculateEhlersEvenBetterSineWaveIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersFilter",
                    () => new EhlersFilterState(),
                    data => data.CalculateEhlersFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersFiniteImpulseResponseFilter",
                    () => new EhlersFiniteImpulseResponseFilterState(),
                    data => data.CalculateEhlersFiniteImpulseResponseFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersFisherizedDeviationScaledOscillator",
                    () => new EhlersFisherizedDeviationScaledOscillatorState(),
                    data => data.CalculateEhlersFisherizedDeviationScaledOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersFisherTransform",
                    () => new EhlersFisherTransformState(),
                    data => data.CalculateEhlersFisherTransform().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersFMDemodulatorIndicator",
                    () => new EhlersFMDemodulatorIndicatorState(),
                    data => data.CalculateEhlersFMDemodulatorIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersFourierSeriesAnalysis.Wave",
                    () => new EhlersFourierSeriesAnalysisState(),
                    data => data.CalculateEhlersFourierSeriesAnalysis().OutputValues["Wave"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersFractalAdaptiveMovingAverage",
                    () => new EhlersFractalAdaptiveMovingAverageState(),
                    data => data.CalculateEhlersFractalAdaptiveMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersGaussianFilter",
                    () => new EhlersGaussianFilterState(),
                    data => data.CalculateEhlersGaussianFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHammingMovingAverage",
                    () => new EhlersHammingMovingAverageState(),
                    data => data.CalculateEhlersHammingMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHammingWindowIndicator",
                    () => new EhlersHammingWindowIndicatorState(),
                    data => data.CalculateEhlersHammingWindowIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHannMovingAverage",
                    () => new EhlersHannMovingAverageState(),
                    data => data.CalculateEhlersHannMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHannWindowIndicator",
                    () => new EhlersHannWindowIndicatorState(),
                    data => data.CalculateEhlersHannWindowIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHighPassFilterV1",
                    () => new EhlersHighPassFilterV1State(),
                    data => data.CalculateEhlersHighPassFilterV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHighPassFilterV2",
                    () => new EhlersHighPassFilterV2State(),
                    data => data.CalculateEhlersHighPassFilterV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHilbertOscillator.IQ",
                    () => new EhlersHilbertOscillatorState(),
                    data => data.CalculateEhlersHilbertOscillator().OutputValues["IQ"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHilbertTransformIndicator.Quad",
                    () => new EhlersHilbertTransformIndicatorState(),
                    data => data.CalculateEhlersHilbertTransformIndicator().OutputValues["Quad"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHilbertTransformer.Real",
                    () => new EhlersHilbertTransformerState(),
                    data => data.CalculateEhlersHilbertTransformer().OutputValues["Real"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHilbertTransformerIndicator.Real",
                    () => new EhlersHilbertTransformerIndicatorState(),
                    data => data.CalculateEhlersHilbertTransformerIndicator().OutputValues["Real"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHomodyneDominantCycle",
                    () => new EhlersHomodyneDominantCycleState(),
                    data => data.CalculateEhlersHomodyneDominantCycle().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHpLpRoofingFilter",
                    () => new EhlersHpLpRoofingFilterState(),
                    data => data.CalculateEhlersHpLpRoofingFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersHurstCoefficient",
                    () => new EhlersHurstCoefficientState(),
                    data => data.CalculateEhlersHurstCoefficient().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersImpulseReaction",
                    () => new EhlersImpulseReactionState(),
                    data => data.CalculateEhlersImpulseReaction().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersImpulseResponse",
                    () => new EhlersImpulseResponseState(),
                    data => data.CalculateEhlersImpulseResponse().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersInfiniteImpulseResponseFilter",
                    () => new EhlersInfiniteImpulseResponseFilterState(),
                    data => data.CalculateEhlersInfiniteImpulseResponseFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersInstantaneousPhaseIndicator",
                    () => new EhlersInstantaneousPhaseIndicatorState(),
                    data => data.CalculateEhlersInstantaneousPhaseIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersInstantaneousTrendlineV1",
                    () => new EhlersInstantaneousTrendlineV1State(),
                    data => data.CalculateEhlersInstantaneousTrendlineV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersInstantaneousTrendlineV2",
                    () => new EhlersInstantaneousTrendlineV2State(),
                    data => data.CalculateEhlersInstantaneousTrendlineV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersInverseFisherTransform",
                    () => new EhlersInverseFisherTransformState(),
                    data => data.CalculateEhlersInverseFisherTransform().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersKaufmanAdaptiveMovingAverage",
                    () => new EhlersKaufmanAdaptiveMovingAverageState(),
                    data => data.CalculateEhlersKaufmanAdaptiveMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersStochasticCenterOfGravityOscillator",
                    () => new EhlersStochasticCenterOfGravityOscillatorState(),
                    data => data.CalculateEhlersStochasticCenterOfGravityOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersStochasticCyberCycle",
                    () => new EhlersStochasticCyberCycleState(),
                    data => data.CalculateEhlersStochasticCyberCycle().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersStochastic",
                    () => new EhlersStochasticState(),
                    data => data.CalculateEhlersStochastic().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSuperPassbandFilter",
                    () => new EhlersSuperPassbandFilterState(),
                    data => data.CalculateEhlersSuperPassbandFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSuperSmootherFilter",
                    () => new EhlersSuperSmootherFilterState(),
                    data => data.CalculateEhlersSuperSmootherFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSwissArmyKnifeIndicator",
                    () => new EhlersSwissArmyKnifeIndicatorState(),
                    data => data.CalculateEhlersSwissArmyKnifeIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersTrendExtraction",
                    () => new EhlersTrendExtractionState(),
                    data => data.CalculateEhlersTrendExtraction().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersTrendflexIndicator",
                    () => new EhlersTrendflexIndicatorState(),
                    data => data.CalculateEhlersTrendflexIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersTriangleMovingAverage",
                    () => new EhlersTriangleMovingAverageState(),
                    data => data.CalculateEhlersTriangleMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersTriangleWindowIndicator",
                    () => new EhlersTriangleWindowIndicatorState(),
                    data => data.CalculateEhlersTriangleWindowIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersTripleDelayLineDetrender",
                    () => new EhlersTripleDelayLineDetrenderState(),
                    data => data.CalculateEhlersTripleDelayLineDetrender().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersTruncatedBandPassFilter",
                    () => new EhlersTruncatedBandPassFilterState(),
                    data => data.CalculateEhlersTruncatedBandPassFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersUniversalOscillator",
                    () => new EhlersUniversalOscillatorState(),
                    data => data.CalculateEhlersUniversalOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersUniversalTradingFilter",
                    () => new EhlersUniversalTradingFilterState(),
                    data => data.CalculateEhlersUniversalTradingFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersVariableIndexDynamicAverage",
                    () => new EhlersVariableIndexDynamicAverageState(),
                    data => data.CalculateEhlersVariableIndexDynamicAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersVossPredictiveFilter",
                    () => new EhlersVossPredictiveFilterState(),
                    data => data.CalculateEhlersVossPredictiveFilter().OutputValues["Voss"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersZeroCrossingsDominantCycle",
                    () => new EhlersZeroCrossingsDominantCycleState(),
                    data => data.CalculateEhlersZeroCrossingsDominantCycle().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersZeroLagExponentialMovingAverage",
                    () => new EhlersZeroLagExponentialMovingAverageState(),
                    data => data.CalculateEhlersZeroLagExponentialMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersZeroMeanRoofingFilter",
                    () => new EhlersZeroMeanRoofingFilterState(),
                    data => data.CalculateEhlersZeroMeanRoofingFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersMotherOfAdaptiveMovingAverages",
                    () => new EhlersMotherOfAdaptiveMovingAveragesState(),
                    data => data.CalculateEhlersMotherOfAdaptiveMovingAverages().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersRoofingFilterV2",
                    () => new EhlersRoofingFilterV2State(),
                    data => data.CalculateEhlersRoofingFilterV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ElasticVolumeWeightedMovingAverageV1",
                    () => new ElasticVolumeWeightedMovingAverageV1State(),
                    data => data.CalculateElasticVolumeWeightedMovingAverageV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ElasticVolumeWeightedMovingAverageV2",
                    () => new ElasticVolumeWeightedMovingAverageV2State(),
                    data => data.CalculateElasticVolumeWeightedMovingAverageV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ElderMarketThermometer",
                    () => new ElderMarketThermometerState(),
                    data => data.CalculateElderMarketThermometer().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ElderSafeZoneStops",
                    () => new ElderSafeZoneStopsState(),
                    data => data.CalculateElderSafeZoneStops().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ElliottWaveOscillator",
                    () => new ElliottWaveOscillatorState(),
                    data => data.CalculateElliottWaveOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EmaWaveIndicator.Wa",
                    () => new EmaWaveIndicatorState(),
                    data => data.CalculateEmaWaveIndicator().OutputValues["Wa"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EndPointMovingAverage",
                    () => new EndPointMovingAverageState(),
                    data => data.CalculateEndPointMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EnhancedIndex",
                    () => new EnhancedIndexState(),
                    data => data.CalculateEnhancedIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EnhancedWilliamsR",
                    () => new EnhancedWilliamsRState(),
                    data => data.CalculateEnhancedWilliamsR().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EquityMovingAverage",
                    () => new EquityMovingAverageState(),
                    data => data.CalculateEquityMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ErgodicCandlestickOscillator",
                    () => new ErgodicCandlestickOscillatorState(),
                    data => data.CalculateErgodicCandlestickOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ErgodicCommoditySelectionIndex",
                    () => new ErgodicCommoditySelectionIndexState(),
                    data => data.CalculateErgodicCommoditySelectionIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ErgodicMeanDeviationIndicator",
                    () => new ErgodicMeanDeviationIndicatorState(),
                    data => data.CalculateErgodicMeanDeviationIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ErgodicMovingAverageConvergenceDivergence",
                    () => new ErgodicMovingAverageConvergenceDivergenceState(),
                    data => data.CalculateErgodicMovingAverageConvergenceDivergence().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ErgodicPercentagePriceOscillator",
                    () => new ErgodicPercentagePriceOscillatorState(),
                    data => data.CalculateErgodicPercentagePriceOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ErgodicTrueStrengthIndexV1",
                    () => new ErgodicTrueStrengthIndexV1State(),
                    data => data.CalculateErgodicTrueStrengthIndexV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ErgodicTrueStrengthIndexV2",
                    () => new ErgodicTrueStrengthIndexV2State(),
                    data => data.CalculateErgodicTrueStrengthIndexV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FallingRisingFilter",
                    () => new FallingRisingFilterState(),
                    data => data.CalculateFallingRisingFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FareySequenceWeightedMovingAverage",
                    () => new FareySequenceWeightedMovingAverageState(),
                    data => data.CalculateFareySequenceWeightedMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FastandSlowKurtosisOscillator",
                    () => new FastandSlowKurtosisOscillatorState(),
                    data => data.CalculateFastandSlowKurtosisOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FastandSlowRelativeStrengthIndexOscillator",
                    () => new FastandSlowRelativeStrengthIndexOscillatorState(),
                    data => data.CalculateFastandSlowRelativeStrengthIndexOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FastandSlowStochasticOscillator",
                    () => new FastandSlowStochasticOscillatorState(),
                    data => data.CalculateFastandSlowStochasticOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FastSlowDegreeOscillator",
                    () => new FastSlowDegreeOscillatorState(),
                    data => data.CalculateFastSlowDegreeOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FearAndGreedIndicator",
                    () => new FearAndGreedIndicatorState(),
                    data => data.CalculateFearAndGreedIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FibonacciPivotPoints.Pivot",
                    () => new FibonacciPivotPointsState(),
                    data => data.CalculateFibonacciPivotPoints().OutputValues["Pivot"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FibonacciRetrace.UpperBand",
                    () => new FibonacciRetraceState(),
                    data => data.CalculateFibonacciRetrace().OutputValues["UpperBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FibonacciWeightedMovingAverage",
                    () => new FibonacciWeightedMovingAverageState(),
                    data => data.CalculateFibonacciWeightedMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FiniteVolumeElements",
                    () => new FiniteVolumeElementsState(),
                    data => data.CalculateFiniteVolumeElements().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FireflyOscillator",
                    () => new FireflyOscillatorState(),
                    data => data.CalculateFireflyOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FisherLeastSquaresMovingAverage",
                    () => new FisherLeastSquaresMovingAverageState(),
                    data => data.CalculateFisherLeastSquaresMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FisherTransformStochasticOscillator",
                    () => new FisherTransformStochasticOscillatorState(),
                    data => data.CalculateFisherTransformStochasticOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FlaggingBands.MiddleBand",
                    () => new FlaggingBandsState(),
                    data => data.CalculateFlaggingBands().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FloorPivotPoints.Pivot",
                    () => new FloorPivotPointsState(),
                    data => data.CalculateFloorPivotPoints().OutputValues["Pivot"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FoldedRelativeStrengthIndex",
                    () => new FoldedRelativeStrengthIndexState(),
                    data => data.CalculateFoldedRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ForceIndex",
                    () => new ForceIndexState(),
                    data => data.CalculateForceIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ForecastOscillator",
                    () => new ForecastOscillatorState(),
                    data => data.CalculateForecastOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FractalChaosBands.MiddleBand",
                    () => new FractalChaosBandsState(),
                    data => data.CalculateFractalChaosBands().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FractalChaosOscillator",
                    () => new FractalChaosOscillatorState(),
                    data => data.CalculateFractalChaosOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FreedomOfMovement",
                    () => new FreedomOfMovementState(),
                    data => data.CalculateFreedomOfMovement().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FunctionToCandles.Close",
                    () => new FunctionToCandlesState(),
                    data => data.CalculateFunctionToCandles().OutputValues["Close"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("FXSniperIndicator",
                    () => new FXSniperIndicatorState(),
                    data => data.CalculateFXSniperIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GainLossMovingAverage",
                    () => new GainLossMovingAverageState(),
                    data => data.CalculateGainLossMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GannHiLoActivator",
                    () => new GannHiLoActivatorState(),
                    data => data.CalculateGannHiLoActivator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GannSwingOscillator",
                    () => new GannSwingOscillatorState(),
                    data => data.CalculateGannSwingOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GannTrendOscillator",
                    () => new GannTrendOscillatorState(),
                    data => data.CalculateGannTrendOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GatorOscillator.Top",
                    () => new GatorOscillatorState(),
                    data => data.CalculateGatorOscillator().OutputValues["Top"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GeneralFilterEstimator",
                    () => new GeneralFilterEstimatorState(),
                    data => data.CalculateGeneralFilterEstimator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GeneralizedDoubleExponentialMovingAverage",
                    () => new GeneralizedDoubleExponentialMovingAverageState(),
                    data => data.CalculateGeneralizedDoubleExponentialMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GOscillator",
                    () => new GOscillatorState(),
                    data => data.CalculateGOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GrandTrendForecasting",
                    () => new GrandTrendForecastingState(),
                    data => data.CalculateGrandTrendForecasting().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GroverLlorensActivator",
                    () => new GroverLlorensActivatorState(),
                    data => data.CalculateGroverLlorensActivator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GroverLlorensCycleOscillator",
                    () => new GroverLlorensCycleOscillatorState(),
                    data => data.CalculateGroverLlorensCycleOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GuppyCountBackLine",
                    () => new GuppyCountBackLineState(),
                    data => data.CalculateGuppyCountBackLine().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GuppyDistanceIndicator.FastDistance",
                    () => new GuppyDistanceIndicatorState(),
                    data => data.CalculateGuppyDistanceIndicator().OutputValues["FastDistance"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("GuppyMultipleMovingAverage",
                    () => new GuppyMultipleMovingAverageState(),
                    data => data.CalculateGuppyMultipleMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HalfTrend",
                    () => new HalfTrendState(),
                    data => data.CalculateHalfTrend().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HampelFilter",
                    () => new HampelFilterState(),
                    data => data.CalculateHampelFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HawkeyeVolumeIndicator.Up",
                    () => new HawkeyeVolumeIndicatorState(),
                    data => data.CalculateHawkeyeVolumeIndicator().OutputValues["Up"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HendersonWeightedMovingAverage",
                    () => new HendersonWeightedMovingAverageState(),
                    data => data.CalculateHendersonWeightedMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HerrickPayoffIndex",
                    () => new HerrickPayoffIndexState(),
                    data => data.CalculateHerrickPayoffIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HighLowIndex",
                    () => new HighLowIndexState(),
                    data => data.CalculateHighLowIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HirashimaSugitaRS.MiddleBand",
                    () => new HirashimaSugitaRSState(),
                    data => data.CalculateHirashimaSugitaRS().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HoltExponentialMovingAverage",
                    () => new HoltExponentialMovingAverageState(),
                    data => data.CalculateHoltExponentialMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HullEstimate",
                    () => new HullEstimateState(),
                    data => data.CalculateHullEstimate().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HurstBands.MiddleBand",
                    () => new HurstBandsState(),
                    data => data.CalculateHurstBands().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HurstCycleChannel.FastMiddleBand",
                    () => new HurstCycleChannelState(),
                    data => data.CalculateHurstCycleChannel().OutputValues["FastMiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("HybridConvolutionFilter",
                    () => new HybridConvolutionFilterState(),
                    data => data.CalculateHybridConvolutionFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("IchimokuCloud.TenkanSen",
                    () => new IchimokuCloudState(),
                    data => data.CalculateIchimokuCloud().OutputValues["TenkanSen"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("IIRLeastSquaresEstimate",
                    () => new IIRLeastSquaresEstimateState(),
                    data => data.CalculateIIRLeastSquaresEstimate().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ImpulseMovingAverageConvergenceDivergence.Macd",
                    () => new ImpulseMovingAverageConvergenceDivergenceState(),
                    data => data.CalculateImpulseMovingAverageConvergenceDivergence().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ImpulsePercentagePriceOscillator",
                    () => new ImpulsePercentagePriceOscillatorState(),
                    data => data.CalculateImpulsePercentagePriceOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("InertiaIndicator",
                    () => new InertiaIndicatorState(),
                    data => data.CalculateInertiaIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("InformationRatio",
                    () => new InformationRatioState(),
                    data => data.CalculateInformationRatio().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("InsyncIndex",
                    () => new InsyncIndexState(),
                    data => data.CalculateInsyncIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("InternalBarStrengthIndicator",
                    () => new InternalBarStrengthIndicatorState(),
                    data => data.CalculateInternalBarStrengthIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("InterquartileRangeBands.MiddleBand",
                    () => new InterquartileRangeBandsState(),
                    data => data.CalculateInterquartileRangeBands().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("InverseDistanceWeightedMovingAverage",
                    () => new InverseDistanceWeightedMovingAverageState(),
                    data => data.CalculateInverseDistanceWeightedMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("InverseFisherFastZScore",
                    () => new InverseFisherFastZScoreState(),
                    data => data.CalculateInverseFisherFastZScore().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("InverseFisherZScore",
                    () => new InverseFisherZScoreState(),
                    data => data.CalculateInverseFisherZScore().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("JapaneseCorrelationCoefficient",
                    () => new JapaneseCorrelationCoefficientState(),
                    data => data.CalculateJapaneseCorrelationCoefficient().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("JmaRsxClone",
                    () => new JmaRsxCloneState(),
                    data => data.CalculateJmaRsxClone().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("JrcFractalDimension",
                    () => new JrcFractalDimensionState(),
                    data => data.CalculateJrcFractalDimension().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("JsaMovingAverage",
                    () => new JsaMovingAverageState(),
                    data => data.CalculateJsaMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("JurikMovingAverage",
                    () => new JurikMovingAverageState(),
                    data => data.CalculateJurikMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KalmanSmoother",
                    () => new KalmanSmootherState(),
                    data => data.CalculateKalmanSmoother().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KarobeinOscillator",
                    () => new KarobeinOscillatorState(),
                    data => data.CalculateKarobeinOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KaseConvergenceDivergence",
                    () => new KaseConvergenceDivergenceState(),
                    data => data.CalculateKaseConvergenceDivergence().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KaseDevStopV1.Dev1",
                    () => new KaseDevStopV1State(),
                    data => data.CalculateKaseDevStopV1().OutputValues["Dev1"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KaseDevStopV2.Dev1",
                    () => new KaseDevStopV2State(),
                    data => data.CalculateKaseDevStopV2().OutputValues["Dev1"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KaseIndicator.KaseUp",
                    () => new KaseIndicatorState(),
                    data => data.CalculateKaseIndicator().OutputValues["KaseUp"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KasePeakOscillatorV1",
                    () => new KasePeakOscillatorV1State(),
                    data => data.CalculateKasePeakOscillatorV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KasePeakOscillatorV2",
                    () => new KasePeakOscillatorV2State(),
                    data => data.CalculateKasePeakOscillatorV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KaseSerialDependencyIndex.KsdiUp",
                    () => new KaseSerialDependencyIndexState(),
                    data => data.CalculateKaseSerialDependencyIndex().OutputValues["KsdiUp"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KaufmanAdaptiveBands.MiddleBand",
                    () => new KaufmanAdaptiveBandsState(),
                    data => data.CalculateKaufmanAdaptiveBands().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KaufmanAdaptiveCorrelationOscillator",
                    () => new KaufmanAdaptiveCorrelationOscillatorState(),
                    data => data.CalculateKaufmanAdaptiveCorrelationOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KaufmanAdaptiveLeastSquaresMovingAverage",
                    () => new KaufmanAdaptiveLeastSquaresMovingAverageState(),
                    data => data.CalculateKaufmanAdaptiveLeastSquaresMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KaufmanAdaptiveMovingAverage",
                    () => new KaufmanAdaptiveMovingAverageState(),
                    data => data.CalculateKaufmanAdaptiveMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KaufmanBinaryWave",
                    () => new KaufmanBinaryWaveState(),
                    data => data.CalculateKaufmanBinaryWave().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KendallRankCorrelationCoefficient",
                    () => new KendallRankCorrelationCoefficientState(),
                    data => data.CalculateKendallRankCorrelationCoefficient().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KirshenbaumBands.MiddleBand",
                    () => new KirshenbaumBandsState(),
                    data => data.CalculateKirshenbaumBands().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KlingerVolumeOscillator",
                    () => new KlingerVolumeOscillatorState(),
                    data => data.CalculateKlingerVolumeOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KnowSureThing",
                    () => new KnowSureThingState(),
                    data => data.CalculateKnowSureThing().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KurtosisIndicator",
                    () => new KurtosisIndicatorState(),
                    data => data.CalculateKurtosisIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("KwanIndicator",
                    () => new KwanIndicatorState(),
                    data => data.CalculateKwanIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LBRPaintBars.MiddleBand",
                    () => new LBRPaintBarsState(),
                    data => data.CalculateLBRPaintBars().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LeastSquaresMovingAverage",
                    () => new LeastSquaresMovingAverageState(),
                    data => data.CalculateLeastSquaresMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LeoMovingAverage",
                    () => new LeoMovingAverageState(),
                    data => data.CalculateLeoMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LightLeastSquaresMovingAverage",
                    () => new LightLeastSquaresMovingAverageState(),
                    data => data.CalculateLightLeastSquaresMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LindaRaschke3_10Oscillator",
                    () => new LindaRaschke3_10OscillatorState(),
                    data => data.CalculateLindaRaschke3_10Oscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LinearExtrapolation",
                    () => new LinearExtrapolationState(),
                    data => data.CalculateLinearExtrapolation().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LinearQuadraticConvergenceDivergenceOscillator",
                    () => new LinearQuadraticConvergenceDivergenceOscillatorState(),
                    data => data.CalculateLinearQuadraticConvergenceDivergenceOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LinearRegressionLine",
                    () => new LinearRegressionLineState(),
                    data => data.CalculateLinearRegressionLine().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LinearTrailingStop",
                    () => new LinearTrailingStopState(),
                    data => data.CalculateLinearTrailingStop().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LinearWeightedMovingAverage",
                    () => new LinearWeightedMovingAverageState(),
                    data => data.CalculateLinearWeightedMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LiquidRelativeStrengthIndex",
                    () => new LiquidRelativeStrengthIndexState(),
                    data => data.CalculateLiquidRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("LogisticCorrelation",
                    () => new LogisticCorrelationState(),
                    data => data.CalculateLogisticCorrelation().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MacZIndicator",
                    () => new MacZIndicatorState(),
                    data => data.CalculateMacZIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MacZVwapIndicator",
                    () => new MacZVwapIndicatorState(),
                    data => data.CalculateMacZVwapIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MarketDirectionIndicator",
                    () => new MarketDirectionIndicatorState(),
                    data => data.CalculateMarketDirectionIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MarketFacilitationIndex",
                    () => new MarketFacilitationIndexState(),
                    data => data.CalculateMarketFacilitationIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MarketMeannessIndex",
                    () => new MarketMeannessIndexState(MovingAvgType.SimpleMovingAverage, 100),
                    data => data.CalculateMarketMeannessIndex(MovingAvgType.SimpleMovingAverage, 100).CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MartinRatio",
                    () => new MartinRatioState(),
                    data => data.CalculateMartinRatio().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MassIndex",
                    () => new MassIndexState(),
                    data => data.CalculateMassIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MassThrustIndicator",
                    () => new MassThrustIndicatorState(),
                    data => data.CalculateMassThrustIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MassThrustOscillator",
                    () => new MassThrustOscillatorState(),
                    data => data.CalculateMassThrustOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MayerMultiple",
                    () => new MayerMultipleState(),
                    data => data.CalculateMayerMultiple().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("McClellanOscillator",
                    () => new McClellanOscillatorState(),
                    data => data.CalculateMcClellanOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("McGinleyDynamicIndicator",
                    () => new McGinleyDynamicIndicatorState(),
                    data => data.CalculateMcGinleyDynamicIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("McNichollMovingAverage",
                    () => new McNichollMovingAverageState(),
                    data => data.CalculateMcNichollMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MiddleHighLowMovingAverage",
                    () => new MiddleHighLowMovingAverageState(),
                    data => data.CalculateMiddleHighLowMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MidpointOscillator",
                    () => new MidpointOscillatorState(),
                    data => data.CalculateMidpointOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MirroredMovingAverageConvergenceDivergence",
                    () => new MirroredMovingAverageConvergenceDivergenceState(),
                    data => data.CalculateMirroredMovingAverageConvergenceDivergence().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MirroredPercentagePriceOscillator",
                    () => new MirroredPercentagePriceOscillatorState(),
                    data => data.CalculateMirroredPercentagePriceOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MobilityOscillator",
                    () => new MobilityOscillatorState(),
                    data => data.CalculateMobilityOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ModifiedGannHiloActivator",
                    () => new ModifiedGannHiloActivatorState(),
                    data => data.CalculateModifiedGannHiloActivator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ModifiedPriceVolumeTrend",
                    () => new ModifiedPriceVolumeTrendState(),
                    data => data.CalculateModifiedPriceVolumeTrend().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ModularFilter",
                    () => new ModularFilterState(),
                    data => data.CalculateModularFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MomentaRelativeStrengthIndex",
                    () => new MomentaRelativeStrengthIndexState(),
                    data => data.CalculateMomentaRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MomentumOscillator",
                    () => new MomentumOscillatorState(),
                    data => data.CalculateMomentumOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MorphedSineWave",
                    () => new MorphedSineWaveState(),
                    data => data.CalculateMorphedSineWave().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MotionSmoothnessIndex",
                    () => new MotionSmoothnessIndexState(),
                    data => data.CalculateMotionSmoothnessIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MotionToAttractionTrailingStop",
                    () => new MotionToAttractionTrailingStopState(),
                    data => data.CalculateMotionToAttractionTrailingStop().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MoveTracker",
                    () => new MoveTrackerState(),
                    data => data.CalculateMoveTracker().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MovingAverageAdaptiveFilter",        
                    () => new MovingAverageAdaptiveFilterState(),
                    data => data.CalculateMovingAverageAdaptiveFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MovingAverageAdaptiveQ",
                    () => new MovingAverageAdaptiveQState(),
                    data => data.CalculateMovingAverageAdaptiveQ().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MovingAverageBandWidth",
                    () => new MovingAverageBandWidthState(),
                    data => data.CalculateMovingAverageBandWidth().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MovingAverageConvergenceDivergenceLeader",
                    () => new MovingAverageConvergenceDivergenceLeaderState(),
                    data => data.CalculateMovingAverageConvergenceDivergenceLeader().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MovingAverageV3",
                    () => new MovingAverageV3State(),
                    data => data.CalculateMovingAverageV3().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MultiDepthZeroLagExponentialMovingAverage",
                    () => new MultiDepthZeroLagExponentialMovingAverageState(),
                    data => data.CalculateMultiDepthZeroLagExponentialMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MultiLevelIndicator",
                    () => new MultiLevelIndicatorState(),
                    data => data.CalculateMultiLevelIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MultiVoteOnBalanceVolume",
                    () => new MultiVoteOnBalanceVolumeState(),
                    data => data.CalculateMultiVoteOnBalanceVolume().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NarrowBandpassFilter",
                    () => new NarrowBandpassFilterState(),
                    data => data.CalculateNarrowBandpassFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NaturalDirectionalCombo",
                    () => new NaturalDirectionalComboState(),
                    data => data.CalculateNaturalDirectionalCombo().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NaturalDirectionalIndex",
                    () => new NaturalDirectionalIndexState(),
                    data => data.CalculateNaturalDirectionalIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NaturalMarketCombo",
                    () => new NaturalMarketComboState(),
                    data => data.CalculateNaturalMarketCombo().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NaturalMarketMirror",
                    () => new NaturalMarketMirrorState(),
                    data => data.CalculateNaturalMarketMirror().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NaturalMarketRiver",
                    () => new NaturalMarketRiverState(),
                    data => data.CalculateNaturalMarketRiver().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NaturalMarketSlope",
                    () => new NaturalMarketSlopeState(),
                    data => data.CalculateNaturalMarketSlope().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NaturalMovingAverage",
                    () => new NaturalMovingAverageState(),
                    data => data.CalculateNaturalMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NaturalStochasticIndicator",
                    () => new NaturalStochasticIndicatorState(),
                    data => data.CalculateNaturalStochasticIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NegativeVolumeDisparityIndicator",
                    () => new NegativeVolumeDisparityIndicatorState(),
                    data => data.CalculateNegativeVolumeDisparityIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NegativeVolumeIndex",
                    () => new NegativeVolumeIndexState(),
                    data => data.CalculateNegativeVolumeIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NickRypockTrailingReverse",
                    () => new NickRypockTrailingReverseState(),
                    data => data.CalculateNickRypockTrailingReverse().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NormalizedRelativeVigorIndex",
                    () => new NormalizedRelativeVigorIndexState(),
                    data => data.CalculateNormalizedRelativeVigorIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("NthOrderDifferencingOscillator",
                    () => new NthOrderDifferencingOscillatorState(),
                    data => data.CalculateNthOrderDifferencingOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("OceanIndicator",
                    () => new OceanIndicatorState(),
                    data => data.CalculateOceanIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("OCHistogram",
                    () => new OCHistogramState(),
                    data => data.CalculateOCHistogram().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("OmegaRatio",
                    () => new OmegaRatioState(),
                    data => data.CalculateOmegaRatio().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("OnBalanceVolumeDisparityIndicator",  
                    () => new OnBalanceVolumeDisparityIndicatorState(),
                    data => data.CalculateOnBalanceVolumeDisparityIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ZweigMarketBreadthIndicator",
                    () => new ZweigMarketBreadthIndicatorState(),
                    data => data.CalculateZweigMarketBreadthIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AverageDirectionalIndex",
                    () => new AverageDirectionalIndexState(),
                    data => data.CalculateAverageDirectionalIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("AverageTrueRange",
                    () => new AverageTrueRangeState(),
                    data => data.CalculateAverageTrueRange().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("ExponentialMovingAverage",
                    () => new ExponentialMovingAverageState(),
                    data => data.CalculateExponentialMovingAverage().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MoneyFlowIndex",
                    () => new MoneyFlowIndexState(),
                    data => data.CalculateMoneyFlowIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("MovingAverageConvergenceDivergence",
                    () => new MovingAverageConvergenceDivergenceState(),
                    data => data.CalculateMovingAverageConvergenceDivergence().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("OnBalanceVolume",
                    () => new OnBalanceVolumeState(),
                    data => data.CalculateOnBalanceVolume().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("OnBalanceVolumeModified",
                    () => new OnBalanceVolumeModifiedState(),
                    data => data.CalculateOnBalanceVolumeModified().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("OnBalanceVolumeReflex",
                    () => new OnBalanceVolumeReflexState(),
                    data => data.CalculateOnBalanceVolumeReflex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersLaguerreFilter",
                    () => new EhlersLaguerreFilterState(),
                    data => data.CalculateEhlersLaguerreFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersLaguerreRelativeStrengthIndex",
                    () => new EhlersLaguerreRelativeStrengthIndexState(),
                    data => data.CalculateEhlersLaguerreRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha",
                    () => new EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaState(),
                    data => data.CalculateEhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersLeadingIndicator",
                    () => new EhlersLeadingIndicatorState(),
                    data => data.CalculateEhlersLeadingIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersMarketStateIndicator",
                    () => new EhlersMarketStateIndicatorState(),
                    data => data.CalculateEhlersMarketStateIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersMedianAverageAdaptiveFilter",
                    () => new EhlersMedianAverageAdaptiveFilterState(),
                    data => data.CalculateEhlersMedianAverageAdaptiveFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersMesaPredictIndicatorV1",
                    () => new EhlersMesaPredictIndicatorV1State(),
                    data => data.CalculateEhlersMesaPredictIndicatorV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersMesaPredictIndicatorV2",
                    () => new EhlersMesaPredictIndicatorV2State(),
                    data => data.CalculateEhlersMesaPredictIndicatorV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersModifiedOptimumEllipticFilter",
                    () => new EhlersModifiedOptimumEllipticFilterState(),
                    data => data.CalculateEhlersModifiedOptimumEllipticFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersModifiedRelativeStrengthIndex",
                    () => new EhlersModifiedRelativeStrengthIndexState(),
                    data => data.CalculateEhlersModifiedRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersModifiedStochasticIndicator",
                    () => new EhlersModifiedStochasticIndicatorState(),
                    data => data.CalculateEhlersModifiedStochasticIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersMovingAverageDifferenceIndicator",
                    () => new EhlersMovingAverageDifferenceIndicatorState(),
                    data => data.CalculateEhlersMovingAverageDifferenceIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersNoiseEliminationTechnology",
                    () => new EhlersNoiseEliminationTechnologyState(),
                    data => data.CalculateEhlersNoiseEliminationTechnology().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersOptimumEllipticFilter",
                    () => new EhlersOptimumEllipticFilterState(),
                    data => data.CalculateEhlersOptimumEllipticFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersPhaseAccumulationDominantCycle",
                    () => new EhlersPhaseAccumulationDominantCycleState(),
                    data => data.CalculateEhlersPhaseAccumulationDominantCycle().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersPhaseCalculation",
                    () => new EhlersPhaseCalculationState(),
                    data => data.CalculateEhlersPhaseCalculation().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersRecursiveMedianFilter",        
                    () => new EhlersRecursiveMedianFilterState(),
                    data => data.CalculateEhlersRecursiveMedianFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("BollingerBands.MiddleBand",
                    () => new BollingerBandsState(),
                    data => data.CalculateBollingerBands().OutputValues["MiddleBand"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersRecursiveMedianOscillator",
                    () => new EhlersRecursiveMedianOscillatorState(),
                    data => data.CalculateEhlersRecursiveMedianOscillator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersReflexIndicator",
                    () => new EhlersReflexIndicatorState(),
                    data => data.CalculateEhlersReflexIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersRelativeStrengthIndexInverseFisherTransform",
                    () => new EhlersRelativeStrengthIndexInverseFisherTransformState(),
                    data => data.CalculateEhlersRelativeStrengthIndexInverseFisherTransform().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersRelativeVigorIndex",
                    () => new EhlersRelativeVigorIndexState(),
                    data => data.CalculateEhlersRelativeVigorIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersRestoringPullIndicator",
                    () => new EhlersRestoringPullIndicatorState(),
                    data => data.CalculateEhlersRestoringPullIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersReverseExponentialMovingAverageIndicatorV1",
                    () => new EhlersReverseExponentialMovingAverageIndicatorV1State(),
                    data => data.CalculateEhlersReverseExponentialMovingAverageIndicatorV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersReverseExponentialMovingAverageIndicatorV2.EremaCycle",
                    () => new EhlersReverseExponentialMovingAverageIndicatorV2State(),
                    data => data.CalculateEhlersReverseExponentialMovingAverageIndicatorV2().OutputValues["EremaCycle"])
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersRocketRelativeStrengthIndex",
                    () => new EhlersRocketRelativeStrengthIndexState(),
                    data => data.CalculateEhlersRocketRelativeStrengthIndex().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersRoofingFilterIndicator",
                    () => new EhlersRoofingFilterIndicatorState(),
                    data => data.CalculateEhlersRoofingFilterIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersRoofingFilterV1",
                    () => new EhlersRoofingFilterV1State(),
                    data => data.CalculateEhlersRoofingFilterV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSignalToNoiseRatioV1",
                    () => new EhlersSignalToNoiseRatioV1State(),
                    data => data.CalculateEhlersSignalToNoiseRatioV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSignalToNoiseRatioV2",
                    () => new EhlersSignalToNoiseRatioV2State(),
                    data => data.CalculateEhlersSignalToNoiseRatioV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSimpleClipIndicator",
                    () => new EhlersSimpleClipIndicatorState(),
                    data => data.CalculateEhlersSimpleClipIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSimpleCycleIndicator",
                    () => new EhlersSimpleCycleIndicatorState(),
                    data => data.CalculateEhlersSimpleCycleIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSimpleDecycler",
                    () => new EhlersSimpleDecyclerState(),
                    data => data.CalculateEhlersSimpleDecycler().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSimpleDerivIndicator",
                    () => new EhlersSimpleDerivIndicatorState(),
                    data => data.CalculateEhlersSimpleDerivIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSimpleWindowIndicator",
                    () => new EhlersSimpleWindowIndicatorState(),
                    data => data.CalculateEhlersSimpleWindowIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSineWaveIndicatorV1",
                    () => new EhlersSineWaveIndicatorV1State(),
                    data => data.CalculateEhlersSineWaveIndicatorV1().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSineWaveIndicatorV2",
                    () => new EhlersSineWaveIndicatorV2State(),
                    data => data.CalculateEhlersSineWaveIndicatorV2().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSmoothedAdaptiveMomentumIndicator",
                    () => new EhlersSmoothedAdaptiveMomentumIndicatorState(),
                    data => data.CalculateEhlersSmoothedAdaptiveMomentum().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSnakeUniversalTradingFilter",
                    () => new EhlersSnakeUniversalTradingFilterState(),
                    data => data.CalculateEhlersSnakeUniversalTradingFilter().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSpearmanRankIndicator",
                    () => new EhlersSpearmanRankIndicatorState(),
                    data => data.CalculateEhlersSpearmanRankIndicator().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSpectrumDerivedFilterBank",
                    () => new EhlersSpectrumDerivedFilterBankState(),
                    data => data.CalculateEhlersSpectrumDerivedFilterBank().CustomValuesList)
            };
            yield return new object[]
            {
                new StatefulIndicatorSpec("EhlersSquelchIndicator",
                    () => new EhlersSquelchIndicatorState(),
                    data => data.CalculateEhlersSquelchIndicator().CustomValuesList)
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

    [Fact]
    public void DynamicPivotPointsUsesAggregatedBars()
    {
        var data = StockTestData;
        data.Should().NotBeNullOrEmpty();

        var batchData = new StockData(data);
        var (inputList, highList, lowList, openList, volumeList) =
            CalculationsHelper.GetInputValuesList(batchData, InputLength.Day);
        var batchValues = batchData.CalculateDynamicPivotPoints(InputLength.Day).OutputValues["Pivot"];
        batchValues.Should().NotBeNullOrEmpty();

        var count = Math.Min(batchValues.Count, inputList.Count);
        count.Should().BeGreaterThan(0);

        var state = new DynamicPivotPointsState();
        var streamingValues = new List<double>(count);
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < count; i++)
        {
            var start = baseTime.AddDays(i);
            var end = start.AddDays(1);
            var bar = new OhlcvBar("AAPL", BarTimeframe.Days(1), start, end,
                openList[i], highList[i], lowList[i], inputList[i], volumeList[i], isFinal: true);
            var result = state.Update(bar, isFinal: true, includeOutputs: false);
            streamingValues.Add(result.Value);
        }

        for (var i = 0; i < count; i++)
        {
            AssertEqual(batchValues[i], streamingValues[i], "DynamicPivotPoints", i);
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
