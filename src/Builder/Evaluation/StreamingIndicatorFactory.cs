using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Factory for creating streaming indicator states.
/// </summary>
internal static class StreamingIndicatorFactory
{
    /// <summary>
    /// Creates a streaming indicator state for the given spec.
    /// </summary>
    public static IStreamingIndicatorState? CreateState(IndicatorSpec spec)
    {
        if (spec.Options is TrendDetectionSpecOptions or TrendDetectionIndexSpecOptions or TrendTriggerFactorSpecOptions or StochasticCustomOscillatorSpecOptions or StochasticMomentumIndexSpecOptions or MarketMeannessIndexSpecOptions or PzoSpecOptions or PriceZoneOscillatorSpecOptions or NviSpecOptions or PviSpecOptions or NegativeVolumeIndexSpecOptions or PositiveVolumeIndexSpecOptions or TradeVolumeIndexSpecOptions or PvtSpecOptions or PriceVolumeTrendSpecOptions or VolumePriceTrendSpecOptions or ModifiedPriceVolumeTrendSpecOptions or EaseOfMovementSpecOptions or EmvSpecOptions or ForceIndexSpecOptions or ElderForceIndexSpecOptions or VortexPositiveSpecOptions or VortexNegativeSpecOptions or VortexPlusSpecOptions or VortexMinusSpecOptions or VortexIndicatorPlusSpecOptions or VortexIndicatorMinusSpecOptions or VolumeAccumulationPercentSpecOptions or TwiggsMoneyFlowSpecOptions or AccumulationDistributionLineSpecOptions or ChaikinOscillatorSpecOptions or WilliamsAccumulationDistributionSpecOptions or SmoothedWilliamsAccumulationDistributionSpecOptions or UlcerIndexSpecOptions or MartinRatioSpecOptions or SortinoRatioSpecOptions or SharpeRatioSpecOptions or InformationRatioSpecOptions or TrendIntensityIndexSpecOptions or PriceVolumeOscillatorSpecOptions or ShinoharaIntensityRatioASpecOptions or ShinoharaIntensityRatioBSpecOptions or RelativeVolumeIndicatorSpecOptions or UpsideDownsideVolumeSpecOptions or TFSVolumeOscillatorSpecOptions or VolumeAccumulationOscillatorSpecOptions or TreynorRatioSpecOptions or OmegaRatioSpecOptions or UpsidePotentialRatioSpecOptions or TrendDirectionForceIndexSpecOptions or ReversalPointsSpecOptions or DemarkPressureRatioV1SpecOptions or DemarkPressureRatioV2SpecOptions or ElderMarketThermometerSpecOptions or LinearRegressionLineSpecOptions or RegressionOscillatorSpecOptions or KendallRankCorrelationCoefficientSpecOptions or LogisticCorrelationSpecOptions or EhlersCorrelationTrendIndicatorSpecOptions or EhlersCorrelationCycleIndicatorSpecOptions or EhlersCorrelationAngleIndicatorSpecOptions or EhlersMarketStateIndicatorSpecOptions or EmaWaveIndicatorSpecOptions or ErgodicMeanDeviationIndicatorSpecOptions or TraderPressureIndexSpecOptions or ZScoreSpecOptions or FastZScoreSpecOptions or InverseFisherZScoreSpecOptions or InverseFisherFastZScoreSpecOptions or FisherTransformSpecOptions or EhlersFisherTransformSpecOptions or CciSpecOptions or WoodieCommodityChannelIndexSpecOptions or EhlersCommodityChannelIndexInverseFisherTransformSpecOptions or EhlersInverseFisherTransformSpecOptions or InverseFisherTransformCoreSpecOptions or EhlersRelativeStrengthIndexInverseFisherTransformSpecOptions or QuasiWhiteNoiseSpecOptions or ConnorsRsiSpecOptions or ConnorsRelativeStrengthIndexSpecOptions or StochasticConnorsRelativeStrengthIndexSpecOptions or CCTStochRelativeStrengthIndexSpecOptions or CCTStochRSISpecOptions or StochRsiSpecOptions or StochasticRsiOscillatorSpecOptions or StochasticRelativeStrengthIndexSpecOptions or ApirineSlowRelativeStrengthIndexSpecOptions or SelfAdjustingRelativeStrengthIndexSpecOptions or AdaptiveRelativeStrengthIndexSpecOptions or AdaptiveRsiSpecOptions or FoldedRelativeStrengthIndexSpecOptions or RapidRelativeStrengthIndexSpecOptions or AsymmetricalRelativeStrengthIndexSpecOptions or AsymmetricalRsiSpecOptions or DoubleSmoothedRelativeStrengthIndexSpecOptions or MomentaRelativeStrengthIndexSpecOptions or RelativeMomentumIndexSpecOptions or IntradayMomentumIndexSpecOptions or ChandeIntradayMomentumIndexSpecOptions or PriceMomentumOscillatorSpecOptions or PmoSpecOptions or DecisionPointPriceMomentumOscillatorSpecOptions or CoppockCurveSpecOptions or SmoothedRateOfChangeSpecOptions or SmoothedRocSpecOptions or KnowSureThingSpecOptions or KstSpecOptions or PringSpecialKSpecOptions or SpecialKSpecOptions or SmoothedDeltaRatioOscillatorSpecOptions or DoubleSmoothedMomentaSpecOptions or DirectionalTrendIndexSpecOptions or OscOscillatorSpecOptions or WamiOscillatorSpecOptions or TsiSpecOptions or TrueStrengthIndexSpecOptions or ErgodicTrueStrengthIndexV1SpecOptions or ErgodicTrueStrengthIndexV2SpecOptions or
            EndPointMovingAverageSpecOptions or SharpModifiedMovingAverageSpecOptions or
            HarmonicMeanMovingAverageSpecOptions or
            CloseToCloseVolatilitySpecOptions or ParkinsonVolatilitySpecOptions or
            GarmanKlassVolatilitySpecOptions or RogersSatchellVolatilitySpecOptions or YangZhangVolatilitySpecOptions or
            HistoricalVolatilitySpecOptions or KaseSerialDependencyIndexSpecOptions or
            HistoricalVolatilityPercentileSpecOptions or
            JapaneseCorrelationCoefficientSpecOptions or
            OceanIndicatorSpecOptions or
            MayerMultipleSpecOptions or
            HybridConvolutionFilterSpecOptions or
            EhlersSpectrumDerivedFilterBankSpecOptions or
            EhlersRestoringPullIndicatorSpecOptions or
            EhlersGaussianFilterSpecOptions or
            EhlersFMDemodulatorIndicatorSpecOptions or
            ElderImpulseSystemSpecOptions or
            GannHiLoActivatorSpecOptions or
            LinearChannelsSpecOptions or
            LinearTrailingStopSpecOptions or
            SupportResistanceSpecOptions or
            TimePriceIndicatorSpecOptions or
            TrendExhaustionIndicatorSpecOptions or
            VanillaABCDPatternSpecOptions or
            VixTradingSystemSpecOptions or
            VostroIndicatorSpecOptions or
            UhlMaCrossoverSystemSpecOptions or
            RelativeDifferenceOfSquaresOscillatorSpecOptions or
            NaturalDirectionalComboSpecOptions or
            NaturalDirectionalIndexSpecOptions or
            NaturalStochasticIndicatorSpecOptions or
            NaturalMarketMirrorSpecOptions or
            NaturalMarketRiverSpecOptions or
            NaturalMarketComboSpecOptions or
            NaturalMarketSlopeSpecOptions or
            McClellanOscillatorSpecOptions or
            DecisionPointBreadthSwenlinTradingOscillatorSpecOptions or
            ZweigMarketBreadthIndicatorSpecOptions or
            TickLineMomentumOscillatorSpecOptions or
            ConditionalAccumulatorSpecOptions or
            ContractHighSpecOptions or
            ContractLowSpecOptions or
            DemarkReversalPointsSpecOptions or
            DemarkSetupIndicatorSpecOptions or
            FractalChaosOscillatorSpecOptions or
            Dema2LinesSpecOptions or
            KeltnerChannelMiddleSpecOptions or
            SpearmanIndicatorSpecOptions or
            PriceVolumeRankSpecOptions or
            EhlersNoiseEliminationTechnologySpecOptions or
            EhlersSpearmanRankIndicatorSpecOptions or
            SentimentZoneOscillatorSpecOptions or
            TotalPowerIndicatorSpecOptions or
            TrendPersistenceRateSpecOptions or
            GuppyCountBackLineSpecOptions or
            GOscillatorSpecOptions or
            MultiVoteOnBalanceVolumeSpecOptions)
            return StatefulIndicatorFactory.Create(spec);

        return spec.Name switch
        {
            IndicatorName.ReverseMovingAverageConvergenceDivergence when spec.Options is ReverseMovingAverageConvergenceDivergenceSpecOptions reverse => new ReverseMovingAverageConvergenceDivergenceState(fastLength: reverse.FastLength, slowLength: reverse.SlowLength, macdLevel: reverse.MacdLevel),
            IndicatorName.MovingAverageConvergenceDivergenceLeader when spec.Options is MovingAverageConvergenceDivergenceLeaderSpecOptions leader => new MovingAverageConvergenceDivergenceLeaderState(leader.MaType, leader.FastLength, leader.SlowLength, leader.SignalLength),
            IndicatorName.PercentagePriceOscillatorLeader when spec.Options is PercentagePriceOscillatorLeaderSpecOptions leader => new PercentagePriceOscillatorLeaderState(signalLength: leader.Length),
            IndicatorName.TFSMboIndicator when spec.Options is TFSMboIndicatorSpecOptions tfs => new TFSMboIndicatorState(tfs.MaType, tfs.FastLength, tfs.SlowLength, tfs.SignalLength),
            IndicatorName.TFSMboPercentagePriceOscillator when spec.Options is TFSMboPercentagePriceOscillatorSpecOptions tfs => new TFSMboPercentagePriceOscillatorState(tfs.MaType),
            IndicatorName.StochasticMovingAverageConvergenceDivergenceOscillator when spec.Options is StochasticMacdOscillatorSpecOptions stochastic => new StochasticMovingAverageConvergenceDivergenceOscillatorState(length: stochastic.Length),
            IndicatorName.ImpulseMovingAverageConvergenceDivergence when spec.Options is ImpulseMovingAverageConvergenceDivergenceSpecOptions impulse => new ImpulseMovingAverageConvergenceDivergenceState(impulse.MaType, impulse.Length, impulse.SignalLength),
            IndicatorName.ImpulsePercentagePriceOscillator when spec.Options is ImpulsePercentagePriceOscillatorSpecOptions impulse => new ImpulsePercentagePriceOscillatorState(length: impulse.Length),
            IndicatorName.AwesomeOscillator when spec.Options is AwesomeOscillatorSpecOptions awesome => new AwesomeOscillatorState(fastLength: awesome.Length, maType: awesome.MaType),
            IndicatorName.AcceleratorOscillator when spec.Options is AcceleratorOscillatorSpecOptions accelerator => new AcceleratorOscillatorState(fastLength: accelerator.Length, maType: accelerator.MaType),
            IndicatorName.TrendForceHistogram when spec.Options is TrendForceHistogramSpecOptions trendForce => new TrendForceHistogramState(trendForce.Length),
            IndicatorName.DetrendedSyntheticPrice when spec.Options is DetrendedSyntheticPriceSpecOptions synthetic => new DetrendedSyntheticPriceState(synthetic.Length),
            IndicatorName.TrendImpulseFilter when spec.Options is TrendImpulseFilterSpecOptions impulse => new TrendImpulseFilterState(impulse.MaType, impulse.Length1, impulse.Length2),
            IndicatorName.MarketFacilitationIndex => new MarketFacilitationIndexState(),
            IndicatorName.TrueRange => new TrueRangeState(),
            IndicatorName.Range => new RangeState(),
            IndicatorName.MoneyFlowIndex when spec.Options is MfiSpecOptions mfi
                => new MoneyFlowIndexState(mfi.Length),
            IndicatorName.MoneyFlowIndex when spec.Options is MfiCoreSpecOptions mfiCore
                => new MoneyFlowIndexState(mfiCore.Length),
            IndicatorName.LogReturns when spec.Options is LogReturnsSpecOptions logReturns
                => new LogReturnsState(logReturns.Length),
            IndicatorName.TFSTetherLineIndicator when spec.Options is TFSTetherLineIndicatorSpecOptions tether => new TFSTetherLineIndicatorState(tether.Length),
            IndicatorName.TFSTetherLineIndicator when spec.Options is TFSTetherLineSpecOptions tetherAlias => new TFSTetherLineIndicatorState(tetherAlias.Length),
            IndicatorName.RangeIdentifier when spec.Options is RangeIdentifierSpecOptions rangeIdentifier
                => new RangeIdentifierState(rangeIdentifier.Length),
            IndicatorName.MovingAverageEnvelope when spec.Options is MovingAverageEnvelopeSpecOptions envelope
                => new MovingAverageEnvelopeState(envelope.MaType, envelope.Length, envelope.Pct),
            IndicatorName.PriceChannel when spec.Options is PriceChannelSpecOptions priceChannel
                => new PriceChannelState(priceChannel.MaType, priceChannel.Length, priceChannel.Pct),
            IndicatorName.PriceChannel when spec.Options is PriceChannelMiddleSpecOptions priceMiddle => new PriceChannelState(length: priceMiddle.Length),
            IndicatorName.PriceChannel when spec.Options is PriceChannelUpperSpecOptions priceUpper => new PriceChannelState(length: priceUpper.Length),
            IndicatorName.PriceChannel when spec.Options is PriceChannelLowerSpecOptions priceLower => new PriceChannelState(length: priceLower.Length),
            IndicatorName.MoveTracker => new MoveTrackerState(),
            IndicatorName.AverageDayRange when spec.Options is AdrSpecOptions adr
                => new AverageDayRangeState(adr.Length),
            IndicatorName.AverageDayRange when spec.Options is AverageDayRangeSpecOptions dayRange
                => new AverageDayRangeState(dayRange.Length),
            IndicatorName.OnBalanceVolume when spec.Options is ObvSpecOptions obv
                => new OnBalanceVolumeState(obv.Length, obv.MaType),
            IndicatorName.OnBalanceVolume when spec.Options is OnBalanceVolumeSpecOptions obvExpanded
                => new OnBalanceVolumeState(obvExpanded.Length, obvExpanded.MaType),
            IndicatorName.VolumeZoneOscillator when spec.Options is VolumeZoneOscillatorSpecOptions zone
                => new VolumeZoneOscillatorState(zone.Length),
            IndicatorName.NormalizedMacd when spec.Options is NormalizedMacdSpecOptions normalizedMacd => new NormalizedMacdState(normalizedMacd.FastLength, normalizedMacd.SlowLength),
            IndicatorName.VolumeOscillator when spec.Options is VolumeOscillatorSpecOptions volumeOscillator => new VolumeOscillatorState(slowLength: volumeOscillator.Length),
            IndicatorName.VolumeMomentumOscillator when spec.Options is VolumeMomentumOscillatorSpecOptions volumeMomentum => new VolumeMomentumOscillatorState(volumeMomentum.ShortLength, volumeMomentum.LongLength),
            IndicatorName.ElliottWaveOscillator when spec.Options is ElliottWaveOscillatorSpecOptions elliott => new ElliottWaveOscillatorState(elliott.MaType, elliott.FastLength, elliott.SlowLength),
            IndicatorName.ErgodicMovingAverageConvergenceDivergence when spec.Options is ErgodicMovingAverageConvergenceDivergenceSpecOptions ergodic => new ErgodicMovingAverageConvergenceDivergenceState(ergodic.MaType, ergodic.Length1, ergodic.Length2, ergodic.Length3),
            IndicatorName.ErgodicPercentagePriceOscillator when spec.Options is ErgodicPercentagePriceOscillatorSpecOptions ergodicPpo => new ErgodicPercentagePriceOscillatorState(ergodicPpo.MaType, length2: ergodicPpo.Length),
            IndicatorName.MirroredMovingAverageConvergenceDivergence when spec.Options is MirroredMovingAverageConvergenceDivergenceSpecOptions mirrorMacd => new MirroredMovingAverageConvergenceDivergenceState(mirrorMacd.MaType, mirrorMacd.Length, mirrorMacd.SignalLength),
            IndicatorName.MirroredPercentagePriceOscillator when spec.Options is MirroredPercentagePriceOscillatorSpecOptions mirrorPpo => new MirroredPercentagePriceOscillatorState(mirrorPpo.MaType, mirrorPpo.Length, mirrorPpo.SignalLength),
            IndicatorName.LindaRaschke3_10Oscillator when spec.Options is LindaRaschke310OscillatorSpecOptions linda => new LindaRaschke3_10OscillatorState(linda.MaType, linda.FastLength, linda.SlowLength, linda.SmoothLength),
            IndicatorName._4MovingAverageConvergenceDivergence when spec.Options is _4MovingAverageConvergenceDivergenceSpecOptions four_4MovingAverageConvergenceDivergence => new _4MovingAverageConvergenceDivergenceState(four_4MovingAverageConvergenceDivergence.MaType, four_4MovingAverageConvergenceDivergence.Length1, four_4MovingAverageConvergenceDivergence.Length2, four_4MovingAverageConvergenceDivergence.Length3, four_4MovingAverageConvergenceDivergence.Length4, four_4MovingAverageConvergenceDivergence.Length5, four_4MovingAverageConvergenceDivergence.Length6, four_4MovingAverageConvergenceDivergence.BlueMult, four_4MovingAverageConvergenceDivergence.YellowMult),
            IndicatorName._4PercentagePriceOscillator when spec.Options is _4PercentagePriceOscillatorSpecOptions four_4PercentagePriceOscillator => new _4PercentagePriceOscillatorState(four_4PercentagePriceOscillator.MaType, four_4PercentagePriceOscillator.Length1, four_4PercentagePriceOscillator.Length2, four_4PercentagePriceOscillator.Length3, four_4PercentagePriceOscillator.Length4, four_4PercentagePriceOscillator.Length5, four_4PercentagePriceOscillator.Length6, four_4PercentagePriceOscillator.BlueMult, four_4PercentagePriceOscillator.YellowMult),
            IndicatorName.DiNapoliMovingAverageConvergenceDivergence when spec.Options is DiNapoliMovingAverageConvergenceDivergenceSpecOptions diNapoliMacd => new DiNapoliMovingAverageConvergenceDivergenceState(diNapoliMacd.Lc, diNapoliMacd.Sc, diNapoliMacd.Sp),
            IndicatorName.DiNapoliPercentagePriceOscillator when spec.Options is DiNapoliPercentagePriceOscillatorSpecOptions => new DiNapoliPercentagePriceOscillatorState(),
            IndicatorName.DidiIndex when spec.Options is DidiIndexSpecOptions didi => new DidiIndexState(didi.MaType, didi.ShortLength, didi.MediumLength, didi.LongLength),
            IndicatorName.CumulativeSum => new CumulativeSumState(),
            IndicatorName.PercentageVolumeOscillator when spec.Options is PvoSpecOptions pvo => new PercentageVolumeOscillatorState(pvo.MaType, pvo.Length, 26, pvo.SignalLength),
            IndicatorName.PercentageVolumeOscillator when spec.Options is PercentageVolumeOscillatorSpecOptions pvo2 => new PercentageVolumeOscillatorState(pvo2.MaType, pvo2.FastLength, pvo2.SlowLength, pvo2.SignalLength),
            IndicatorName.PercentagePriceOscillator when spec.Options is PpoSpecOptions ppo => new PercentagePriceOscillatorState(ppo.MaType, ppo.FastLength, ppo.SlowLength, ppo.SignalLength),
            IndicatorName.PercentagePriceOscillator when spec.Options is PercentagePriceOscillatorSpecOptions ppo2 => new PercentagePriceOscillatorState(ppo2.MaType, ppo2.FastLength, ppo2.SlowLength, ppo2.SignalLength),
            IndicatorName.PercentagePriceOscillator when spec.Options is PriceOscillatorPercentSpecOptions percentPpo => new PercentagePriceOscillatorState(percentPpo.MaType, percentPpo.ShortLength, percentPpo.LongLength, percentPpo.SignalLength),
            IndicatorName.DisparityIndex when spec.Options is DisparityIndexSpecOptions disparity => new DisparityIndexState(disparity.MaType, disparity.Length),
            IndicatorName.PerformanceIndex when spec.Options is PerformanceIndexSpecOptions performance => new PerformanceIndexState(performance.Length),
            IndicatorName.PerformanceIndex when spec.Options is PercentChangeSpecOptions percent => new PerformanceIndexState(percent.Length),
            IndicatorName.PerformanceIndex when spec.Options is PercentageChangeSpecOptions percentage => new PerformanceIndexState(percentage.Length),
            IndicatorName.PercentageTrailingStops when spec.Options is PercentageTrailingStopsSpecOptions stops => new PercentageTrailingStopsState(stops.Length, stops.Pct),
            IndicatorName.NickRypockTrailingReverse when spec.Options is NickRypockTrailingReverseSpecOptions reversal => new NickRypockTrailingReverseState(reversal.Length),
            IndicatorName.QmaSmaDifference when spec.Options is QmaSmaDifferenceSpecOptions spread => new QmaSmaDifferenceState(spread.Length),
            IndicatorName.MovingAverageDisplacedEnvelope when spec.Options is MovingAverageDisplacedEnvelopeSpecOptions envelope => new MovingAverageDisplacedEnvelopeState(envelope.MaType, envelope.Length1, envelope.Length2, envelope.Pct),
            IndicatorName.ChandeForecastOscillator when spec.Options is ChandeForecastOscillatorSpecOptions forecast => new ChandeForecastOscillatorState(forecast.Length),
            IndicatorName.MeanAbsoluteErrorBands when spec.Options is MeanAbsoluteErrorBandsSpecOptions mae => new MeanAbsoluteErrorBandsState(mae.StdDevFactor, mae.MaType, mae.Length),
            IndicatorName.MeanAbsoluteDeviationBands when spec.Options is MeanAbsoluteDeviationBandsSpecOptions mad => new MeanAbsoluteDeviationBandsState(mad.StdDevFactor, mad.MaType, mad.Length),
            IndicatorName.InterquartileRangeBands when spec.Options is InterquartileRangeBandsSpecOptions quartile => new InterquartileRangeBandsState(quartile.Length, quartile.Mult),
            IndicatorName.RangeBands when spec.Options is RangeBandsSpecOptions range => new RangeBandsState(range.StdDevFactor, range.MaType, range.Length),
            IndicatorName.MidpointOscillator when spec.Options is MidpointOscillatorSpecOptions midpoint => new MidpointOscillatorState(midpoint.MaType, midpoint.Length),
            IndicatorName.GuppyDistanceIndicator when spec.Options is GuppyDistanceIndicatorSpecOptions gdi => new GuppyDistanceIndicatorState(gdi.MaType, gdi.Length1, gdi.Length2, gdi.Length3, gdi.Length4, gdi.Length5, gdi.Length6, gdi.Length7, gdi.Length8, gdi.Length9, gdi.Length10, gdi.Length11, gdi.Length12),
            IndicatorName.GuppyMultipleMovingAverage when spec.Options is GuppyMultipleMovingAverageSpecOptions gmma => new GuppyMultipleMovingAverageState(gmma.MaType, gmma.Length1, gmma.Length2, gmma.Length3, gmma.Length4, gmma.Length5, gmma.Length6, gmma.Length7, gmma.Length8, gmma.Length9, gmma.Length10, gmma.Length11, gmma.Length12, gmma.Length13, gmma.Length14, gmma.Length15, gmma.Length16, gmma.Length17, gmma.Length18, gmma.Length19, gmma.Length20, gmma.Length21, gmma.Length22, gmma.Length23, gmma.Length24, gmma.Length25, gmma.Length26, gmma.Length27),
            IndicatorName.TypicalPriceVolatility when spec.Options is TypicalPriceVolatilitySpecOptions typical => new TypicalPriceVolatilityState(typical.Length),
            IndicatorName.DownsideDeviation when spec.Options is DownsideDeviationSpecOptions downside => new DownsideDeviationState(downside.Length),
            IndicatorName.CoefficientOfVariation when spec.Options is CoefficientOfVariationSpecOptions coefficient => new CoefficientOfVariationState(coefficient.Length),
            IndicatorName.Skewness when spec.Options is SkewnessSpecOptions skew => new SkewnessState(skew.Length),
            IndicatorName.StandardDeviationChannel when spec.Options is StandardDeviationChannelSpecOptions deviationChannel => new StandardDeviationChannelState(deviationChannel.Length),
            IndicatorName.RSquared when spec.Options is RSquaredSpecOptions squared => new RSquaredState(squared.Length),
            IndicatorName.LinearRegression when spec.Options is LinRegSpecOptions linreg => new LinearRegressionState(linreg.Length),
            IndicatorName.LinearRegression when spec.Options is LinearChannelMiddleSpecOptions channelMiddle => new LinearRegressionState(channelMiddle.Length),
            IndicatorName.LinearRegression when spec.Options is LinRegSlopeSpecOptions linSlope => new LinearRegressionState(linSlope.Length),
            IndicatorName.LinearRegression when spec.Options is LinRegInterceptSpecOptions linIntercept => new LinearRegressionState(linIntercept.Length),
            IndicatorName.LinearRegression when spec.Options is LinearRegressionSlopeSpecOptions linearSlope => new LinearRegressionState(linearSlope.Length),
            IndicatorName.LinearRegression when spec.Options is LinearRegressionInterceptSpecOptions linearIntercept => new LinearRegressionState(linearIntercept.Length),
            IndicatorName.StandardError when spec.Options is StandardErrorSpecOptions error => new StandardErrorState(error.Length),
            IndicatorName.StandardErrorOfTheMean when spec.Options is StandardErrorCoreSpecOptions meanError => new StandardErrorOfTheMeanState(meanError.Length),
            IndicatorName.Variance when spec.Options is VarianceSpecOptions variance => new VarianceState(variance.Length),
            IndicatorName.CumulativeVolumeIndex => new CumulativeVolumeIndexState(),
            IndicatorName.HighLowIndex when spec.Options is HighLowIndexSpecOptions highLow
                => new HighLowIndexState(highLow.MaType, highLow.Length),
            IndicatorName.SimpleReturns when spec.Options is SimpleReturnsSpecOptions simple
                => new SimpleReturnsState(simple.Length),
            IndicatorName.NetVolume => new NetVolumeState(),
            IndicatorName.NormalizedVolume when spec.Options is NormalizedVolumeSpecOptions normalized
                => new NormalizedVolumeState(normalized.Length),
            IndicatorName.BalanceOfPower when spec.Options is BalanceOfPowerSpecOptions balance
                => new BalanceOfPowerState(balance.MaType, balance.Length),
            IndicatorName.PriceMomentum when spec.Options is PriceMomentumSpecOptions priceMomentum
                => new PriceMomentumState(priceMomentum.Length),
            IndicatorName.VolumeMomentum when spec.Options is VolumeMomentumSpecOptions volumeMomentum
                => new VolumeMomentumState(volumeMomentum.Length),
            IndicatorName.MomentumOscillator when spec.Options is MomentumSpecOptions mom
                => new MomentumOscillatorState(length: mom.Length),
            IndicatorName.MomentumOscillator when spec.Options is MomentumOscillatorSpecOptions momentumOptions
                => new MomentumOscillatorState(momentumOptions.MaType, momentumOptions.Length),
            IndicatorName.DiNapoliPreferredStochasticOscillator when spec.Options is DiNapoliPreferredStochasticOscillatorSpecOptions diNapoli
                => new DiNapoliPreferredStochasticOscillatorState(diNapoli.Length),
            IndicatorName.DynamicMomentumOscillator when spec.Options is DynamicMomentumOscillatorSpecOptions momentum
                => new DynamicMomentumOscillatorState(momentum.MaType, momentum.Length),
            IndicatorName.DoubleStochasticOscillator when spec.Options is DoubleStochasticOscillatorSpecOptions doubleStochastic
                => new DoubleStochasticOscillatorState(doubleStochastic.MaType, doubleStochastic.Length),
            IndicatorName.RateOfChange when spec.Options is RocSpecOptions roc
                => new RateOfChangeState(roc.Length),
            IndicatorName.RateOfChange when spec.Options is RateOfChangeSpecOptions rate
                => new RateOfChangeState(rate.Length),
            IndicatorName.VolumeRateOfChange when spec.Options is VrocSpecOptions volumeRate
                => new VolumeRateOfChangeState(volumeRate.Length),
            IndicatorName.WilliamsR when spec.Options is WilliamsRSpecOptions williams
                => new WilliamsRState(williams.Length),
            IndicatorName.StochasticFastOscillator when spec.Options is StochasticFastOscillatorSpecOptions fastStoch
                => new StochasticFastOscillatorState(fastStoch.MaType, fastStoch.Length, fastStoch.SmoothLength1, fastStoch.SmoothLength2),
            IndicatorName.StochasticRegular when spec.Options is StochasticRegularSpecOptions regular
                => new StochasticRegularState(regular.MaType, regular.Length1, regular.Length2),
            IndicatorName.StochasticOscillator when spec.Options is StochasticSpecOptions stochastic
                => new StochasticOscillatorState(stochastic.MaType, stochastic.KLength, stochastic.DLength),
            IndicatorName.StochasticOscillator when spec.Options is StochasticKSpecOptions stochasticK
                => new StochasticOscillatorState(stochasticK.MaType, stochasticK.Length, stochasticK.SmoothLength1, stochasticK.SmoothLength2),
            IndicatorName.StochasticOscillator when spec.Options is PricePositionSpecOptions position
                => new StochasticOscillatorState(position.MaType, position.Length, position.SmoothLength1, position.SmoothLength2),
            IndicatorName.StochasticOscillator when spec.Options is StochasticOscillatorSpecOptions stochasticFull
                => new StochasticOscillatorState(stochasticFull.MaType, stochasticFull.Length, stochasticFull.SmoothLength1, stochasticFull.SmoothLength2),
            IndicatorName.StochasticOscillator when spec.Options is StochasticDSpecOptions stochasticD
                => new StochasticOscillatorState(length: stochasticD.Length),
            IndicatorName.ChandeMomentumOscillatorAverage when spec.Options is ChandeMomentumOscillatorAverageSpecOptions
                => new ChandeMomentumOscillatorAverageState(),
            IndicatorName.ChandeMomentumOscillatorAbsoluteAverage when spec.Options is ChandeMomentumOscillatorAbsoluteAverageSpecOptions
                => new ChandeMomentumOscillatorAbsoluteAverageState(),
            IndicatorName.ChandeMomentumOscillatorFilter when spec.Options is ChandeMomentumOscillatorFilterSpecOptions filteredChande
                => new ChandeMomentumOscillatorFilterState(filteredChande.MaType, filteredChande.Length),
            IndicatorName.ChandeMomentumOscillatorAbsolute when spec.Options is ChandeMomentumOscillatorAbsoluteSpecOptions absoluteChande
                => new ChandeMomentumOscillatorAbsoluteState(absoluteChande.Length),
            IndicatorName.VariableIndexDynamicAverage when spec.Options is VidyaSpecOptions vidya
                => new VariableIndexDynamicAverageState(vidya.MaType, vidya.Length),
            IndicatorName.VariableIndexDynamicAverage when spec.Options is VariableIndexDynamicAverageSpecOptions dynamicAverage
                => new VariableIndexDynamicAverageState(dynamicAverage.MaType, dynamicAverage.Length),
            IndicatorName.ChandeMomentumOscillator when spec.Options is ChandeMomentumOscillatorSignalSpecOptions chandeSignal
                => new ChandeMomentumOscillatorState(chandeSignal.MaType, chandeSignal.Length, chandeSignal.SignalLength),
            IndicatorName.ChandeMomentumOscillator when spec.Options is CmoSpecOptions cmo
                => new ChandeMomentumOscillatorState(cmo.MaType, cmo.Length, 3),
            IndicatorName.ChandeMomentumOscillator when spec.Options is ChandeMomentumOscillatorSpecOptions chande
                => new ChandeMomentumOscillatorState(chande.MaType, chande.Length, chande.SignalLength),
            IndicatorName.EhlersHannMovingAverage when spec.Options is EhlersHannMovingAverageSpecOptions hann
                => new EhlersHannMovingAverageState(hann.Length),
            IndicatorName.SineWeightedMovingAverage when spec.Options is SineWmaSpecOptions sine
                => new SineWeightedMovingAverageState(sine.Length),
            IndicatorName.DoubleExponentialMovingAverage when spec.Options is DemaSpecOptions dema
                => new DoubleExponentialMovingAverageState(length: dema.Length),
            IndicatorName.TripleExponentialMovingAverage when spec.Options is TemaSpecOptions tema
                => new TripleExponentialMovingAverageState(length: tema.Length),
            IndicatorName.ZeroLagExponentialMovingAverage when spec.Options is ZlemaSpecOptions zlema
                => new ZeroLagExponentialMovingAverageState(length: zlema.Length),
            IndicatorName.DetrendedPriceOscillator when spec.Options is DpoSpecOptions dpo
                => new DetrendedPriceOscillatorState(length: dpo.Length),
            IndicatorName.DetrendedPriceOscillator when spec.Options is DetrendedPriceOscillatorSpecOptions detrended
                => new DetrendedPriceOscillatorState(detrended.MaType, detrended.Length),
            IndicatorName.ElderRayIndex when spec.Options is ElderRayIndexSpecOptions elder
                => new ElderRayIndexState(elder.MaType, elder.Length),
            IndicatorName.ElderRayIndex when spec.Options is ElderRayBullPowerSpecOptions elderBull
                => new ElderRayIndexState(length: elderBull.Length),
            IndicatorName.ElderRayIndex when spec.Options is ElderRayBearPowerSpecOptions elderBear
                => new ElderRayIndexState(length: elderBear.Length),
            IndicatorName.AbsolutePriceOscillator when spec.Options is ApoSpecOptions apo
                => new AbsolutePriceOscillatorState(apo.MaType, apo.FastLength, apo.SlowLength),
            IndicatorName.AbsolutePriceOscillator when spec.Options is AbsolutePriceOscillatorSpecOptions absolute
                => new AbsolutePriceOscillatorState(absolute.MaType, absolute.FastLength, absolute.SlowLength),
            IndicatorName.AbsolutePriceOscillator when spec.Options is PriceOscillatorSpecOptions priceOscillator
                => new AbsolutePriceOscillatorState(fastLength: priceOscillator.ShortLength, slowLength: priceOscillator.LongLength),
            IndicatorName.EhlersHammingMovingAverage when spec.Options is HammingMaSpecOptions hamming
                => new EhlersHammingMovingAverageState(hamming.Length),
            IndicatorName.EhlersHammingMovingAverage when spec.Options is EhlersHammingMovingAverageSpecOptions hammingAlias
                => new EhlersHammingMovingAverageState(hammingAlias.Length),
            IndicatorName.SimplifiedLeastSquaresMovingAverage when spec.Options is SimplifiedLeastSquaresMovingAverageSpecOptions simplifiedLsma
                => new SimplifiedLeastSquaresMovingAverageState(simplifiedLsma.Length),
            IndicatorName.LeastSquaresMovingAverage when spec.Options is LsmaSpecOptions lsma
                => new LeastSquaresMovingAverageState(lsma.Length),
            IndicatorName.LeoMovingAverage when spec.Options is LeoMovingAverageSpecOptions leo
                => new LeoMovingAverageState(leo.Length),
            IndicatorName.ArnaudLegouxMovingAverage when spec.Options is AlmaSpecOptions alma
                => new ArnaudLegouxMovingAverageState(alma.Length),
            IndicatorName.NaturalMovingAverage when spec.Options is NaturalMaSpecOptions natural
                => new NaturalMovingAverageState(natural.Length),
            IndicatorName.SequentiallyFilteredMovingAverage when spec.Options is SequentiallyFilteredMovingAverageSpecOptions sequential
                => new SequentiallyFilteredMovingAverageState(sequential.MaType, sequential.Length),
            IndicatorName.DistanceWeightedMovingAverage when spec.Options is DistanceWeightedMovingAverageSpecOptions reciprocal
                => new DistanceWeightedMovingAverageState(reciprocal.Length),
            IndicatorName.InverseDistanceWeightedMovingAverage when spec.Options is InverseDistanceWeightedMovingAverageSpecOptions distance
                => new InverseDistanceWeightedMovingAverageState(distance.Length),
            IndicatorName.MiddleHighLowMovingAverage when spec.Options is MiddleHighLowMovingAverageSpecOptions middle
                => new MiddleHighLowMovingAverageState(middle.MaType, middle.Length1, middle.Length2),
            IndicatorName.FareySequenceWeightedMovingAverage when spec.Options is FareySequenceWeightedMovingAverageSpecOptions farey
                => new FareySequenceWeightedMovingAverageState(farey.Length),
            IndicatorName.WellesWilderMovingAverage when spec.Options is WwmaSpecOptions wilder
                => new WellesWilderMovingAverageState(wilder.Length),
            IndicatorName.WellesWilderMovingAverage when spec.Options is SmmaSpecOptions smoothed
                => new WellesWilderMovingAverageState(smoothed.Length),
            IndicatorName.WellesWilderMovingAverage when spec.Options is ModifiedMaSpecOptions modified
                => new WellesWilderMovingAverageState(modified.Length),
            IndicatorName.SymmetricallyWeightedMovingAverage when spec.Options is SymmetricallyWeightedMovingAverageSpecOptions symmetric
                => new SymmetricallyWeightedMovingAverageState(symmetric.Length),
            IndicatorName.EhlersTriangleMovingAverage when spec.Options is EhlersTriangleMovingAverageSpecOptions triangle
                => new EhlersTriangleMovingAverageState(triangle.Length),
            IndicatorName.JsaMovingAverage when spec.Options is JsaMovingAverageSpecOptions paired
                => new JsaMovingAverageState(paired.Length),
            IndicatorName.Midpoint when spec.Options is MidpointSpecOptions midpoint => new MidpointState(midpoint.Length),
            IndicatorName.Midprice when spec.Options is MidpriceSpecOptions midprice => new MidpriceState(midprice.Length),
            IndicatorName.ParabolicWeightedMovingAverage when spec.Options is QuadraticWmaSpecOptions quadratic => new ParabolicWeightedMovingAverageState(quadratic.Length),
            IndicatorName.ParabolicWeightedMovingAverage when spec.Options is ParabolicWmaSpecOptions parabolic => new ParabolicWeightedMovingAverageState(parabolic.Length),
            IndicatorName.CubedWeightedMovingAverage when spec.Options is CubicWmaSpecOptions cubic => new CubedWeightedMovingAverageState(cubic.Length),
            IndicatorName.CubedWeightedMovingAverage when spec.Options is CubedWeightedMovingAverageSpecOptions cubed => new CubedWeightedMovingAverageState(cubed.Length),
            IndicatorName.QuickMovingAverage when spec.Options is QuickMovingAverageSpecOptions quick => new QuickMovingAverageState(quick.Length),
            IndicatorName.FibonacciWeightedMovingAverage when spec.Options is FibonacciWeightedMovingAverageSpecOptions fib => new FibonacciWeightedMovingAverageState(fib.Length),
            IndicatorName.SquareRootWeightedMovingAverage when spec.Options is SquareRootWeightedMovingAverageSpecOptions root => new SquareRootWeightedMovingAverageState(root.Length),
            IndicatorName.KaufmanAdaptiveMovingAverage when spec.Options is KamaSpecOptions kama => new KaufmanAdaptiveMovingAverageState(kama.Length),
            IndicatorName.QuadraticMovingAverage when spec.Options is QuadraticMovingAverageSpecOptions quadraticMean => new QuadraticMovingAverageState(quadraticMean.Length),
            IndicatorName.GeometricMovingAverage when spec.Options is GeoMaSpecOptions geometric => new GeometricMovingAverageState(geometric.Length),
            IndicatorName.GeometricMeanMovingAverage when spec.Options is GeometricMeanMovingAverageSpecOptions positiveGeometric => new GeometricMeanMovingAverageState(positiveGeometric.Length),
            IndicatorName.WeightedMovingAverage when spec.Options is WmaSpecOptions wma => new WeightedMovingAverageState(wma.Length),
            IndicatorName.SimpleMovingAverage => new SimpleMovingAverageState(((SmaSpecOptions)spec.Options).Length),
            IndicatorName.ExponentialMovingAverage => new ExponentialMovingAverageState(((EmaSpecOptions)spec.Options).Length),
            IndicatorName.RelativeStrengthIndex => new RelativeStrengthIndexState(((RsiSpecOptions)spec.Options).Length, maType: ((RsiSpecOptions)spec.Options).MaType),
            IndicatorName.MovingAverageConvergenceDivergence when spec.Options is MacdSpecOptions macd => new MovingAverageConvergenceDivergenceState(macd.FastLength, macd.SlowLength, macd.SignalLength),
            IndicatorName.MovingAverageConvergenceDivergence when spec.Options is MacdLineSpecOptions line => new MovingAverageConvergenceDivergenceState(line.FastLength, line.SlowLength),
            IndicatorName.MovingAverageConvergenceDivergence when spec.Options is MacdSignalSpecOptions signal => new MovingAverageConvergenceDivergenceState(signal.FastLength, signal.SlowLength, signal.SignalLength),
            IndicatorName.MovingAverageConvergenceDivergence when spec.Options is MacdHistogramSpecOptions histogram => new MovingAverageConvergenceDivergenceState(histogram.FastLength, histogram.SlowLength, histogram.SignalLength),
            IndicatorName.BollingerBands when spec.Options is BollingerBandsSpecOptions bb => new BollingerBandsState(bb.Length, bb.StdDevMult, bb.MaType),
            IndicatorName.BollingerBands when spec.Options is BollingerBandsMiddleSpecOptions bbMiddle => new BollingerBandsState(bbMiddle.Length),
            IndicatorName.BollingerBandsPercentB when spec.Options is BollingerBandsPercentBSpecOptions bbPercent => new BollingerBandsPercentBState(bbPercent.Multiplier, length: bbPercent.Length),
            IndicatorName.BollingerBandsWidth when spec.Options is BollingerBandsWidthSpecOptions bbWidth => new BollingerBandsWidthState(length: bbWidth.Length),
            IndicatorName.AverageTrueRange => new AverageTrueRangeState(((AtrSpecOptions)spec.Options).Length),
            _ => null
        };
    }

    /// <summary>
    /// Extracts the appropriate value from a streaming update.
    /// </summary>
    public static double ExtractValue(StreamingIndicatorStateUpdate update, IndicatorSpec spec)
    {
        // A named key wins, and is read before the slot: a spec built from a key carries Output = Primary, so
        // the branch below would answer with the state's own value and never look at the named outputs - the
        // batch half of that same mistake is in BuilderArmBinding. See issue #219.
        if (spec.OutputKey is { } named)
        {
            return update.Outputs is not null && update.Outputs.TryGetValue(named, out var namedValue)
                ? namedValue
                : double.NaN;
        }

        // A spec that names no key wants the state's own value.
        return update.Value;
    }
}
