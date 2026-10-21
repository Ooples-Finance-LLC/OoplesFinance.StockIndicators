#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // The legacy scan uses int j + lag. Preserve its fallback when that can overflow.
    private static bool IsDemarkCountState(IIndicator indicator) => indicator switch
    {
        DemarkSetupIndicator setup => Math.Max(1, setup.Length) <= 1_073_741_824,
        DemarkReversalPoints reversal => (long)Math.Max(1, reversal.Length1) + Math.Max(1, reversal.Length2) - 1 <= int.MaxValue,
        _ => false
    };

    private static bool IsVossShared(EhlersVossPredictiveFilter filter)
    {
        if (!double.IsFinite(filter.Predict) || !double.IsFinite(filter.Bw)) return false;
        var length = Math.Max(1, filter.Length);
        var phase = filter.Bw * 2 * Math.PI / length;
        if (!double.IsInfinity(phase)) return true;
        var exact = new ExactMeanAccumulator();
        exact.AddProduct(filter.Bw, 2 * Math.PI);
        return double.IsFinite(exact.Mean(length));
    }

    private static bool IsTruncatedBandShared(EhlersTruncatedBandPassFilter filter)
    {
        if (!double.IsFinite(filter.Bw)) return false;
        var length = Math.Max(1, filter.Length1);
        var phase = filter.Bw * 2 * Math.PI / length;
        if (!double.IsInfinity(phase)) return true;
        var exact = new ExactMeanAccumulator();
        exact.AddProduct(filter.Bw, 2 * Math.PI);
        return double.IsFinite(exact.Mean(length));
    }

    private static bool IsGeneratedState(IIndicator indicator) =>
        indicator is PivotDetectorOscillator { MaType: null } ||
        indicator is TheRangeIndicator { MaType: null } ||
        indicator is ImpulseMovingAverageConvergenceDivergence { MaType: null } or ImpulsePercentagePriceOscillator ||
        indicator is DecisionPointBreadthSwenlinTradingOscillator ||
        indicator is OscOscillator { MaType: null } ||
        indicator is UtBotAlerts { MaType: null } bot && double.IsFinite(bot.KeyValue) ||
        indicator is OptimizedTrendTracker { MaType: null } trend && double.IsFinite(trend.Percent) ||
        indicator is MartinRatio { MaType: null } martin && double.IsFinite(martin.Bmk) ||
        indicator is MiddleHighLowMovingAverage { MaType: null } ||
        indicator is OvershootReductionMovingAverage { MaType: null } ||
        indicator is OscarIndicator or PhaseChangeIndex { MaType: null } ||
        indicator is KendallRankCorrelationCoefficient or RSquared ||
        indicator is LinearWeightedMovingAverageCore or LeoMovingAverage or McGinleyDynamic ||
        indicator is GeoMa or GeometricMeanMovingAverage ||
        indicator is GeneralFilterEstimator ||
        indicator is GOscillator or GannSwingOscillator ||
        indicator is GatorOscillator ||
        indicator is MarketMeannessIndex meanness && meanness.MaType is null && meanness.SecondAverage is null ||
        indicator is FloorPivotPointS1 or FloorPivotPointR1 or PivotPoint ||
        indicator is Jma ||
        indicator is Kama ||
        indicator is LiquidRelativeStrengthIndex ||
        indicator is KaseSerialDependencyIndex ||
        indicator is IchimokuTenkanSen or IchimokuKijunSen or IchimokuSenkouSpanA or IchimokuSenkouSpanB or IchimokuChikouSpan ||
        indicator is Kst or KnowSureThing { MaType: null, SecondAverage: null, ThirdAverage: null, FourthAverage: null, FifthAverage: null } ||
        indicator is ShinoharaIntensityRatioA or ShinoharaIntensityRatioB ||
        indicator is StochasticD or Stochastic { MaType: null } or StochasticK { MaType: null } or PricePosition { MaType: null }
            or StochasticOscillator { MaType: null } or StochasticFastOscillator { MaType: null } or StochasticRegular { MaType: null } ||
        indicator is ParabolicSar sar && double.IsFinite(sar.Start) && sar.Start >= 0
            && double.IsFinite(sar.Increment) && sar.Increment >= 0 && double.IsFinite(sar.Maximum) && sar.Maximum >= sar.Start ||
        indicator is StochasticCustomOscillator { MaType: null } or StochasticMomentumIndex { MaType: null } ||
        indicator is RapidRelativeStrengthIndex { MaType: null } or StochRsi { MaType: null }
            or StochasticRsiOscillator or StochasticRelativeStrengthIndex { MaType: null } ||
        indicator is Pfe or PolarizedFractalEfficiency { MaType: null } ||
        indicator is T3 { MaType: null } ||
        indicator is TillsonT3MovingAverage { MaType: null } tillson && double.IsFinite(tillson.VFactor) ||
        indicator is OnBalanceVolumeModified { MaType: null } or OnBalanceVolumeReflex { MaType: null } ||
        indicator is ConnorsRsi or ConnorsRelativeStrengthIndex { MaType: null } or StochasticConnorsRelativeStrengthIndex { MaType: null } ||
        indicator is SelfAdjustingRelativeStrengthIndex { MaType: null } selfRsi && double.IsFinite(selfRsi.Mult) ||
        indicator is ThreeHMA or TripleHullMovingAverage
            or ThreeHma { MaType: MovingAvgType.WeightedMovingAverage } or _3HMA { MaType: MovingAvgType.WeightedMovingAverage } ||
        indicator is DominantCycleTunedRelativeStrengthIndex ||
        indicator is TurboTrigger { MaType: null } or TurboStochasticsFast { MaType: null } or TurboStochasticsSlow { MaType: null } ||
        IsDemarkCountState(indicator) ||
        indicator is ModularFilter modular && double.IsFinite(modular.Beta) ||
        indicator is MovingAverageAdaptiveFilter adaptiveFilter && double.IsFinite(adaptiveFilter.Filter) && adaptiveFilter.Filter >= 0
            && adaptiveFilter.FastAlpha >= 0 && adaptiveFilter.FastAlpha <= 1 && adaptiveFilter.SlowAlpha >= 0 && adaptiveFilter.SlowAlpha <= 1 ||
        indicator is NegativeVolumeDisparityIndicator { MaType: null } disparity && double.IsFinite(disparity.Top) && double.IsFinite(disparity.Bottom) ||
        indicator is MultiLevelIndicator level && double.IsFinite(level.Factor) ||
        indicator is MacZVwapIndicator { MaType: null } maczVwap && double.IsFinite(maczVwap.Gamma) ||
        indicator is MacZIndicator { MaType: null } macz && double.IsFinite(macz.Mult) ||
        indicator is LogisticCorrelation logistic && double.IsFinite(logistic.K) ||
        indicator is LBRPaintBars { MaType: null } paint && double.IsFinite(paint.AtrMult) ||
        indicator is KaufmanAdaptiveBands bands && double.IsFinite(bands.StdDevFactor) && bands.StdDevFactor >= 0 ||
        indicator is KaufmanBinaryWave wave && double.IsFinite(wave.FastSc) && wave.FastSc >= 0
            && double.IsFinite(wave.SlowSc) && wave.SlowSc >= 0 && double.IsFinite(wave.FilterPct) && wave.FilterPct >= 0 ||
        indicator is InsyncIndex { MaType: null } insync && double.IsFinite(insync.StdDevMult) && double.IsFinite(insync.Divisor) ||
        indicator is HerrickPayoffIndex payoff && double.IsFinite(payoff.PointValue) ||
        indicator is HawkeyeVolumeIndicator hawkeye && double.IsFinite(hawkeye.Divisor) ||
        indicator is HampelFilter hampel && double.IsFinite(hampel.ScalingFactor) ||
        indicator is GroverLlorensActivator { MaType: null } grover && double.IsFinite(grover.Mult) ||
        indicator is FXSniperIndicator { MaType: null } sniper && double.IsFinite(sniper.B) ||
        indicator is FastandSlowKurtosisOscillator { MaType: null } kurtosis && double.IsFinite(kurtosis.Ratio) ||
        indicator is FibonacciRetrace { MaType: null } retrace && double.IsFinite(retrace.Factor) ||
        indicator is GrandTrendForecasting grand && double.IsFinite(grand.Mult) && grand.Mult >= 0 ||
        indicator is EhlersTruncatedBandPassFilter truncated && IsTruncatedBandShared(truncated) ||
        indicator is EhlersVossPredictiveFilter voss && IsVossShared(voss) ||
        indicator is BollingerBandsAvgTrueRange ratio && StrengthWindow.Supports(ratio.MaType) && double.IsFinite(ratio.StdDevMult) ||
        indicator is Skewness or PercentRank or MedianValue or MedianMa ||
        indicator is NaturalMa or FareySequenceWeightedMovingAverage or OptimalWeightedMovingAverage ||
        indicator is Midpoint or Midprice ||
        indicator is InformationRatio { MaType: null } information && double.IsFinite(information.Bmk) ||
        indicator is SharpeRatio { MaType: null } sharpe && double.IsFinite(sharpe.Bmk) ||
        indicator is SortinoRatio { MaType: null, SecondAverage: null } sortino && double.IsFinite(sortino.Bmk) ||
        indicator is NormalizedVolume or OCHistogram { MaType: null, SecondAverage: null } or ModifiedPriceVolumeTrend { MaType: null } ||
        indicator is OmegaRatio omega && double.IsFinite(omega.Bmk) ||
        indicator is RunningEquity { MaType: null } ||
        indicator is SimpleReturns or LogReturns ||
        indicator is NetVolume or Range or MarketFacilitationIndex ||
        indicator is HighestHigh { Length: > 0 } or LowestLow { Length: > 0 }
            or RollingMax { Length: > 0 } or RollingMin { Length: > 0 }
            or TrueRange or BalanceOfPower or Wma or WilliamsR or Ema or Adl
            or Wwma or Smma or ModifiedMa or Dema or Tema or Zlema or Tma or Hma or Rsi or Atr or Roc or Momentum
            or Cci or Cmo or Trix or Apo or Ppo or Macd or Aroon or Dpo or Cmf or Mfi or ForceIndex or Obv
            or Nvi or Pvi or Vroc or Pvt or Adx or MassIndex or ChaikinOscillator or Emv or Vwap or AroonUp or AroonDown
            or Demarker or DeMarker or UlcerIndex or ChoppinessIndex or Natr or AtrPercent or AtrChannelWidth or ChaikinVolatility
            or Variance or CoefficientOfVariation or StandardError or StdDev { Length: > 0 } or HistoricalVolatility
            or Kvo or KlingerVolumeOscillator or UltimateOscillator or Tsi or TrueStrengthIndex
            or ErgodicTrueStrengthIndexV1 or SMIErgodicIndicator
            or HullMovingAverage or TriangularMovingAverage or EhlersZeroLagEma or EhlersZeroLagExponentialMovingAverage
            or ZeroLowLagMovingAverage or ZeroLagTripleExponentialMovingAverage or SlowSmoothedMovingAverage { Length: > int.MinValue }
            or AbsolutePriceOscillator or PriceOscillator or PriceOscillatorPercent or PercentagePriceOscillator
            or McNichollMovingAverage or WellRoundedMovingAverage or WellesWilderSummation or WildersSummationMethod
            or OnBalanceVolume or NegativeVolumeIndex or PositiveVolumeIndex or PriceVolumeTrend
            or AverageTrueRange or ChandeMomentumOscillator or RateOfChange or DetrendedPriceOscillator
            or AccumulationDistributionLine or WilliamsAccumulationDistribution or SmoothedWilliamsAccumulationDistribution
            or EaseOfMovement { Divisor: >= double.MinValue and <= double.MaxValue }
            or Pvo or PercentageVolumeOscillator or VolumeOscillator or PriceVolumeOscillator
            or EhlersBetterExponentialMovingAverage or HoltExponentialMovingAverage or RegularizedEma or QuickMovingAverage or EhlersLaguerreFilter or EhlersLaguerreRsi
            or EhlersAdaptiveLaguerreFilter or EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha
            or RepulsionMovingAverage or CompoundRatioMovingAverage or GeneralizedDoubleEma or DoubleExponentialSmoothing
            or GeneralizedDoubleExponentialMovingAverage { VolumeFactor: >= double.MinValue and <= double.MaxValue }
            or LightLeastSquaresMovingAverage or LinearExtrapolation or RecursiveMovingTrendAverage
            or EhlersIirFilter or EhlersInfiniteImpulseResponseFilter or EhlersFirFilter or EhlersFiniteImpulseResponseFilter or EhlersOptimumEllipticFilter or EhlersModifiedOptimumEllipticFilter
            or Spencer15PointMovingAverage or Spencer21PointMovingAverage or DampedSineWaveWeightedFilter or HendersonWeightedMovingAverage or RightSidedRickerMovingAverage
            or FibonacciWeightedMovingAverage or SquareRootWeightedMovingAverage or SineWma
            or TillsonIE2 or IIRLeastSquaresEstimate or HullEstimate
            or Ehlers2PoleButterworthFilterV1 or Ehlers2PoleButterworthFilterV2 or Ehlers2PoleSuperSmootherFilterV1 or Ehlers2PoleSuperSmootherFilterV2
            or Ehlers3PoleButterworthFilterV1 or Ehlers3PoleButterworthFilterV2 or Ehlers3PoleSuperSmootherFilter
            or EhlersDecycler or EhlersHighPassFilterV1 or EhlersLeadingIndicator or EhlersSimpleDecycler
            or EhlersHighPassFilterV2 or EhlersDecyclerOscillatorV1 or EhlersDecyclerOscillatorV2
            or EhlersRoofingFilter or EhlersRoofingFilterV1 or EhlersRoofingFilterV2 or EhlersRoofingFilterIndicator
            or EhlersHpLpRoofingFilter or EhlersZeroMeanRoofingFilter or EhlersDetrendedLeadingIndicator
            or EhlersCyberCycle or EhlersGaussianFilter
            or EhlersCyberCycleOscillator { Alpha: >= double.MinValue and <= double.MaxValue }
            or EhlersInstantaneousTrendlineV2 { Alpha: >= double.MinValue and <= double.MaxValue }
            or EhlersBandPassFilterV1 { Bw: >= double.MinValue and <= double.MaxValue }
            or EhlersBandPassFilterV2 { Bw: >= double.MinValue and <= double.MaxValue }
            or EhlersCycleBandPassFilter { Delta: >= double.MinValue and <= double.MaxValue }
            or EhlersSuperPassbandFilter
            or EhlersEarlyOnsetTrendIndicator { K: >= double.MinValue and <= double.MaxValue }
            or EhlersImpulseReaction { Q: >= double.MinValue and <= double.MaxValue }
            or EhlersImpulseResponse { Bw: >= double.MinValue and <= double.MaxValue }
            or EhlersHannMovingAverage or EhlersHammingMovingAverage or HammingMa
            or EhlersHannWindowIndicator or EhlersHammingWindowIndicator
            or SymmetricallyWeightedMovingAverage or EhlersTriangleMovingAverage or EhlersTriangleWindowIndicator
            or Alma or JsaMovingAverage or SelfWeightedMovingAverage
            or SimplifiedLeastSquaresMovingAverage or FisherLeastSquaresMovingAverage or TStepLeastSquaresMovingAverage
            or PolynomialLeastSquaresMovingAverage or QuadraticRegression or QuadraticLeastSquaresMovingAverage or AdaptiveLeastSquares
            or AtrFilteredEma or AtrFilteredExponentialMovingAverage { Min: > double.NegativeInfinity and < double.PositiveInfinity }
            or AhrensMovingAverage or AlphaDecreasingEma or VariableLengthMovingAverage
            or Vwma or VolumeWeightedMovingAverage or WindowedVolumeWeightedMovingAverage
            or BuffAverage or EquityMovingAverage
            or UpsideDownsideVolume or TFSVolumeOscillator or VolumeAccumulationOscillator or RelativeVolumeIndicator or BetterVolumeIndicator
            or AverageMoneyFlowOscillator or TradeVolumeIndex or TimeAndMoneyChannel or TironeLevels or TrendStep or TechnicalRank or TrigonometricOscillator or TimeSeriesForecast or UltimateTraderOscillator or UltimateVolatilityIndicator or ValueChartIndicator or SmoothedWilliamsR or EnhancedWilliamsR or VerticalHorizontalFilter or VerticalHorizontalMovingAverage
            or TrendContinuationFactor or TrendTriggerFactor or TrendIntensityIndex or TraderPressureIndex or VolumeAccumulationPercent or TwiggsMoneyFlow
            or AverageTrueRangeChannel { Multiplier: > double.NegativeInfinity and < double.PositiveInfinity }
            or StollerAverageRangeChannels { AtrMult: > double.NegativeInfinity and < double.PositiveInfinity } or KeltnerChannelWidth
            or PriceChannel { Pct: > double.NegativeInfinity and < double.PositiveInfinity }
            or PriceChannelMiddle or PriceChannelUpper or PriceChannelLower or PriceLineChannel or PriceCurveChannel
            or MovingAverageBands { Mult: > double.NegativeInfinity and < double.PositiveInfinity }
            or MovingAverageBandWidth { Mult: > double.NegativeInfinity and < double.PositiveInfinity }
            or PriceHeadleyAccelerationBands { Factor: > double.NegativeInfinity and < double.PositiveInfinity }
            or RootMovingAverageSquaredErrorBands { StdDevFactor: > double.NegativeInfinity and < double.PositiveInfinity }
            or MeanAbsoluteErrorBands { StdDevFactor: > double.NegativeInfinity and < double.PositiveInfinity }
            or MeanAbsoluteDeviationBands { StdDevFactor: > double.NegativeInfinity and < double.PositiveInfinity }
            or InterquartileRangeBands { Mult: > double.NegativeInfinity and < double.PositiveInfinity }
            or RangeBands { StdDevFactor: > double.NegativeInfinity and < double.PositiveInfinity }
            or BollingerBands { HasUnmodifiedDefaultAverage: true, StdDev: > double.NegativeInfinity and < double.PositiveInfinity }
            or BollingerBandsWidth
            or BollingerBandsPercentB { Multiplier: > double.NegativeInfinity and < double.PositiveInfinity }
            or NarrowSidewaysChannel
            or BollingerBandsMiddle
            or BollingerBandsAtr { Multiplier: > double.NegativeInfinity and < double.PositiveInfinity }
            or BollingerBandsFibonacciRatios { FibRatio3: > double.NegativeInfinity and < double.PositiveInfinity }
            or BollingerBandsWithAtrPct { StdDevMult: > double.NegativeInfinity and < double.PositiveInfinity }
            or KirshenbaumBands { StdDevFactor: > double.NegativeInfinity and < double.PositiveInfinity }
            or TrendTraderBands { Mult: > double.NegativeInfinity and < double.PositiveInfinity, BandStep: > double.NegativeInfinity and < double.PositiveInfinity }
            or StandardDeviationChannel or RateOfChangeBands
            or ScalpersChannel
            or FlaggingBands or ExtendedRecursiveBands or MotionToAttractionChannels or VolumeAdaptiveBands
            or FractalChaosBands or FractalChaosOscillator
            or ProjectionBands or ProjectionOscillator or ProjectionBandwidth
            or StatisticalVolatility or VolatilityQualityIndex
            or VolatilityRatio or VolatilitySwitchIndicator
            or VolatilityWaveMovingAverage
            or GarmanKlassVolatility
            or HistoricalVolatilityPercentile
            or StandardErrorCore or ErgodicMeanDeviationIndicator
            or AdaptiveErgodicCandlestickOscillator
            or ErgodicCommoditySelectionIndex { PointValue: > double.NegativeInfinity and < double.PositiveInfinity }
            or ErgodicCandlestickOscillator or ErgodicMovingAverageConvergenceDivergence
            or ErgodicPercentagePriceOscillator or ErgodicTrueStrengthIndexV2
            or EhlersAllPassPhaseShifter or EhlersCenterOfGravityOscillator or EhlersCenterofGravityOscillator or EhlersChebyshevLowPassFilter
            or EhlersSimpleDerivIndicator or EhlersSimpleClipIndicator or EhlersSimpleWindowIndicator or EhlersTripleDelayLineDetrender
            or EhlersStochasticCenterOfGravityOscillator or EhlersStochastic or EhlersRelativeVigorIndex
            or EhlersStochasticCyberCycle { Alpha: >= double.NegativeInfinity and <= double.PositiveInfinity }
            or EhlersModifiedRelativeStrengthIndex or EhlersModifiedStochasticIndicator or EhlersMovingAverageDifferenceIndicator
            or EhlersRocketRelativeStrengthIndex { ObosLevel: > double.NegativeInfinity and < double.PositiveInfinity, Mult: > double.NegativeInfinity and < double.PositiveInfinity }
            or Frama { Length: < int.MaxValue } or EhlersFrama { Length: < int.MaxValue }
            or FisherTransform or EhlersFisherTransform or EhlersInverseFisherTransform or EhlersRelativeStrengthIndexInverseFisherTransform
            or EhlersCorrelationTrendIndicator or EhlersCorrelationCycleIndicator or EhlersCorrelationAngleIndicator or EhlersMarketStateIndicator
            or EhlersHurstCoefficient or EhlersSpearmanRankIndicator
            or EhlersNoiseEliminationTechnology { Length: < int.MaxValue }
            or EhlersClassicHilbertTransformer or EhlersHilbertTransformer or EhlersHilbertTransformerIndicator
            or EhlersInstantaneousPhaseIndicator
            or EhlersHilbertTransformIndicator { IMult: >= double.MinValue and <= double.MaxValue, QMult: >= double.MinValue and <= double.MaxValue }
            or EhlersInstantaneousTrendlineV1 or EhlersHilbertOscillator or EhlersSineWaveIndicatorV1
            or EhlersSineWaveIndicatorV2 { Alpha: >= double.MinValue and <= double.MaxValue }
            or FollowingAdaptiveMovingAverage
            or EhlersMesaAdaptiveMovingAverage { FastLimit: >= double.MinValue and <= double.MaxValue, SlowLimit: >= double.MinValue and <= double.MaxValue }
            or EhlersDualDifferentiatorDominantCycle or EhlersHomodyneDominantCycle or EhlersPhaseAccumulationDominantCycle
            or EhlersSignalToNoiseRatioV1 { MaType: null }
            or EhlersSignalToNoiseRatioV2 or EhlersAlternateSignalToNoiseRatio or EhlersEnhancedSignalToNoiseRatio
            or EhlersCycleAmplitude { Delta: >= double.MinValue and <= double.MaxValue }
            or EhlersTrendExtraction { MaType: null, Delta: >= double.MinValue and <= double.MaxValue }
            or EhlersEmpiricalModeDecomposition { MaType: null, SecondAverage: null, ThirdAverage: null, Delta: >= double.MinValue and <= double.MaxValue, Fraction: >= double.MinValue and <= double.MaxValue }
            or EhlersConvolutionIndicator
            or EhlersPhaseCalculation { MaType: null }
            or EhlersFourierSeriesAnalysis { Bw: >= double.MinValue and <= double.MaxValue }
            or EhlersDiscreteFourierTransform or EhlersDiscreteFourierTransformSpectralEstimate
            or EhlersCombFilterSpectralEstimate { Bw: >= 0 and <= double.MaxValue } or EhlersDominantCycleTunedBypassFilter or EhlersAutoCorrelationReversals { MaType: null }
            or EhlersMesaPredictIndicatorV1
            or EhlersMesaPredictIndicatorV2 { MaType: null }
            or EhlersAnticipateIndicator { MaType: null, Length: <= AnticipateWindow.MaximumLength, Bw: >= double.MinValue and <= double.MaxValue }
            or EhlersFMDemodulatorIndicator { MaType: null }
            or SuperSmoother or EhlersSquelchIndicator
            or EhlersVariableIndexDynamicAverage { MaType: null, SecondAverage: null }
            or EhlersSwissArmyKnife { Delta: >= 0 and <= double.MaxValue }
            or EhlersSpectrumDerivedFilterBank { MinLength: < int.MaxValue, MaxLength: < int.MaxValue, Length2: < int.MaxValue }
            or EhlersRestoringPullIndicator { MaType: null, MinLength: < int.MaxValue, MaxLength: < int.MaxValue, Length2: < int.MaxValue }
            or ButterworthFilter or InverseFisherTransformCore or EhlersReflex or EhlersTrendflex
            or ElderForceIndex or MfiCore or WilliamsAD
            or KlingerSignal or MassIndexCore
            or ChandeMomentumOscillatorSignal { MaType: null }
            or MacdLine or MacdSignal or MacdHistogram
            or AverageDirectionalIndex { MaType: null } or MomentumOscillator { MaType: null }
            or VolumePriceTrend { MaType: null } or StandardDevation { MaType: MovingAvgType.SimpleMovingAverage } or Vhf
            or Ama or AdaptiveRsi or AsymmetricalRsi or AsymmetricalRelativeStrengthIndex
            or AdaptiveRelativeStrengthIndex { MaType: null }
            or AdaptiveStochastic or ApirineSlowRelativeStrengthIndex { MaType: null }
            or PoweredKaufmanAdaptiveMovingAverage
            or AdaptiveTrailingStop { Multiplier: >= double.MinValue and <= double.MaxValue }
            or AverageAbsoluteErrorNormalization or BelkhayateTiming or BayesianOscillator { MaType: null }
            or AtrTrailingStops { MaType: null, Multiplier: >= double.MinValue and <= double.MaxValue }
            or AverageTrueRangeTrailingStops { MaType: null, Multiplier: >= double.MinValue and <= double.MaxValue }
            or ChandelierExit { MaType: null, Mult: >= double.MinValue and <= double.MaxValue }
            or ChandelierExitLong { MaType: null } or ChandelierExitShort { MaType: null }
            or MotionToAttractionTrailingStop
            or ElderSafeZoneStops { MaType: null, Mult: >= double.MinValue and <= double.MaxValue }
            or KaseDevStopV1 { MaType: null, StdDev1: >= double.MinValue and <= double.MaxValue, StdDev2: >= double.MinValue and <= double.MaxValue, StdDev3: >= double.MinValue and <= double.MaxValue, StdDev4: >= double.MinValue and <= double.MaxValue }
            or KaseDevStopV2 { MaType: null, StdDev1: >= double.MinValue and <= double.MaxValue, StdDev2: >= double.MinValue and <= double.MaxValue, StdDev3: >= double.MinValue and <= double.MaxValue, StdDev4: >= double.MinValue and <= double.MaxValue }
            or LinearTrailingStop { Mult: >= double.MinValue and <= double.MaxValue }
            or PercentageTrailingStops { Pct: >= double.MinValue and <= double.MaxValue }
            or BilateralStochasticOscillator { MaType: null }
            or BreakoutRsi
            or CCTStochRelativeStrengthIndex
            or CCTStochRSI { MaType: MovingAvgType.SimpleMovingAverage or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WeightedMovingAverage or MovingAvgType.WildersSmoothingMethod }
            or ChandeCompositeMomentumIndex or ChandeForecastOscillator or ChandeKrollRSquaredIndex { MaType: null }
            or ChandeMomentumOscillatorAbsolute or ChandeMomentumOscillatorAbsoluteAverage or ChandeMomentumOscillatorAverage or ChandeMomentumOscillatorAverageDisparityIndex
            or ChandeMomentumOscillatorFilter { MaType: null }
            or ChartmillValueIndicator { MaType: null } or CenterOfLinearity or ChopZone { MaType: null }
            or CommoditySelectionIndex { MaType: null } or ConstanceBrownCompositeIndex { MaType: null }
            or ConfluenceIndicator { MaType: null }
            or CoppockCurve { MaType: null } or DerivativeOscillator { MaType: null } or DidiIndex { MaType: null }
            or Pmo { MaType: null } or PriceMomentumOscillator { MaType: null } or DecisionPointPriceMomentumOscillator
            or DoubleSmoothedStochastic { MaType: null } or DTOscillator { MaType: null }
            or DoubleStochasticOscillator { MaType: null } or DMIStochastic { MaType: null }
            or CalmarRatio
            or DemandIndex or DemandOscillator { MaType: null }
            or DampingIndex { MaType: null }
            or DetrendedSyntheticPrice
            or DirectionalTrendIndex { MaType: null }
            or CoralTrendIndicator or CorrectedMovingAverage { MaType: null }
            or CubedWeightedMovingAverage
            or Dema2Lines { MaType: null }
            or DemarkPressureRatioV1 or DemarkPressureRatioV2 or DemarkRangeExpansionIndex
            or DiNapoliMovingAverageConvergenceDivergence { Lc: > 0 and <= double.MaxValue, Sc: > 0 and <= double.MaxValue, Sp: > 0 and <= double.MaxValue }
            or DiNapoliPercentagePriceOscillator or DiNapoliPreferredStochasticOscillator
            or DistanceWeightedMovingAverage or InverseDistanceWeightedMovingAverage
            or DisparityIndex { MaType: null }
            or DoubleSmoothedMomenta { MaType: null, SecondAverage: null, ThirdAverage: null, FourthAverage: null, FifthAverage: null }
            or DoubleSmoothedRelativeStrengthIndex
            or DynamicMomentumIndex { MaType: null } or DynamicMomentumOscillator { MaType: null }
            or DynamicSupportAndResistance { MaType: null } or DynamicallyAdjustableFilter or DynamicallyAdjustableMovingAverage
            or EdgePreservingFilter { MaType: null } or EfficientPrice
            or EfficientAutoLine { FastAlpha: >= -double.MaxValue and <= double.MaxValue, SlowAlpha: >= -double.MaxValue and <= double.MaxValue }
            or EarningSupportResistanceLevels or ElderMarketThermometer { MaType: null, SecondAverage: null }
            or ElderImpulseSystem or ElderRayIndex { MaType: null }
            or EndPointMovingAverage
            or ElliottWaveOscillator { MaType: null, SecondAverage: null, ThirdAverage: null } or EmaWaveIndicator
            or EnhancedIndex { MaType: null }
            or FallingRisingFilter
            or FastSlowKurtosisOscillator or FastSlowRsiOscillator or FastSlowStochasticOscillator
            or FastSlowDegreeOscillator { MaType: null }
            or FearAndGreedIndicator { MaType: null } or FireflyOscillator { MaType: null }
            or FoldedRelativeStrengthIndex { MaType: null } or FisherTransformStochasticOscillator
            or ForecastOscillator { MaType: null }
            or FreedomOfMovement { MaType: null } or FunctionToCandles { MaType: null }
            or GainLossMovingAverage { MaType: null, SecondAverage: null }
            or GannHiLoActivator { MaType: null, SecondAverage: null } or GannTrendOscillator
            or GopalakrishnanRangeIndex { MaType: null, SecondAverage: null }
            or GroverLlorensCycleOscillator { MaType: null }
            or HarmonicMeanMovingAverage
            or HalfTrend { MaType: null }
            or GuppyDistanceIndicator { SecondAverage: null, ThirdAverage: null, FourthAverage: null, FifthAverage: null, Average6Average: null, Average7Average: null, Average8Average: null, Average9Average: null, Average10Average: null, Average11Average: null, Average12Average: null, MaType: null }
            or GuppyMultipleMovingAverage { SecondAverage: null, ThirdAverage: null, FourthAverage: null, FifthAverage: null, Average6Average: null, Average7Average: null, Average8Average: null, Average9Average: null, Average10Average: null, Average11Average: null, Average12Average: null, Average13Average: null, Average14Average: null, Average15Average: null, Average16Average: null, Average17Average: null, Average18Average: null, Average19Average: null, Average20Average: null, Average21Average: null, Average22Average: null, Average23Average: null, Average24Average: null, Average25Average: null, Average26Average: null, Average27Average: null, Average28Average: null, MaType: null }
            or GuppyCountBackLine { Length: <= 1_073_741_824 }
            or HirashimaSugitaRS { MaType: null }
            or HighLowIndex { MaType: null }
            or InternalBarStrengthIndicator
            or Inertia
            or InertiaIndicator { MaType: null }
            or HybridConvolutionFilter { Length: < int.MaxValue }
            or JmaRsxClone
            or JrcFractalDimension { MaType: null }
            or JapaneseCorrelationCoefficient { MaType: null, SecondAverage: null, ThirdAverage: null }
            or MayerMultiple { MaType: null }
            or OceanIndicator { MaType: null }
            or KaseIndicator { MaType: null }
            or KaseConvergenceDivergence { MaType: null }
            or KasePeakOscillatorV1
            or KasePeakOscillatorV2 { MaType: null }
            or KarobeinOscillator { MaType: null } or KwanIndicator { MaType: null }
            or LindaRaschke310Oscillator { MaType: null, SecondAverage: null, ThirdAverage: null, FourthAverage: null }
            or LinearQuadraticConvergenceDivergenceOscillator
            or MotionSmoothnessIndex
            or KurtosisIndicator
            or MarketDirectionIndicator or MassThrust or MassThrustIndicator { MaType: null } or MassThrustOscillator { MaType: null }
            or MobilityOscillator { MaType: null }
            or McClellanOscillator { MaType: null } or MidpointOscillator { MaType: null }
            or ModifiedGannHiloActivator { MaType: null } or MorphedSineWave or MoveTracker
            or MomentaRelativeStrengthIndex { MaType: null, SecondAverage: null, ThirdAverage: null }
            or MultiVoteOnBalanceVolume { MaType: null }
            or NormalizedRelativeVigorIndex { MaType: null, SecondAverage: null, ThirdAverage: null } or NthOrderDifferencingOscillator
            or NarrowBandpassFilter
            or NaturalMarketSlope
            or NaturalDirectionalIndex { MaType: null } or NaturalStochasticIndicator { MaType: null }
            or NaturalDirectionalCombo { MaType: null }
            or NaturalMarketMirror { MaType: null } or NaturalMarketRiver { MaType: null } or NaturalMarketCombo { MaType: null }
            or MirroredMovingAverageConvergenceDivergence { MaType: null, SecondAverage: null, ThirdAverage: null, FourthAverage: null }
            or MirroredPercentagePriceOscillator { MaType: null, SecondAverage: null, ThirdAverage: null, FourthAverage: null }
            or MovingAverageAdaptiveQ
            or MovingAverageV3 { MaType: null, SecondAverage: null }
            or MultiDepthZeroLagExponentialMovingAverage
            or NormalizedMacd or NickRypockTrailingReverse
            or MovingAverageConvergenceDivergenceLeader { MaType: null, SecondAverage: null, ThirdAverage: null, FourthAverage: null, FifthAverage: null }
            or PercentagePriceOscillatorLeader
            or PercentChangeOscillator { MaType: null } or PerformanceIndex
            or PremierStochasticOscillator { MaType: null }
            or PrettyGoodOscillator { MaType: null, SecondAverage: null }
            or PriceZoneOscillator { MaType: null, SecondAverage: null }
            or PriceVolumeRank { MaType: null, SecondAverage: null }
            or PsychologicalLine
            or PringSpecialK { MaType: null, SecondAverage: null, ThirdAverage: null, FourthAverage: null, FifthAverage: null, Average6Average: null, Average7Average: null, Average8Average: null, Average9Average: null, Average10Average: null, Average11Average: null, Average12Average: null, Average13Average: null }
            or PercentageTrend { Length: > 0 and <= 1024, Pct: > double.NegativeInfinity and < double.PositiveInfinity }
            or PentupleExponentialMovingAverage or QuadrupleExponentialMovingAverage
            or PpoMa or QmaSmaDifference or QuadraticMovingAverage
            or ParabolicWma or QuadraticWma
            or ParametricCorrectiveLinearMovingAverage or ParametricKalmanFilter
            or PeakValleyEstimation { MaType: null }
            or ReallySimpleIndicator { MaType: null, SecondAverage: null }
            or RexOscillator { MaType: null, SecondAverage: null }
            or RelativeDifferenceOfSquaresOscillator
            or RecursiveDifferenciator { MaType: null, Alpha: > double.NegativeInfinity and < double.PositiveInfinity }
            or RecursiveRelativeStrengthIndex { MaType: null }
            or RecursiveStochastic { Alpha: >= double.NegativeInfinity and <= double.PositiveInfinity }
            or RelativeVigorIndex { MaType: null, SecondAverage: null }
            or Repulse { MaType: null, SecondAverage: null, ThirdAverage: null }
            or RangeIdentifier or RatioOchlAverager
            or RangeActionVerificationIndex { MaType: null, SecondAverage: null }
            or RelativeSpreadStrength { MaType: null, SecondAverage: null, ThirdAverage: null }
            or RetrospectiveCandlestickChart
            or ReverseEngineeringRsi { Length: >= 2, RsiLevel: > 0 and < 100 }
            or RetentionAccelerationFilter
            or RobustWeightingOscillator { MaType: null, SecondAverage: null, ThirdAverage: null }
            or ReverseMovingAverageConvergenceDivergence
            or SimpleCycle or SimplePriceZone or SigmaSpikes { MaType: null }
            or SimpleLines { Multiplier: > double.NegativeInfinity and < double.PositiveInfinity }
            or SentimentZoneOscillator { MaType: null }
            or RandomWalkIndex { MaType: null }
            or RSINGIndicator { MaType: null, SecondAverage: null }
            or SellGravitationIndex { MaType: null, SecondAverage: null }
            or RainbowOscillator { MaType: null }
            or SchaffTrendCycle { MaType: null } or SchaffTrendCycleShk { MaType: null }
            or StationaryExtrapolatedLevels { MaType: null } or StationaryExtrapolatedLevelsOscillator { MaType: null }
            or SettingLessTrendStepFiltering or SqueezeMomentumIndicator { MaType: null }
            or SharpModifiedMovingAverage { MaType: null }
            or SequentiallyFilteredMovingAverage { MaType: null }
            or ReversalPoints { MaType: null }
            or ShapeshiftingMovingAverage { Length: <= 1_073_741_824 }
            or SmoothedRateOfChange { MaType: null }
            or SimplifiedWeightedMovingAverage
            or SmoothedDeltaRatioOscillator { MaType: null }
            or SuperTrendFilter { Factor: >= 0 and <= 1 }
            or SurfaceRoughnessEstimator { MaType: null, SecondAverage: null }
            or SupportAndResistanceOscillator
            or StiffnessIndicator { MaType: null }
            or StrengthOfMovement { MaType: null, SecondAverage: null }
            or SuperTrend { MaType: null }
            or SwingIndex { LimitMove: > double.NegativeInfinity and < double.PositiveInfinity }
            or TimePriceIndicator
            or TickLineMomentumOscillator { MaType: null, SecondAverage: null }
            or TFSTetherLineIndicator
            or TFSMboIndicator { MaType: null, SecondAverage: null, ThirdAverage: null }
            or TFSMboPercentagePriceOscillator { MaType: null, SecondAverage: null, ThirdAverage: null }
            or TopsAndBottomsFinder { MaType: null }
            or TrendDirectionForceIndex { MaType: null, SecondAverage: null, Length2: < int.MaxValue }
            or TrendAnalysisIndex { MaType: null }
            or TrendAnalysisIndicator { MaType: null }
            or TrendDetectionIndex
            or Trender { MaType: null, AtrMult: > double.NegativeInfinity and < double.PositiveInfinity }
            or TrendForceHistogram
            or TrendImpulseFilter { MaType: null }
            or TrendPersistenceRate
            or TrendExhaustionIndicator { MaType: null }
            or TrueRangeAdjustedExponentialMovingAverage { Mult: > double.NegativeInfinity and < double.PositiveInfinity }
            or Trimean
            or TreynorRatio { Beta: > double.NegativeInfinity and < double.PositiveInfinity, Bmk: > double.NegativeInfinity and < double.PositiveInfinity }
            or UpsidePotentialRatio { Bmk: > double.NegativeInfinity and < double.PositiveInfinity }
            or TTMScalperIndicator
            or TurboScaler { MaType: null, PctMultiplier: > double.NegativeInfinity and < double.PositiveInfinity }
            or UberTrendIndicator
            or VaradiOscillator { MaType: null }
            or UhlMaCrossoverSystem { MaType: null }
            or UltimateMovingAverage { MaType: null }
            or UltimateMovingAverageBands { MaType: null, StdDevMult: > double.NegativeInfinity and < double.PositiveInfinity }
            or VariableIndexDynamicAverage { MaType: null } or Vidya { MaType: null }
            or Vma or VariableMovingAverageBands { MaType: null, SecondAverage: null, Mult: > double.NegativeInfinity and < double.PositiveInfinity }
            or WaddahAttarExplosion { Sensitivity: > double.NegativeInfinity and < double.PositiveInfinity }
            or WaveTrendOscillator
            or VolatilityMovingAverage { MaType: null, SecondAverage: null, ThirdAverage: null }
            or ZweigMarketBreadthIndicator { MaType: null }
            or ZeroLagSmoothedCycle
            or VostroIndicator { MaType: null, Level: > double.NegativeInfinity and < double.PositiveInfinity }
            or WilliamsFractalUp or WilliamsFractalDown or WilliamsFractalsUp or WilliamsFractalsDown or WilliamsFractals
            or WoodiePivotPoint
            or CamarillaPivotPoint or FibonacciPivotPoint or DemarkPivotPoint or FloorPivotPoint
            or ElasticVolumeWeightedMovingAverageV1 { MaType: null } or ElasticVolumeWeightedMovingAverageV2
            or StandardPivotPoints or DynamicPivotPoints
            or EhlersAdaptiveCyberCycle { Alpha: > 0 and < 1 }
            or EhlersAdaptiveCenterOfGravityOscillator
            or EhlersSmoothedAdaptiveMomentum { MaType: MovingAvgType.SimpleMovingAverage or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WeightedMovingAverage }
            or EhlersAdaptiveRsiFisherTransformV1
            or EhlersAdaptiveRelativeStrengthIndexV1 { CycPart: > 0 and < double.PositiveInfinity }
            or EhlersAdaptiveStochasticIndicatorV1 { CycPart: > 0 and < double.PositiveInfinity }
            or EhlersAdaptiveCommodityChannelIndexV1 { CycPart: > 0 and < double.PositiveInfinity, Constant: > 0 and < double.PositiveInfinity }
            or EhlersAutoCorrelationIndicator or EhlersAutoCorrelationPeriodogram
            or EhlersAdaptiveBandPassFilter { Bw: >= 0 and < double.PositiveInfinity }
            or EhlersZeroCrossingsDominantCycle { Bw: >= 0 and < double.PositiveInfinity }
            or EhlersAdaptiveRelativeStrengthIndexV2 or EhlersAdaptiveRsiFisherTransformV2
            or EhlersAdaptiveStochasticIndicatorV2 or EhlersAdaptiveStochasticInverseFisherTransform or EhlersAdaptiveCommodityChannelIndexV2
            or EhlersEvenBetterSineWaveIndicator
            or EhlersReverseEmaIndicatorV1 { Alpha: >= double.NegativeInfinity and <= double.PositiveInfinity }
            or EhlersReverseEmaIndicatorV2 { TrendAlpha: >= double.NegativeInfinity and <= double.PositiveInfinity, CycleAlpha: >= double.NegativeInfinity and <= double.PositiveInfinity }
            or EhlersUniversalOscillator
            or EhlersUniversalTradingFilter { Mult: > double.NegativeInfinity and < double.PositiveInfinity }
            or EhlersSnakeUniversalTradingFilter { Bw: > double.NegativeInfinity and < double.PositiveInfinity }
            or EhlersAMDetector
            or EhlersKaufmanAdaptiveMovingAverage
            or EhlersSimpleCycleIndicator { Alpha: > double.NegativeInfinity and < double.PositiveInfinity }
            or EhlersFilter or EhlersAverageErrorFilter or EhlersDistanceCoefficientFilter or EhlersReflexIndicator or EhlersTrendflexIndicator
            or EhlersDeviationScaledMovingAverage { Length: <= 1_073_741_823 }
            or EhlersFisherizedDeviationScaledOscillator or EhlersDeviationScaledSuperSmoother
            or TypicalPriceVolatility or StandardDeviationVolatility or ClosedFormDistanceVolatility
            or ParkinsonVolatility { Length: > 0 and <= 536_870_911 } or RogersSatchellVolatility { Length: > 0 }
            or YangZhangVolatility { Length: < int.MaxValue }
            or EhlersRecursiveMedianFilter or EhlersRecursiveMedianOscillator
            or EhlersMedianAverageAdaptiveFilter { Threshold: > double.NegativeInfinity and < double.PositiveInfinity }
            or VolatilityIndexDynamicAverageIndicator { Alpha1: >= 0 and < double.PositiveInfinity, Alpha2: >= 0 and < double.PositiveInfinity }
            or ChandeVolatilityIndexDynamicAverageIndicator { Alpha1: >= 0 and < double.PositiveInfinity, Alpha2: >= 0 and < double.PositiveInfinity }
            or RelativeVolatilityIndex or RviVolatility or RelativeVolatilityIndexV2
            or RelativeVolatilityIndexHigh or RelativeVolatilityIndexLow
            or VolatilityStop { Multiplier: > double.NegativeInfinity and < double.PositiveInfinity }
            or WellesWilderVolatilitySystem { Factor: > double.NegativeInfinity and < double.PositiveInfinity }
            or CloseToCloseVolatility or DownsideDeviation
            or PrimeNumberOscillator or PrimeNumberBands
            or EfficientTrendStepChannel
            or LinearChannels { Mult: > double.NegativeInfinity and < double.PositiveInfinity }
            or VervoortVolatilityBands { DevMult: > double.NegativeInfinity and < double.PositiveInfinity, LowBandMult: > double.NegativeInfinity and < double.PositiveInfinity }
            or VervoortModifiedBollingerBandIndicator { StdDevMult: > double.NegativeInfinity and < double.PositiveInfinity }
            or UniChannel { UbFac: > double.NegativeInfinity and < double.PositiveInfinity, LbFac: > double.NegativeInfinity and < double.PositiveInfinity }
            or PseudoPolynomialChannel { Morph: > double.NegativeInfinity and < double.PositiveInfinity }
            or HurstCycleChannel { FastMult: > double.NegativeInfinity and < double.PositiveInfinity, SlowMult: > double.NegativeInfinity and < double.PositiveInfinity }
            or HurstBands { InnerMult: > double.NegativeInfinity and < double.PositiveInfinity, OuterMult: > double.NegativeInfinity and < double.PositiveInfinity, ExtremeMult: > double.NegativeInfinity and < double.PositiveInfinity }
            or SmoothedVolatilityBands { Length1: <= 1_073_741_824, Deviation: > double.NegativeInfinity and < double.PositiveInfinity, BandAdjust: > double.NegativeInfinity and < double.PositiveInfinity }
            or HighLowMovingAverage
            or MovingAverageSupportResistance or MovingAverageChannel
            or HighLowBands { PctShift: > double.NegativeInfinity and < double.PositiveInfinity }
            or MovingAverageEnvelope { Pct: > double.NegativeInfinity and < double.PositiveInfinity }
            or MovingAverageDisplacedEnvelope { Pct: > double.NegativeInfinity and < double.PositiveInfinity }
            or SmartEnvelope { Factor: > double.NegativeInfinity and < double.PositiveInfinity }
            or DEnvelope { DevFactor: > double.NegativeInfinity and < double.PositiveInfinity } or GChannels
            or ChandeTrendScore { Length: < int.MaxValue } or TrendScore { Length: < int.MaxValue }
            or KeltnerChannelMiddle or KeltnerChannels { MultFactor: > double.NegativeInfinity and < double.PositiveInfinity }
            or PriceCycleOscillator
            or Qstick or DailyAveragePriceDelta or DrunkardWalk
            or DonchianChannel or DonchianChannelUpper or DonchianChannelLower
            or DonchianChannelWidth
            or ChandeQuickStick or DeltaMovingAverage
            or CumulativeSum or CumulativeVolumeIndex
            or ConditionalAccumulator
            or ContractHigh or ContractLow
            or IntradayMomentumIndex or ChandeIntradayMomentumIndex or RelativeMomentumIndex
            or Adr or AverageDayRange
            or AlligatorJaw or AlligatorTeeth or AlligatorLips
            or BullPower or BearPower or BullPowerIndicator or BearPowerIndicator
            or AutoLine or AutoLineWithDrift
            or AutoFilter or AutoDispersionBands
            or AwesomeOscillator or AcceleratorOscillator
            or AnchoredMomentum
            or AccumulativeSwingIndex { LimitMove: > double.NegativeInfinity and < double.PositiveInfinity }
            or AbsoluteStrengthIndex or AbsoluteStrengthMTFIndicator
            or WilsonRelativePriceChannel { Overbought: > double.NegativeInfinity and < double.PositiveInfinity,
                Oversold: > double.NegativeInfinity and < double.PositiveInfinity,
                UpperNeutralZone: > double.NegativeInfinity and < double.PositiveInfinity,
                LowerNeutralZone: > double.NegativeInfinity and < double.PositiveInfinity }
            or Vpci or VolumeWeightedRsi or VortexBands
            or WoodieCommodityChannelIndex
            or EhlersCommodityChannelIndexInverseFisherTransform { Constant: > 0d and < double.PositiveInfinity }
            or ZScore or FastZScore or InverseFisherZScore or InverseFisherFastZScore
            or ZDistanceFromVwap or WamiOscillator or PriceMomentum or VolumeMomentum
            or VolumeZoneOscillator or VolumeMomentumOscillator
            or VortexPositive or VortexNegative or VortexPlus or VortexMinus or VortexIndicatorPlus or VortexIndicatorMinus
            or UltimateMomentumIndicator { StdDevMult: > double.NegativeInfinity and < double.PositiveInfinity }
            or FiniteVolumeElements { Factor: > double.NegativeInfinity and < double.PositiveInfinity }
            or VolumeFlowIndicator { Coef: > double.NegativeInfinity and < double.PositiveInfinity, Vcoef: > double.NegativeInfinity and < double.PositiveInfinity }
            or VolumeAdjustedMa or VolumeAdjustedMovingAverage { Factor: > double.NegativeInfinity and < double.PositiveInfinity }
            or AdaptiveEma or AdaptiveExponentialMovingAverage
            or BryantAdaptiveMovingAverage { Length: >= int.MinValue / 4 and <= int.MaxValue / 4 }
            or VariableAdaptiveMovingAverage
            or AdaptivePriceZoneIndicator { Pct: > double.NegativeInfinity and < double.PositiveInfinity }
            or AutonomousRecursiveMa
            or AdaptiveAutonomousRecursiveMovingAverage { Lambda: > double.NegativeInfinity and < double.PositiveInfinity }
            or AdaptiveAutonomousRecursiveTrailingStop { Lambda: > double.NegativeInfinity and < double.PositiveInfinity }
            or R2AdaptiveRegression or KaufmanAdaptiveLeastSquaresMovingAverage or RegressionOscillator or KaufmanAdaptiveCorrelationOscillator
            or OneLCLeastSquaresMovingAverage { MaType: MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod }
            or _1LCLeastSquaresMovingAverage { MaType: MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage or MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod }
            or Lsma or LinReg or LinearChannelMiddle or LinRegSlope or LinearRegressionSlope or LinRegIntercept or LinearRegressionIntercept or LinearRegressionLine;

    private static object CreateGeneratedState(IIndicator indicator, int count, bool volumeFlowCustomRange, bool valueChartCustomRange, bool vervoortModifiedCustomRange) => indicator switch
    {
        PivotDetectorOscillator => new GeneratedPivotDetectorState(),
        TheRangeIndicator range => new GeneratedRangeIndicatorState(range.Length),
        ImpulseMovingAverageConvergenceDivergence impulse => new GeneratedImpulsePairState(impulse.Length, impulse.SignalLength, count, false),
        ImpulsePercentagePriceOscillator impulse => new GeneratedImpulsePairState(impulse.Length, 9, count, true),
        DecisionPointBreadthSwenlinTradingOscillator breadth => new GeneratedSwenlinState(breadth.Length),
        OscOscillator oscillator => new GeneratedOscState(oscillator.Length, count),
        UtBotAlerts bot => new GeneratedUtBotState(bot.Length, bot.KeyValue),
        OptimizedTrendTracker trend => new GeneratedOptimizedTrendState(trend.Length, trend.Percent),
        MartinRatio martin => new GeneratedMartinRatioState(martin.Length, martin.Bmk, count),
        MiddleHighLowMovingAverage average => new GeneratedMiddleMeanState(average.Length1, average.Length2, count),
        OvershootReductionMovingAverage average => new GeneratedOvershootState(average.Length, count),
        OscarIndicator oscar => new GeneratedOscarState(oscar.Length),
        PhaseChangeIndex phase => new GeneratedPhaseChangeState(phase.Length, phase.SmoothLength),
        TurboTrigger turbo => new GeneratedTurboFollowupState(turbo.Length, 1, 0, count, 0),
        TurboStochasticsFast turbo => new GeneratedTurboFollowupState(turbo.Length1, turbo.Length2, turbo.TurboLength, count, 1),
        TurboStochasticsSlow turbo => new GeneratedTurboFollowupState(turbo.Length1, turbo.Length2, turbo.TurboLength, count, 2),
        DominantCycleTunedRelativeStrengthIndex cycle => new GeneratedCycleTunedRsiState(cycle.Length),
        ThreeHMA hull => new GeneratedThreeHullState(hull.Length, count),
        ThreeHma hull => new GeneratedThreeHullState(hull.Length, count),
        TripleHullMovingAverage hull => new GeneratedThreeHullState(hull.Length, count),
        _3HMA hull => new GeneratedThreeHullState(hull.Length, count),
        SelfAdjustingRelativeStrengthIndex selfRsi => new GeneratedSelfAdjustingRsiState(selfRsi.Length, selfRsi.SmoothingLength, selfRsi.Mult, count),
        ConnorsRsi connors => new GeneratedConnorsFollowupState(2, connors.Length, 100, 3, 3, count, false),
        ConnorsRelativeStrengthIndex connors => new GeneratedConnorsFollowupState(connors.Length1, connors.Length2, connors.Length3, 3, 3, count, false),
        StochasticConnorsRelativeStrengthIndex connors => new GeneratedConnorsFollowupState(connors.Length1, connors.Length2, connors.Length3, connors.SmoothLength1, connors.SmoothLength2, count, true),
        OnBalanceVolumeModified volume => new GeneratedObvFollowupState(volume.Length1, volume.Length2, false),
        OnBalanceVolumeReflex volume => new GeneratedObvFollowupState(volume.Length, volume.SignalLength, true),
        T3 tillson => new GeneratedTillsonAliasState(tillson.Length, .7),
        TillsonT3MovingAverage tillson => new GeneratedTillsonAliasState(tillson.Length, tillson.VFactor),
        Pfe efficiency => new GeneratedPolarizedEfficiencyState(efficiency.Length, 5),
        PolarizedFractalEfficiency efficiency => new GeneratedPolarizedEfficiencyState(efficiency.Length, efficiency.SmoothLength),
        RapidRelativeStrengthIndex oscillator => new GeneratedRsiFollowupState(oscillator.Length, 1, 1, oscillator.Length, count, true),
        StochRsi oscillator => new GeneratedRsiFollowupState(oscillator.RsiLength, oscillator.StochLength, 3, 3, count, false),
        StochasticRsiOscillator oscillator => new GeneratedRsiFollowupState(oscillator.RsiLength, oscillator.StochLength, 3, 3, count, false),
        StochasticRelativeStrengthIndex oscillator => new GeneratedRsiFollowupState(oscillator.Length, oscillator.Length, oscillator.SmoothLength1, oscillator.SmoothLength2, count, false),
        StochasticCustomOscillator oscillator => new GeneratedStochasticComponentsState(oscillator.Length, 3, 1, 12, count, false),
        StochasticMomentumIndex oscillator => new GeneratedStochasticComponentsState(oscillator.Length1, oscillator.Length2, oscillator.SmoothLength1, oscillator.SmoothLength2, count, true),
        ParabolicSar sar => new GeneratedParabolicSarState(sar.Start, sar.Increment, sar.Maximum),
        Stochastic oscillator => new GeneratedStochasticState(oscillator.KLength, oscillator.DLength, 3, MovingAvgType.SimpleMovingAverage, 0, count),
        StochasticK oscillator => new GeneratedStochasticState(oscillator.Length, oscillator.SmoothLength1, oscillator.SmoothLength2, MovingAvgType.SimpleMovingAverage, 0, count),
        PricePosition oscillator => new GeneratedStochasticState(oscillator.Length, oscillator.SmoothLength1, oscillator.SmoothLength2, MovingAvgType.SimpleMovingAverage, 0, count),
        StochasticOscillator oscillator => new GeneratedStochasticState(oscillator.Length, oscillator.SmoothLength1, oscillator.SmoothLength2, MovingAvgType.SimpleMovingAverage, 0, count),
        StochasticFastOscillator oscillator => new GeneratedStochasticState(oscillator.Length, oscillator.SmoothLength1, oscillator.SmoothLength2, MovingAvgType.ExponentialMovingAverage, 2, count),
        StochasticRegular oscillator => new GeneratedStochasticState(oscillator.Length1, oscillator.Length2, 1, MovingAvgType.SimpleMovingAverage, 1, count),
        StochasticD oscillator => new GeneratedStochasticDState(oscillator.Length, count),
        ShinoharaIntensityRatioA ratio => new GeneratedShinoharaState(ratio.Length, count, false),
        ShinoharaIntensityRatioB ratio => new GeneratedShinoharaState(ratio.Length, count, true),
        Kst bank => new GeneratedKstState([10, 15, 20, 30], [bank.Length, 10, 10, 15], 9, count),
        KnowSureThing bank => new GeneratedKstState([bank.RocLength1, bank.RocLength2, bank.RocLength3, bank.RocLength4],
            [bank.Length1, bank.Length2, bank.Length3, bank.Length4], bank.SignalLength, count),
        IchimokuTenkanSen line => new GeneratedIchimokuState(line.Length, count, 0),
        IchimokuKijunSen line => new GeneratedIchimokuState(line.Length, count, 1),
        IchimokuSenkouSpanA line => new GeneratedIchimokuState(line.Length, count, 2),
        IchimokuSenkouSpanB line => new GeneratedIchimokuState(line.Length, count, 3),
        IchimokuChikouSpan line => new GeneratedIchimokuState(line.Length, count, 4),
        KaseSerialDependencyIndex index => new GeneratedKaseSerialState(index.Length, count),
        LiquidRelativeStrengthIndex oscillator => new GeneratedNaturalLiquidState(oscillator.Length),
        Kama average => new GeneratedKamaState(average.Length, count),
        Jma average => new GeneratedRecursiveAliasState(average.Length),
        FloorPivotPointS1 => new GeneratedFloorAliasScalarState(1),
        FloorPivotPointR1 => new GeneratedFloorAliasScalarState(4),
        PivotPoint => new GeneratedFloorAliasState(),
        GatorOscillator gator => new GeneratedGatorMeannessState(gator.Length, false),
        MarketMeannessIndex meanness => new GeneratedGatorMeannessState(meanness.Length, true),
        GOscillator oscillator => new GeneratedGOscillatorState(oscillator.Length, count),
        GannSwingOscillator swing => new GeneratedGannTrendState(Math.Max(1, Math.Min(swing.Length, count))),
        GeneralFilterEstimator estimator => new GeneratedGeneralFilterState(estimator.Length),
        ElasticVolumeWeightedMovingAverageV1 average => new GeneratedElasticVolumeState(average.Length, count, 0),
        ElasticVolumeWeightedMovingAverageV2 average => new GeneratedElasticVolumeState(average.Length, count, 1),
        InformationRatio information => new GeneratedRiskRatioState(information.Length, information.Bmk, count, 0),
        SharpeRatio sharpe => new GeneratedRiskRatioState(sharpe.Length, sharpe.Bmk, count, 1),
        SortinoRatio sortino => new GeneratedRiskRatioState(sortino.Length, sortino.Bmk, count, 2),
        Midpoint midpoint => new GeneratedRangeStatisticsState(midpoint.Length, count, 2),
        Midprice midprice => new GeneratedRangeStatisticsState(midprice.Length, count, 3),
        NaturalMa natural => new GeneratedWindowMeansState(natural.Length, count, 0),
        FareySequenceWeightedMovingAverage farey => new GeneratedWindowMeansState(farey.Length, count, 1),
        OptimalWeightedMovingAverage optimal => new GeneratedWindowMeansState(optimal.Length, count, 2),
        Skewness skew => new GeneratedWindowStatisticsState(skew.Length, count, 0),
        PercentRank rank => new GeneratedWindowStatisticsState(rank.Length, count, 1),
        MedianValue median => new GeneratedWindowStatisticsState(median.Length, count, 2),
        MedianMa median => new GeneratedWindowStatisticsState(median.Length, count, 3),
        GeoMa geometric => new GeneratedGeometricMeansState(geometric.Length, count, false),
        GeometricMeanMovingAverage geometric => new GeneratedGeometricMeansState(geometric.Length, count, true),
        LinearWeightedMovingAverageCore average => new GeneratedAverageCompositionsState(average.Length, count, 0),
        LeoMovingAverage average => new GeneratedAverageCompositionsState(average.Length, count, 1),
        McGinleyDynamic average => new GeneratedAverageCompositionsState(average.Length, count, 2),
        KendallRankCorrelationCoefficient correlation => new GeneratedCorrelationStatisticsState(correlation.Length, count, false),
        RSquared correlation => new GeneratedCorrelationStatisticsState(correlation.Length, count, true),
        NormalizedVolume volume => new GeneratedNormalizedVolumeState(volume.Length, count),
        OCHistogram histogram => new GeneratedOcHistogramState(histogram.Length),
        ModifiedPriceVolumeTrend trend => new GeneratedModifiedPriceVolumeTrendState(trend.Length),
        OmegaRatio omega => new GeneratedTargetReturnState(omega.Length, omega.Bmk, false, count),
        RollingMax maximum => new GeneratedRollingCloseExtremeState(maximum.Length, count, true),
        RollingMin minimum => new GeneratedRollingCloseExtremeState(minimum.Length, count, false),
        SimpleReturns returns => new GeneratedLaggedReturnState(returns.Length, count, false),
        LogReturns returns => new GeneratedLaggedReturnState(returns.Length, count, true),
        RunningEquity equity => new GeneratedRunningEquityState(equity.Length),
        NetVolume => new GeneratedNetVolumeState(),
        Range => new GeneratedRawRangeState(),
        MarketFacilitationIndex => new GeneratedMarketFacilitationState(),
        HighestHigh high when high.Length >= count => new PrefixPriceExtremeState(true),
        LowestLow low when low.Length >= count => new PrefixPriceExtremeState(false),
        HighestHigh high => new PriceExtremeState(high.Length, count, true),
        LowestLow low => new PriceExtremeState(low.Length, count, false),
        TrueRange => new TrueRangeValueState(),
        BalanceOfPower power => new BalanceOfPowerValueState(Math.Max(1, power.Length)),
        Wma average => new WmaValueState(Math.Max(1, average.Length), count),
        WilliamsR range => new WilliamsValueState(Math.Max(1, range.Length), count),
        Ema average => new EmaValueState(average.Length),
        Wwma average => new WilderValueState(average.Length),
        Smma average => new WilderValueState(average.Length),
        ModifiedMa average => new WilderValueState(average.Length),
        Dema average => new ExtrapolatedEmaValueState(average.Length, triple: false),
        Tema average => new ExtrapolatedEmaValueState(average.Length, triple: true),
        Zlema average => new ExtrapolatedEmaValueState(average.Length, triple: false),
        Tma average => new GeneratedTriangularState(average.Length),
        Hma average => new HullValueState(average.Length),
        Rsi strength => new GeneratedRsiState(strength.Length, count),
        Atr range => new GeneratedAtrState(range.Length, count),
        AverageTrueRange range => new GeneratedAtrState(range.Length, count),
        ChandeMomentumOscillator momentum => new GeneratedChandeState(momentum.Length, count, momentum.SignalLength),
        RateOfChange change => new GeneratedRatioState(Math.Max(1, change.Length), count),
        DetrendedPriceOscillator oscillator => new GeneratedDetrendedState(Math.Max(1, oscillator.Length), count),
        Roc change => new GeneratedRatioState(Math.Max(1, change.Length), count),
        Momentum momentum => new GeneratedMomentumState(Math.Max(1, momentum.Length), count),
        Cci commodity => new GeneratedCommodityState(commodity.Length, count),
        Cmo momentum => new GeneratedChandeState(momentum.Length, count),
        Trix trix => new GeneratedTrixState(trix.Length),
        Apo oscillator => new GeneratedEmaGapState(oscillator.FastLength, oscillator.SlowLength),
        AbsolutePriceOscillator oscillator => new GeneratedEmaGapState(oscillator.FastLength, oscillator.SlowLength),
        PriceOscillator oscillator => new GeneratedEmaGapState(oscillator.ShortLength, oscillator.LongLength),
        PriceOscillatorPercent oscillator => new GeneratedEmaGapSignalState(oscillator.ShortLength, oscillator.LongLength, oscillator.SignalLength, percentage: true),
        PercentagePriceOscillator oscillator => new GeneratedEmaGapSignalState(oscillator.FastLength, oscillator.SlowLength, oscillator.SignalLength, percentage: true),
        Ppo oscillator => new GeneratedEmaGapSignalState(oscillator.FastLength, oscillator.SlowLength, oscillator.SignalLength, percentage: true),
        Pvo oscillator => new GeneratedVolumeGapState(oscillator.Length, 26, oscillator.SignalLength),
        PercentageVolumeOscillator oscillator => new GeneratedVolumeGapState(oscillator.FastLength, oscillator.SlowLength, oscillator.SignalLength),
        VolumeOscillator oscillator => new GeneratedVolumeSmaGapState(oscillator.Length, count),
        PriceVolumeOscillator oscillator => new GeneratedPriceVolumeBalanceState(oscillator.Length, count),
        Macd oscillator => new GeneratedEmaGapSignalState(oscillator.FastLength, oscillator.SlowLength, oscillator.SignalLength),
        Aroon oscillator => new GeneratedAroonState(Math.Max(1, oscillator.Length)),
        Dpo oscillator => new GeneratedDetrendedState(Math.Max(1, oscillator.Length), count),
        Cmf flow => new GeneratedChaikinFlowState(flow.Length),
        Mfi flow => new GeneratedMoneyFlowState(flow.Length, count),
        ForceIndex force => new GeneratedForceState(force.Length),
        Obv volume => new GeneratedObvState(volume.Length),
        OnBalanceVolume volume => new GeneratedObvState(volume.Length),
        NegativeVolumeIndex volume => new GeneratedVolumeIndexState(volume.Length, false, volume.InitialValue),
        PositiveVolumeIndex volume => new GeneratedVolumeIndexState(volume.Length, true, volume.InitialValue),
        PriceVolumeTrend volume => new GeneratedVolumeTrendState(volume.Length),
        Nvi volume => new GeneratedVolumeIndexState(volume.Length, positive: false),
        Pvi volume => new GeneratedVolumeIndexState(volume.Length, positive: true),
        Vroc volume => new GeneratedVolumeRatioState(Math.Max(1, volume.Length), count),
        Pvt volume => new GeneratedVolumeTrendState(volume.Length),
        Adx direction => new GeneratedDirectionalState(direction.Length, count),
        MassIndex mass => new GeneratedMassState(mass.EmaLength, mass.SumLength, count),
        ChaikinOscillator flow => new GeneratedChaikinOscillatorState(flow.FastLength, flow.SlowLength),
        Emv => new GeneratedEaseState(),
        EaseOfMovement ease => new GeneratedEaseState(ease.Divisor),
        AccumulationDistributionLine flow => new GeneratedAccumulationState(flow.Length),
        WilliamsAccumulationDistribution => new GeneratedWilliamsAccumulationState(),
        SmoothedWilliamsAccumulationDistribution flow => new GeneratedSmoothedWilliamsState(flow.Length, count),
        Vwap => new GeneratedVwapState(),
        AroonUp up => new GeneratedAroonLegState(Math.Max(1, up.Length), maximum: true),
        AroonDown down => new GeneratedAroonLegState(Math.Max(1, down.Length), maximum: false),
        Demarker marker => new GeneratedDemarkerState(marker.Length, count),
        DeMarker marker => new GeneratedDemarkerState(marker.Length, count),
        UlcerIndex drawdown => new GeneratedDrawdownState(drawdown.Length, count),
        ChoppinessIndex choppiness => new GeneratedChoppinessState(choppiness.Length),
        Natr range => new GeneratedAtrDerivedState(range.Length, count),
        AtrPercent range => new GeneratedAtrDerivedState(range.Length, count),
        AtrChannelWidth range => new GeneratedAtrDerivedState(range.Length, count, range.Multiplier),
        ChaikinVolatility volatility => new GeneratedChaikinVolatilityState(volatility.Length, count),
        ZeroLowLagMovingAverage average => new GeneratedZeroLowLagState(average.Length),
        ZeroLagTripleExponentialMovingAverage average => new GeneratedZeroLagTripleState(average.Length),
        SlowSmoothedMovingAverage average => new GeneratedSlowSmoothedState(average.Length, count),
        McNichollMovingAverage average => new GeneratedMcNichollState(average.Length),
        WellRoundedMovingAverage average => new GeneratedWellRoundedState(average.Length),
        EhlersBetterExponentialMovingAverage average => new GeneratedBetterEmaState(average.Length),
        HoltExponentialMovingAverage average => new GeneratedHoltState(average.Length),
        RegularizedEma average => new GeneratedRegularizedState(average.Length),
        EhlersAdaptiveLaguerreFilter filter => new GeneratedAdaptiveLaguerreState(filter.Length),
        EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha strength => new GeneratedSelfAdjustingLaguerreState(strength.Length),
        EhlersLaguerreFilter filter => new GeneratedLaguerreFilterState(filter.Length),
        EhlersLaguerreRsi strength => new GeneratedLaguerreRsiState(strength.Length),
        RepulsionMovingAverage average => new GeneratedRepulsionState(average.Length, count),
        CompoundRatioMovingAverage average => new GeneratedCompoundRatioState(average.Length),
        GeneralizedDoubleEma average => new GeneratedGeneralizedDoubleState(average.Length, .7),
        GeneralizedDoubleExponentialMovingAverage average => new GeneratedGeneralizedDoubleState(average.Length, average.VolumeFactor),
        DoubleExponentialSmoothing => new GeneratedDoubleSmoothingState(),
        LightLeastSquaresMovingAverage average => new GeneratedLightLeastSquaresState(average.Length, count),
        LinearExtrapolation extrapolation => new GeneratedLinearExtrapolationState(extrapolation.Length, count),
        RecursiveMovingTrendAverage average => new GeneratedRecursiveTrendState(average.Length),
        EhlersIirFilter filter => new GeneratedEhlersIirState(filter.Length),
        EhlersInfiniteImpulseResponseFilter filter => new GeneratedEhlersIirState(filter.Length),
        EhlersFirFilter filter => new GeneratedEhlersFirState(),
        EhlersFiniteImpulseResponseFilter filter => new GeneratedEhlersFirState(),
        EhlersOptimumEllipticFilter filter => new GeneratedEllipticState(false),
        EhlersModifiedOptimumEllipticFilter filter => new GeneratedEllipticState(true),
        Spencer15PointMovingAverage => new GeneratedSpencerState(false),
        Spencer21PointMovingAverage => new GeneratedSpencerState(true),
        DampedSineWaveWeightedFilter filter => new GeneratedDampedSineState(filter.Length, count),
        HendersonWeightedMovingAverage average => new GeneratedHendersonState(average.Length, count),
        RightSidedRickerMovingAverage average => new GeneratedRickerState(average.Length, count),
        FibonacciWeightedMovingAverage average => new GeneratedFibonacciMeanState(average.Length, count),
        SquareRootWeightedMovingAverage average => new GeneratedSquareRootMeanState(average.Length, count),
        SineWma average => new GeneratedSineMeanState(average.Length, count),
        TillsonIE2 average => new GeneratedTillsonIe2State(average.Length, count),
        IIRLeastSquaresEstimate average => new GeneratedIirLeastSquaresState(average.Length),
        HullEstimate average => new GeneratedHullEstimateState(average.Length, count),
        Ehlers2PoleButterworthFilterV1 filter => new GeneratedTwoPoleState(filter.Length, 0),
        Ehlers2PoleButterworthFilterV2 filter => new GeneratedTwoPoleState(filter.Length, 1),
        Ehlers2PoleSuperSmootherFilterV1 filter => new GeneratedTwoPoleState(filter.Length, 2),
        Ehlers2PoleSuperSmootherFilterV2 filter => new GeneratedTwoPoleState(filter.Length, 3),
        Ehlers3PoleButterworthFilterV1 filter => new GeneratedThreePoleState(filter.Length, 0),
        Ehlers3PoleButterworthFilterV2 filter => new GeneratedThreePoleState(filter.Length, 1),
        Ehlers3PoleSuperSmootherFilter filter => new GeneratedThreePoleState(filter.Length, 2),
        AtrFilteredEma average => new GeneratedAtrFilteredState(average.Length, 20, 10, 20, 5),
        AhrensMovingAverage average => new GeneratedAhrensState(average.Length),
        AlphaDecreasingEma => new GeneratedAlphaDecreasingState(),
        VariableLengthMovingAverage average => new GeneratedVariableLengthState(average.Length, average.MaxLength),
        UpsideDownsideVolume volume => new GeneratedVolumeBalanceState(volume.Length, VolumeBalanceKind.UpsideDownside),
        TFSVolumeOscillator volume => new GeneratedVolumeBalanceState(volume.Length, VolumeBalanceKind.Tfs),
        VolumeAccumulationOscillator volume => new GeneratedVolumeBalanceState(volume.Length, VolumeBalanceKind.Accumulation),
        VolumeFlowIndicator volume => new GeneratedVolumeFlowState(volume.Length1, volume.Length2, volume.SignalLength, volume.SmoothLength, volume.Coef, volume.Vcoef, volumeFlowCustomRange),
        FiniteVolumeElements volume => new GeneratedFiniteVolumeState(volume.Length, volume.Factor),
        RelativeVolumeIndicator volume => new GeneratedRelativeVolumeState(volume.Length),
        BetterVolumeIndicator volume => new GeneratedBetterVolumeState(volume.Length, volume.LbLength),
        AverageMoneyFlowOscillator flow => new GeneratedAverageMoneyFlowState(flow.Length),
        TradeVolumeIndex volume => new GeneratedTradeVolumeState(volume.Length),
        TrendContinuationFactor trend => new GeneratedTrendContinuationState(trend.Length),
        TrendTriggerFactor trend => new GeneratedTrendTriggerState(trend.Length),
        TrendIntensityIndex trend => new GeneratedTrendIntensityState(trend.Length),
        DemarkSetupIndicator setup => new GeneratedDemarkCountState(Math.Max(1, setup.Length), Math.Max(1, setup.Length), count),
        DemarkReversalPoints reversal => new GeneratedDemarkCountState(Math.Max(1, reversal.Length1), Math.Max(1, reversal.Length2), count),
        AverageTrueRangeChannel channel => new GeneratedRangeChannelState(channel.Length, channel.Multiplier, true, count),
        StollerAverageRangeChannels channel => new GeneratedRangeChannelState(channel.Length, channel.AtrMult, false, count),
        KeltnerChannelWidth channel => new GeneratedKeltnerWidthState(channel.Length, count),
        PriceChannel channel => new EmaValueState(Math.Max(1, channel.Length)),
        PriceChannelMiddle channel => new EmaValueState(Math.Max(1, channel.Length)),
        PriceChannelUpper channel => new GeneratedPriceBandState(Math.Max(1, channel.Length), 1),
        PriceChannelLower channel => new GeneratedPriceBandState(Math.Max(1, channel.Length), -1),
        PriceLineChannel channel => new GeneratedPriceDriftState(channel.Length, false, count),
        PriceCurveChannel channel => new GeneratedPriceDriftState(channel.Length, true, count),
        MovingAverageBands channel => new GeneratedMovingAverageBandState(channel.FastLength, channel.SlowLength, channel.Mult, false, count),
        MovingAverageBandWidth channel => new GeneratedMovingAverageBandState(channel.FastLength, channel.SlowLength, channel.Mult, true, count),
        PriceHeadleyAccelerationBands channel => new GeneratedHeadleyBandState(channel.Length, channel.Factor, count),
        RootMovingAverageSquaredErrorBands channel => new GeneratedRmseBandState(channel.Length, channel.StdDevFactor, count),
        MeanAbsoluteErrorBands channel => new GeneratedMaeBandState(Math.Max(1, channel.Length), channel.StdDevFactor, count),
        MeanAbsoluteDeviationBands channel => new GeneratedMadBandState(Math.Max(1, channel.Length), channel.StdDevFactor, count),
        InterquartileRangeBands channel => new GeneratedIqrBandState(Math.Max(1, channel.Length), channel.Mult, count),
        RangeBands channel => new GeneratedRangeBandState(Math.Max(1, channel.Length), channel.StdDevFactor, count),
        BollingerBands channel => new GeneratedBollingerState(Math.Max(1, channel.Length), Math.Max(.001, channel.StdDev), count, 0),
        BollingerBandsWidth channel => new GeneratedBollingerState(Math.Max(1, channel.Length), 2, count, 1),
        BollingerBandsPercentB channel => new GeneratedBollingerState(Math.Max(1, channel.Length), channel.Multiplier, count, 2),
        // The legacy facade ignores its obsolete Pct option and uses three deviations.
        NarrowSidewaysChannel channel => new GeneratedBollingerState(Math.Max(1, channel.Length), 3, count, 0),
        BollingerBandsMiddle channel => new GeneratedBollingerMiddleState(Math.Max(1, channel.Length), count),
        BollingerBandsAtr ratio => new GeneratedBollingerAtrState(MovingAvgType.SimpleMovingAverage, 22, Math.Max(1, ratio.Length), ratio.Multiplier, count),
        BollingerBandsAvgTrueRange ratio => new GeneratedBollingerAtrState(ratio.MaType, Math.Max(1, ratio.AtrLength), Math.Max(1, ratio.Length), ratio.StdDevMult, count),
        BollingerBandsFibonacciRatios channel => new GeneratedRangeChannelState(channel.Length, channel.FibRatio3, false, count),
        BollingerBandsWithAtrPct channel => new GeneratedAtrPercentBandState(channel.Length, channel.BbLength, channel.StdDevMult, count),
        KirshenbaumBands channel => new GeneratedKirshenbaumState(channel.Length1, channel.Length2, channel.StdDevFactor, count),
        TrendTraderBands channel => new GeneratedTrendTraderBandState(channel.Length, channel.Mult, channel.BandStep, count),
        StandardDeviationChannel channel => new GeneratedRegressionChannelState(Math.Max(1, channel.Length), count),
        RateOfChangeBands channel => new GeneratedRocBandState(channel.Length, channel.SmoothLength, count),
        ScalpersChannel channel => new GeneratedScalperChannelState(channel.Length1, channel.Length2, count),
        FlaggingBands channel => new GeneratedFlaggingBandState(channel.Length, count),
        FractalChaosBands => new GeneratedFractalChaosState(false),
        FractalChaosOscillator => new GeneratedFractalChaosState(true),
        ProjectionBands channel => new GeneratedProjectionFamilyState(IndicatorName.ProjectionBands, channel.Length),
        ProjectionOscillator oscillator => new GeneratedProjectionFamilyState(IndicatorName.ProjectionOscillator, oscillator.Length),
        ProjectionBandwidth bandwidth => new GeneratedProjectionFamilyState(IndicatorName.ProjectionBandwidth, bandwidth.Length),
        StatisticalVolatility volatility => new GeneratedStatisticalVolatilityState(volatility.Length1, volatility.Length2),
        VolatilityQualityIndex volatility => new GeneratedVolatilityQualityState(volatility.FastLength, volatility.SlowLength),
        VolatilityStop volatility => new GeneratedVolatilityStopState(volatility.Length, volatility.Multiplier),
        WellesWilderVolatilitySystem volatility => new GeneratedWilderVolatilityState(volatility.Length1, volatility.Length2, volatility.Factor),
        RelativeVolatilityIndex volatility => new GeneratedRelativeVolatilityState(volatility.Length, 14, false, count),
        RviVolatility volatility => new GeneratedRelativeVolatilityState(volatility.Length, 14, false, count),
        RelativeVolatilityIndexV2 volatility => new GeneratedRelativeVolatilityState(volatility.Length, volatility.SmoothLength, true, count),
        RelativeVolatilityIndexHigh volatility => new GeneratedSeededRelativeVolatilityState(true, Math.Max(1, volatility.Length), Math.Max(1, volatility.StdDevLength), count),
        RelativeVolatilityIndexLow volatility => new GeneratedSeededRelativeVolatilityState(false, Math.Max(1, volatility.Length), Math.Max(1, volatility.StdDevLength), count),
        VolatilityRatio volatility => new GeneratedVolatilityRatioState(volatility.Length),
        VolatilityWaveMovingAverage average => new GeneratedVolatilityWaveState(average.Length),
        GarmanKlassVolatility volatility => new GeneratedGarmanKlassState(volatility.Length, count),
        HistoricalVolatilityPercentile volatility => new GeneratedHistoricalRankState(volatility.Length, volatility.AnnualLength),
        StandardErrorCore error => new GeneratedMeanErrorState(error.Length, count),
        AdaptiveErgodicCandlestickOscillator candle => new GeneratedAdaptiveErgodicCandleState(candle.SmoothLength, candle.StochLength, candle.SignalLength, count),
        ErgodicCommoditySelectionIndex selection => new GeneratedErgodicSelectionState(selection.Length, selection.SmoothLength, selection.PointValue),
        ErgodicCandlestickOscillator candle => new GeneratedErgodicCandleState(candle.Length, count),
        ErgodicMovingAverageConvergenceDivergence oscillator => new GeneratedEmaGapSignalState(oscillator.Length1, oscillator.Length2, oscillator.Length3),
        ErgodicPercentagePriceOscillator oscillator => new GeneratedEmaGapSignalState(32, oscillator.Length, 5, percentage: true),
        ErgodicTrueStrengthIndexV2 strength => new GeneratedErgodicStrengthPairState([strength.Length1, strength.Length2, strength.Length3], [strength.Length4, strength.Length5, strength.Length6], strength.SignalLength, count),
        ErgodicMeanDeviationIndicator deviation => new GeneratedErgodicDeviationState(deviation.Length1, deviation.Length2, deviation.Length3, deviation.SignalLength),
        EhlersAllPassPhaseShifter filter => new GeneratedAllPassPhaseState(Math.Max(1, filter.Length)),
        EhlersCenterOfGravityOscillator filter => new GeneratedCenterGravityState(filter.Length, count),
        EhlersCenterofGravityOscillator filter => new GeneratedCenterGravityState(filter.Length, count),
        EhlersChebyshevLowPassFilter => new GeneratedChebyshevWaveState(),
        EhlersSimpleDerivIndicator filter => new GeneratedEhlersDerivState(filter.Length, filter.SignalLength),
        EhlersSimpleClipIndicator filter => new GeneratedEhlersClipState(filter.Length1, filter.Length3, filter.SignalLength),
        EhlersSimpleWindowIndicator filter => new GeneratedEhlersSimpleWindowState(filter.Length),
        EhlersTripleDelayLineDetrender filter => new GeneratedTripleDelayState(filter.Length),
        EhlersStochasticCenterOfGravityOscillator oscillator => new GeneratedStochasticGravityState(oscillator.Length, count),
        EhlersStochastic oscillator => new GeneratedRoofingStochasticState(oscillator.Length),
        EhlersStochasticCyberCycle oscillator => new GeneratedStochasticCyberState(oscillator.Length, Math.Max(.01, Math.Min(.99, oscillator.Alpha))),
        EhlersRelativeVigorIndex oscillator => new GeneratedEhlersVigorState(oscillator.Length, oscillator.SignalLength, count),
        EhlersModifiedRelativeStrengthIndex oscillator => new GeneratedModifiedRsiState(oscillator.Length1, oscillator.Length2, oscillator.Length3),
        EhlersModifiedStochasticIndicator oscillator => new GeneratedModifiedStochasticState(oscillator.Length1, oscillator.Length2, oscillator.Length3),
        EhlersRocketRelativeStrengthIndex oscillator => new GeneratedRocketRsiState(oscillator.Length1, oscillator.Length2, oscillator.Mult),
        EhlersMovingAverageDifferenceIndicator oscillator => new GeneratedAverageDifferenceState(oscillator.FastLength, oscillator.SlowLength, count),
        Frama average => new GeneratedFramaState(average.Length),
        EhlersFrama average => new GeneratedFramaState(average.Length),
        FisherTransform oscillator => new GeneratedFisherState(oscillator.Length),
        EhlersFisherTransform oscillator => new GeneratedFisherState(oscillator.Length),
        EhlersInverseFisherTransform oscillator => new GeneratedInverseRsiFisherState(oscillator.Length, 9, count),
        EhlersRelativeStrengthIndexInverseFisherTransform oscillator => new GeneratedInverseRsiFisherState(oscillator.Length, oscillator.SignalLength, count),
        EhlersCorrelationTrendIndicator correlation => new GeneratedCorrelationState(correlation.Length, CorrelationKind.Trend),
        EhlersCorrelationCycleIndicator correlation => new GeneratedCorrelationState(correlation.Length, CorrelationKind.Cycle),
        EhlersCorrelationAngleIndicator correlation => new GeneratedCorrelationState(correlation.Length, CorrelationKind.Angle),
        EhlersMarketStateIndicator correlation => new GeneratedCorrelationState(correlation.Length, CorrelationKind.MarketState),
        EhlersHurstCoefficient hurst => new GeneratedHurstCoefficientState(hurst.Length1, hurst.Length2),
        EhlersNoiseEliminationTechnology noise => new GeneratedNoiseEliminationState(noise.Length),
        EhlersSpearmanRankIndicator rank => new GeneratedChronologicalSpearmanState(rank.Length),
        EhlersAdaptiveRelativeStrengthIndexV1 rsi => new GeneratedAdaptiveRsiV1State(rsi.CycPart, false),
        EhlersClassicHilbertTransformer transformer => new GeneratedClassicHilbertState(transformer.Length1, transformer.Length2),
        EhlersHilbertTransformer transformer => new GeneratedHilbertTransformerState(transformer.Length1, transformer.Length2, 1, false),
        EhlersHilbertTransformerIndicator transformer => new GeneratedHilbertTransformerState(transformer.Length1, transformer.Length2, transformer.Length3, true),
        EhlersHilbertTransformIndicator transform => new GeneratedHilbertPhaseState(transform.Length, transform.IMult, transform.QMult),
        EhlersInstantaneousPhaseIndicator phase => new GeneratedInstantaneousPhaseState(phase.Length1, phase.Length2),
        EhlersSineWaveIndicatorV1 => new GeneratedMamaDerivedState(0),
        EhlersHilbertOscillator => new GeneratedMamaDerivedState(1),
        EhlersInstantaneousTrendlineV1 => new GeneratedMamaDerivedState(2),
        EhlersSineWaveIndicatorV2 sine => new GeneratedCyberSineState(sine.Length, sine.Alpha),
        FollowingAdaptiveMovingAverage => new GeneratedFollowingMamaState(),
        EhlersMesaAdaptiveMovingAverage mama => new GeneratedMamaOutputsState(mama.FastLimit, mama.SlowLimit),
        EhlersDualDifferentiatorDominantCycle cycle => new GeneratedHilbertCycleState(cycle.Length1, cycle.Length2, cycle.Length3, 1, 0),
        EhlersHomodyneDominantCycle cycle => new GeneratedHilbertCycleState(cycle.Length1, cycle.Length2, cycle.Length3, 1, 1),
        EhlersPhaseAccumulationDominantCycle cycle => new GeneratedHilbertCycleState(cycle.Length1, cycle.Length2, cycle.Length3, cycle.Length4, 2),
        EhlersSignalToNoiseRatioV1 noise => new GeneratedHilbertNoiseState(noise.Length),
        EhlersSignalToNoiseRatioV2 noise => new GeneratedMamaNoiseState(noise.Length, 1),
        EhlersAlternateSignalToNoiseRatio noise => new GeneratedMamaNoiseState(noise.Length, 0),
        EhlersEnhancedSignalToNoiseRatio noise => new GeneratedEnhancedMamaNoiseState(noise.Length),
        EhlersCycleAmplitude amplitude => new GeneratedCycleAmplitudeState(amplitude.Length, amplitude.Delta, count),
        EhlersTrendExtraction trend => new GeneratedTrendExtractionState(trend.Length, trend.Delta),
        EhlersEmpiricalModeDecomposition decomposition => new GeneratedEmpiricalDecompositionState(decomposition.Length1, decomposition.Length2, decomposition.Delta, decomposition.Fraction),
        EhlersPhaseCalculation phase => new GeneratedFourierPhaseState(phase.Length),
        EhlersConvolutionIndicator convolution => new GeneratedEhlersConvolutionState(convolution.Length1, convolution.Length2, convolution.Length3),
        EhlersFourierSeriesAnalysis fourier => new GeneratedFourierSeriesState(fourier.Length, fourier.Bw),
        EhlersDiscreteFourierTransform fourier => new GeneratedDiscreteFourierState(fourier.MinLength, fourier.MaxLength, fourier.Length),
        EhlersDiscreteFourierTransformSpectralEstimate spectrum => new GeneratedDftSpectrumState(spectrum.Length1, spectrum.Length2),
        EhlersAutoCorrelationReversals reversal => new GeneratedAutocorrelationReversalState(reversal.Length1, reversal.Length2, reversal.Length3),
        EhlersCombFilterSpectralEstimate comb => new GeneratedCombSpectrumState(comb.Length1, comb.Length2, comb.Bw),
        EhlersDominantCycleTunedBypassFilter tuned => new GeneratedTunedBypassState(tuned.MinLength, tuned.MaxLength, Math.Max(3, tuned.Length1), tuned.Length2),
        EhlersMesaPredictIndicatorV1 mesa => new GeneratedMesaPredictV1State(mesa.Length1, mesa.Length2, mesa.LowerLength, mesa.UpperLength),
        EhlersMesaPredictIndicatorV2 mesa => new GeneratedMesaPredictV2State(mesa.Length1, mesa.Length2, mesa.Length3, mesa.Length4),
        EhlersAnticipateIndicator anticipate => new GeneratedAnticipateState(anticipate.Length, anticipate.Bw),
        EhlersFMDemodulatorIndicator demodulator => new GeneratedFmDemodulatorState(demodulator.FastLength, demodulator.SlowLength),
        SuperSmoother smoother => new GeneratedSuperSmootherAliasState(smoother.Length),
        EhlersVariableIndexDynamicAverage => new GeneratedEhlersVidyaState(),
        EhlersSquelchIndicator squelch => new GeneratedSquelchState(squelch.Length1, squelch.Length2, squelch.Length3),
        EhlersSwissArmyKnife knife => new GeneratedSwissArmyState(knife.Length, knife.Delta),
        EhlersSpectrumDerivedFilterBank bank => new GeneratedSpectrumBankState(bank.MinLength, bank.MaxLength, bank.Length1, bank.Length2),
        EhlersRestoringPullIndicator pull => new GeneratedRestoringPullState(pull.MinLength, pull.MaxLength, pull.Length1, pull.Length2),
        ButterworthFilter filter => new GeneratedTwoPoleState(filter.Length, 0),
        InverseFisherTransformCore => new GeneratedInverseRsiFisherState(5, 9, count),
        EhlersReflex filter => new GeneratedTrendflexState(filter.Length, true),
        EhlersTrendflex filter => new GeneratedTrendflexState(filter.Length, false),
        ElderForceIndex force => new GeneratedForceState(force.Length),
        MfiCore flow => new GeneratedMoneyFlowState(flow.Length, count),
        WilliamsAD => new GeneratedWilliamsAccumulationState(),
        KlingerSignal signal => new GeneratedKlingerSignalState(signal.FastLength, signal.SlowLength, signal.SignalLength),
        ChandeMomentumOscillatorSignal signal => new GeneratedChandeSignalState(signal.Length, count, signal.SignalLength),
        MassIndexCore mass => new GeneratedMassState(21, mass.Length, count),
        MacdLine oscillator => new GeneratedEmaGapSignalState(oscillator.FastLength, oscillator.SlowLength, 9),
        MacdSignal oscillator => new GeneratedMacdAliasState(oscillator.FastLength, oscillator.SlowLength, oscillator.SignalLength, 1),
        MacdHistogram oscillator => new GeneratedMacdAliasState(oscillator.FastLength, oscillator.SlowLength, oscillator.SignalLength, 2),
        AverageDirectionalIndex direction => new GeneratedDirectionalState(direction.Length, count),
        MomentumOscillator momentum => new GeneratedMomentumState(Math.Max(1, momentum.Length), count),
        VolumePriceTrend volume => new GeneratedVolumeTrendState(volume.Length),
        StandardDevation deviation => new GeneratedStandardDeviationAliasState(Math.Max(1, deviation.Length), count),
        Vhf filter => new GeneratedVerticalHorizontalState(filter.Length, count),
        Ama average => new GeneratedAdaptiveRangeState(average.Length),
        AdaptiveRsi => new GeneratedAdaptivePriceRsiState(14, count),
        AdaptiveRelativeStrengthIndex average => new GeneratedAdaptivePriceRsiState(Math.Max(1, average.Length), count),
        AsymmetricalRsi oscillator => new GeneratedAsymmetricRsiState(oscillator.UpLength, count),
        AsymmetricalRelativeStrengthIndex oscillator => new GeneratedAsymmetricRsiState(oscillator.Length, count),
        AdaptiveStochastic oscillator => new GeneratedAdaptiveStochasticState(oscillator.MinLength, oscillator.MaxLength),
        ApirineSlowRelativeStrengthIndex oscillator => new GeneratedApirineRsiState(oscillator.Length, oscillator.SmoothLength, count),
        PoweredKaufmanAdaptiveMovingAverage average => new GeneratedPoweredKaufmanState(average.Length),
        AdaptiveTrailingStop stop => new GeneratedAdaptiveStopState(stop.Length, stop.Multiplier),
        AverageAbsoluteErrorNormalization normalization => new GeneratedAbsoluteErrorState(normalization.Length),
        BelkhayateTiming => new GeneratedBelkhayateState(),
        BayesianOscillator oscillator => new GeneratedBayesianState(oscillator.Length),
        AtrTrailingStops stop => new GeneratedAtrStopsState(stop.Length, stop.Multiplier, count),
        AverageTrueRangeTrailingStops stop => new GeneratedAtrStopsState(stop.Length, stop.Multiplier, count),
        ChandelierExit stop => new GeneratedChandelierAliasState(stop.Length, stop.Mult, false),
        ChandelierExitLong stop => new GeneratedChandelierAliasState(stop.Length, 3, false),
        ChandelierExitShort stop => new GeneratedChandelierAliasState(stop.Length, 3, true),
        MotionToAttractionTrailingStop stop => new GeneratedMotionStopState(stop.Length),
        ElderSafeZoneStops stop => new GeneratedElderSafeStopState(stop.Length, stop.Mult),
        KaseDevStopV1 stop => new GeneratedKaseStopV1State(stop),
        KaseDevStopV2 stop => new GeneratedKaseStopV2State(stop),
        LinearTrailingStop stop => new GeneratedLinearStopState(Math.Max(1, stop.Length), stop.Mult),
        PercentageTrailingStops stop => new GeneratedPercentageStopsState(stop.Length, stop.Pct, count),
        BilateralStochasticOscillator oscillator => new GeneratedBilateralState(oscillator.Length),
        BreakoutRsi rsi => new GeneratedBreakoutRsiState(rsi.Length),
        CCTStochRelativeStrengthIndex => new GeneratedCctRsiState(5, 8, 13, 14, 21, 3, 8, MovingAvgType.ExponentialMovingAverage, count),
        CCTStochRSI rsi => new GeneratedCctRsiState(rsi.Length1, rsi.Length2, rsi.Length3, rsi.Length4, rsi.Length5, rsi.SmoothLength1, rsi.SmoothLength2, rsi.MaType, count),
        ChandeCompositeMomentumIndex => new GeneratedChandeCompositeState(),
        ChandeForecastOscillator forecast => new GeneratedChandeForecastState(forecast.Length, count),
        ChandeKrollRSquaredIndex kroll => new GeneratedChandeKrollState(kroll.Length),
        ChandeMomentumOscillatorAbsolute oscillator => new GeneratedChandeAbsoluteState(Math.Max(1, oscillator.Length), count),
        ChandeMomentumOscillatorAbsoluteAverage => new GeneratedChandeAverageState(count, true),
        ChandeMomentumOscillatorAverage => new GeneratedChandeAverageState(count, false),
        ChandeMomentumOscillatorAverageDisparityIndex => new GeneratedChandeDisparityState(),
        ChandeMomentumOscillatorFilter oscillator => new GeneratedChandeFilterState(Math.Max(1, oscillator.Length), count),
        ChartmillValueIndicator chart => new GeneratedChartmillState(chart.Length, count),
        CenterOfLinearity center => new GeneratedCenterLinearityState(center.Length),
        ChopZone chop => new GeneratedChopZoneState(chop.Length, count),
        CommoditySelectionIndex commodity => new GeneratedCommoditySelectionState(commodity.Length),
        ConstanceBrownCompositeIndex brown => new GeneratedBrownCompositeState(brown),
        ConfluenceIndicator confluence => new GeneratedConfluenceState(confluence.Length),
        CoppockCurve curve => new GeneratedCoppockState(curve.Length, count),
        DerivativeOscillator derivative => new GeneratedDerivativeState(derivative.Length),
        DidiIndex didi => new GeneratedDidiState(didi.ShortLength, didi.MediumLength, didi.LongLength, count),
        Pmo pmo => new GeneratedPriceMomentumState(pmo.Length, 20, pmo.SignalLength, count, false),
        PriceMomentumOscillator pmo => new GeneratedPriceMomentumState(pmo.Length1, pmo.Length2, pmo.SignalLength, count, false),
        DecisionPointPriceMomentumOscillator pmo => new GeneratedPriceMomentumState(checked(Math.Max(1, pmo.Length) * 2 + 7), checked(Math.Max(1, pmo.Length) + 6), 10, count, true),
        DoubleSmoothedStochastic stochastic => new GeneratedDoubleSmoothedStochasticState(stochastic.Length),
        DTOscillator oscillator => new GeneratedDtOscillatorState(oscillator.Length),
        DoubleStochasticOscillator stochastic => new GeneratedDoubleStochasticState(Math.Max(1, stochastic.Length)),
        DMIStochastic stochastic => new GeneratedDmiStochasticState(Math.Max(1, stochastic.Length), count),
        CalmarRatio ratio => new GeneratedCalmarState(ratio.Length),
        DemandIndex => new GeneratedDemandIndexState(),
        DemandOscillator => new GeneratedDemandOscillatorState(),
        DampingIndex damping => new GeneratedDampingState(damping.Length, count),
        DetrendedSyntheticPrice synthetic => new GeneratedSyntheticPriceState(synthetic.Length),
        DirectionalTrendIndex trend => new GeneratedDirectionalTrendState(trend.Length, count),
        CoralTrendIndicator coral => new GeneratedCoralTrendState(coral.Length),
        CorrectedMovingAverage corrected => new GeneratedCorrectedAverageState(corrected.Length),
        CubedWeightedMovingAverage cube => new GeneratedCubicWeightedState(cube.Length, count),
        Dema2Lines lines => new GeneratedDemaTwoLinesState(lines.FastLength, lines.SlowLength),
        DemarkPressureRatioV1 pressure => new GeneratedDemarkPressureState(pressure.Length, count, false),
        DemarkPressureRatioV2 pressure => new GeneratedDemarkPressureState(pressure.Length, count, true),
        DemarkRangeExpansionIndex range => new GeneratedDemarkRangeState(range.Length),
        DiNapoliMovingAverageConvergenceDivergence macd => new GeneratedDiNapoliOscillatorState(macd.Lc, macd.Sc, macd.Sp, false),
        DiNapoliPercentagePriceOscillator => new GeneratedDiNapoliOscillatorState(17.5185, 8.3896, 9.0503, true),
        DiNapoliPreferredStochasticOscillator stochastic => new GeneratedDiNapoliStochasticState(stochastic.Length),
        DistanceWeightedMovingAverage distance => new GeneratedDistanceMassState(distance.Length, count, true),
        InverseDistanceWeightedMovingAverage distance => new GeneratedDistanceMassState(distance.Length, count, false),
        DisparityIndex disparity => new GeneratedDisparityState(Math.Max(1, disparity.Length), count),
        DoubleSmoothedMomenta momenta => new GeneratedDoubleMomentaState(momenta.MomentumLength, momenta.FirstSmooth, momenta.SecondSmooth, count),
        DoubleSmoothedRelativeStrengthIndex => new GeneratedDoubleRangeStrengthState(count),
        DynamicMomentumIndex => new GeneratedDynamicMomentumIndexState(),
        DynamicMomentumOscillator oscillator => new GeneratedDynamicMomentumOscillatorState(Math.Max(1, oscillator.Length), count),
        DynamicSupportAndResistance support => new GeneratedDynamicSupportState(support.Length, count),
        DynamicallyAdjustableFilter filter => new GeneratedDynamicFilterState(filter.Length),
        DynamicallyAdjustableMovingAverage average => new GeneratedDynamicAverageState(average.FastLength, average.SlowLength),
        EdgePreservingFilter filter => new GeneratedEdgePreservingState(filter.Length),
        EfficientPrice price => new GeneratedEfficiencyOutputState(price.Length, false, .0001, .005),
        EfficientAutoLine line => new GeneratedEfficiencyOutputState(line.Length, true, line.FastAlpha, line.SlowAlpha),
        EarningSupportResistanceLevels => new GeneratedEarningLevelsState(),
        ElderMarketThermometer thermometer => new GeneratedElderThermometerState(thermometer.Length),
        ElderImpulseSystem impulse => new GeneratedElderImpulseState(impulse.Length),
        ElderRayIndex ray => new GeneratedElderRayState(ray.Length),
        EndPointMovingAverage average => new GeneratedEndPointState(Math.Max(1, average.Length), count),
        ElliottWaveOscillator wave => new GeneratedElliottWaveState(Math.Max(1, wave.FastLength), Math.Max(1, wave.SlowLength), count),
        EmaWaveIndicator wave => new GeneratedEmaWaveState(wave.Length1, wave.Length2, wave.Length3, wave.SmoothLength, count),
        EnhancedIndex index => new GeneratedEnhancedIndexState(index.Length, index.SignalLength),
        FallingRisingFilter filter => new GeneratedFallingRisingState(filter.Length),
        FXSniperIndicator sniper => new GeneratedFxSniperState(sniper.CciLength, sniper.T3Length, sniper.B),
        FastSlowKurtosisOscillator kurtosis => new GeneratedFastSlowKurtosisState(kurtosis.Length, .03),
        FastandSlowKurtosisOscillator kurtosis => new GeneratedFastSlowKurtosisState(kurtosis.Length, kurtosis.Ratio),
        FastSlowRsiOscillator => new GeneratedFastSlowCompositeState(true),
        FastSlowStochasticOscillator => new GeneratedFastSlowCompositeState(false),
        FastSlowDegreeOscillator degree => new GeneratedFastSlowDegreeState(degree.Length),
        FearAndGreedIndicator fear => new GeneratedFearGreedState(fear.FastLength, fear.SlowLength, fear.SmoothLength),
        FireflyOscillator firefly => new GeneratedFireflyState(firefly.Length),
        FoldedRelativeStrengthIndex folded => new GeneratedFoldedRsiState(folded.Length, count),
        FisherTransformStochasticOscillator fisher => new GeneratedFisherStochasticState(fisher.Length),
        FibonacciRetrace retrace => new GeneratedFibonacciRetraceState(retrace.Length2, retrace.Factor),
        ForecastOscillator forecast => new GeneratedForecastOscillatorState(forecast.Length, count),
        FreedomOfMovement freedom => new GeneratedFreedomState(freedom.Length),
        FunctionToCandles candles => new GeneratedFunctionCandlesState(candles.Length),
        GainLossMovingAverage gain => new GeneratedGainLossAverageState(gain.Length, gain.SignalLength, count),
        GannHiLoActivator gann => new GeneratedGannHiLoState(gann.Length, count),
        GannTrendOscillator gann => new GeneratedGannTrendState(Math.Max(1, gann.Length)),
        GopalakrishnanRangeIndex range => new GeneratedGopalakrishnanState(range.Length, count),
        GrandTrendForecasting grand => new GeneratedGrandTrendState(grand.Length, grand.ForecastLength, grand.Mult),
        GroverLlorensActivator activator => new GeneratedGroverState(activator.Length, activator.Mult, false),
        GroverLlorensCycleOscillator cycle => new GeneratedGroverState(cycle.Length, 10, true),
        HampelFilter hampel => new GeneratedHampelState(hampel.Length, hampel.ScalingFactor),
        HarmonicMeanMovingAverage harmonic => new GeneratedHarmonicState(harmonic.Length, count),
        HalfTrend trend => new GeneratedHalfTrendState(trend.Length),
        HawkeyeVolumeIndicator hawkeye => new GeneratedHawkeyeState(hawkeye.Divisor),
        GuppyDistanceIndicator guppy => new GeneratedGuppyDistanceState([guppy.Length1, guppy.Length2, guppy.Length3, guppy.Length4, guppy.Length5, guppy.Length6, guppy.Length7, guppy.Length8, guppy.Length9, guppy.Length10, guppy.Length11, guppy.Length12]),
        GuppyMultipleMovingAverage guppy => new GeneratedGuppyMultipleState([guppy.Length1, guppy.Length2, guppy.Length3, guppy.Length5, guppy.Length7, guppy.Length9, guppy.Length10, guppy.Length11, guppy.Length12, guppy.Length13, guppy.Length14, guppy.Length15, guppy.Length16, guppy.Length18, guppy.Length19, guppy.Length21, guppy.Length22, guppy.Length23, guppy.Length25, guppy.Length26, 52, 55, 58, 61, 64, 67, 70]),
        GuppyCountBackLine countBack => new GeneratedCountBackState(countBack.Length, count),
        HirashimaSugitaRS bands => new GeneratedHirashimaState(bands.Length),
        HerrickPayoffIndex payoff => new GeneratedHerrickState(payoff.PointValue),
        HighLowIndex index => new GeneratedHighLowIndexState(Math.Max(1, index.Length), count),
        InternalBarStrengthIndicator strength => new GeneratedInternalBarState(strength.Length),
        Inertia inertia => new GeneratedInertiaState(inertia.SmoothLength, inertia.RviLength),
        InertiaIndicator inertia => new GeneratedInertiaState(inertia.Length, 14),
        HybridConvolutionFilter filter => new GeneratedHybridState(Math.Max(1, filter.Length), count),
        InsyncIndex insync => new GeneratedInsyncState(insync),
        JmaRsxClone rsx => new GeneratedRsxState(rsx.Length),
        JrcFractalDimension jrc => new GeneratedJrcState(jrc.Length1, jrc.Length2, jrc.SmoothLength),
        JapaneseCorrelationCoefficient ratio => new GeneratedJapaneseState(Math.Max(1, ratio.Length), count),
        MayerMultiple ratio => new GeneratedMayerState(Math.Max(1, ratio.Length), count),
        OceanIndicator ocean => new GeneratedOceanState(Math.Max(1, ocean.Length), count),
        KaseIndicator kase => new GeneratedKaseRatioState(kase.Length),
        KaseConvergenceDivergence kase => new GeneratedKaseConvergenceState(kase.Length1, kase.Length2, kase.Length3),
        KasePeakOscillatorV1 peak => new GeneratedKasePeakV1State(peak.Length, peak.SmoothLength),
        KasePeakOscillatorV2 peak => new GeneratedKasePeakV2State(peak.Length),
        KaufmanAdaptiveBands bands => new GeneratedKaufmanBandsState(bands.Length, bands.StdDevFactor),
        KaufmanBinaryWave wave => new GeneratedKaufmanBinaryState(wave.Length, wave.FastSc, wave.SlowSc, wave.FilterPct),
        KarobeinOscillator oscillator => new GeneratedKarobeinState(oscillator.Length),
        KwanIndicator kwan => new GeneratedKwanState(kwan.Length, kwan.SmoothLength),
        LBRPaintBars paint => new GeneratedLbrPaintState(paint.Length, paint.LbLength, paint.AtrMult),
        LindaRaschke310Oscillator linda => new GeneratedLindaState(linda.FastLength, linda.SlowLength, linda.SmoothLength, count),
        LinearQuadraticConvergenceDivergenceOscillator oscillator => new GeneratedLinearQuadraticState(oscillator.Length),
        LogisticCorrelation logistic => new GeneratedLogisticState(logistic.Length, logistic.K, count),
        MotionSmoothnessIndex motion => new GeneratedMotionSmoothnessState(motion.Length),
        MacZIndicator macz => new GeneratedMacZState(macz.FastLength, macz.SlowLength, macz.SignalLength, macz.Length, macz.Mult),
        MacZVwapIndicator macz => new GeneratedMacZVwapState(macz.FastLength, macz.SlowLength, macz.SignalLength, macz.Length1, macz.Length2, macz.Gamma),
        KurtosisIndicator => new GeneratedKurtosisState(count),
        MarketDirectionIndicator market => new GeneratedMarketDirectionState(market.Length),
        MassThrust mass => new GeneratedMassThrustState(false, mass.Length),
        MassThrustIndicator mass => new GeneratedMassThrustState(false, mass.Length),
        MassThrustOscillator mass => new GeneratedMassThrustState(true, mass.Length),
        MobilityOscillator mobility => new GeneratedMobilityState(mobility.Length),
        McClellanOscillator => new GeneratedMcClellanState(),
        MidpointOscillator midpoint => new GeneratedMidpointOscillatorState(midpoint.Length, count),
        ModifiedGannHiloActivator gann => new GeneratedModifiedGannState(gann.Length),
        MorphedSineWave sine => new GeneratedMorphedSineState(sine.Length),
        MoveTracker => new GeneratedMoveTrackerState(),
        MultiLevelIndicator level => new GeneratedMultiLevelState(level.Length, level.Factor),
        MomentaRelativeStrengthIndex momenta => new GeneratedMomentaRsiState(momenta.Length1, momenta.Length2, count),
        MultiVoteOnBalanceVolume vote => new GeneratedMultiVoteState(vote.Length),
        NormalizedRelativeVigorIndex vigor => new GeneratedNormalizedVigorState(vigor.Length, count),
        NthOrderDifferencingOscillator difference => new GeneratedNthDifferenceState(difference.Length, count),
        NegativeVolumeDisparityIndicator disparity => new GeneratedNegativeDisparityState(disparity.Length, disparity.SignalLength, disparity.Top, disparity.Bottom),
        NarrowBandpassFilter narrow => new GeneratedNarrowBandpassState(narrow.Length, count),
        NaturalMarketSlope slope => new GeneratedNaturalSlopeState(slope.Length, count),
        NaturalDirectionalIndex natural => new GeneratedNaturalDirectionalState(natural.Length, natural.SmoothLength, count),
        NaturalStochasticIndicator natural => new GeneratedNaturalStochasticState(natural.Length, natural.SmoothLength, count),
        NaturalDirectionalCombo natural => new GeneratedNaturalComboState(natural.Length, natural.SmoothLength, count),
        NaturalMarketMirror natural => new GeneratedNaturalMarketState(natural.Length, count, true),
        NaturalMarketRiver natural => new GeneratedNaturalMarketState(natural.Length, count, false),
        NaturalMarketCombo natural => new GeneratedNaturalMarketComboState(natural.Length, natural.SmoothLength, count),
        MirroredMovingAverageConvergenceDivergence mirrored => new GeneratedMirroredPairState(mirrored.Length, mirrored.SignalLength, false),
        MirroredPercentagePriceOscillator mirrored => new GeneratedMirroredPairState(mirrored.Length, mirrored.SignalLength, true),
        MovingAverageAdaptiveQ adaptive => new GeneratedAdaptiveQState(adaptive.Length),
        MovingAverageAdaptiveFilter adaptive => new GeneratedAdaptiveFilterState(adaptive.Length, adaptive.Filter, adaptive.FastAlpha, adaptive.SlowAlpha),
        MovingAverageV3 average => new GeneratedMovingAverageV3State(average.Length),
        ModularFilter modular => new GeneratedModularState(modular.Length, modular.Beta),
        MultiDepthZeroLagExponentialMovingAverage depth => new GeneratedMultiDepthState(depth.Length),
        NormalizedMacd normalized => new GeneratedNormalizedMacdState(normalized.FastLength, normalized.SlowLength),
        NickRypockTrailingReverse nick => new GeneratedNickReverseState(nick.Length),
        MovingAverageConvergenceDivergenceLeader leader => new GeneratedMacdLeaderState(leader.FastLength, leader.SlowLength, leader.SignalLength),
        PercentagePriceOscillatorLeader leader => new GeneratedPpoLeaderState(leader.Length),
        PercentChangeOscillator change => new GeneratedPercentChangeState(change.Length, count),
        PerformanceIndex performance => new GeneratedPerformanceIndexState(performance.Length, count),
        PremierStochasticOscillator premier => new GeneratedPremierState(premier.Length, premier.SmoothLength),
        PrettyGoodOscillator pretty => new GeneratedPrettyGoodState(pretty.Length, count),
        PriceZoneOscillator zone => new GeneratedPriceZoneState(zone.Length),
        PriceVolumeRank rank => new GeneratedPriceVolumeRankState(rank.FastLength, rank.SlowLength, count),
        PsychologicalLine psychological => new GeneratedPsychologicalState(psychological.Length, count),
        PringSpecialK pring => new GeneratedPringSpecialState(pring, count),
        PercentageTrend trend => new GeneratedPercentageTrendState(trend.Length, trend.Pct, count),
        PentupleExponentialMovingAverage pentuple => new GeneratedBinomialCascadeState(pentuple.Length, true),
        QuadrupleExponentialMovingAverage quadruple => new GeneratedBinomialCascadeState(quadruple.Length, false),
        PpoMa gap => new GeneratedPpoAverageState(gap.FastLength, gap.SlowLength, count),
        QmaSmaDifference gap => new GeneratedQuadraticMeanState(gap.Length, count, true),
        QuadraticMovingAverage mean => new GeneratedQuadraticMeanState(mean.Length, count, false),
        ParabolicWma parabolic => new GeneratedSquareWeightState(parabolic.Length, count),
        QuadraticWma quadratic => new GeneratedSquareWeightState(quadratic.Length, count),
        ParametricCorrectiveLinearMovingAverage corrective => new GeneratedParametricCorrectiveState(corrective.Length),
        ParametricKalmanFilter kalman => new GeneratedParametricKalmanState(kalman.Length),
        PeakValleyEstimation peak => new GeneratedPeakValleyState(peak.Length, peak.SmoothLength),
        ReallySimpleIndicator simple => new GeneratedReallySimpleState(simple.Length, simple.SmoothLength),
        RexOscillator rex => new GeneratedRexState(rex.Length, count),
        RelativeDifferenceOfSquaresOscillator squares => new GeneratedRelativeSquaresState(squares.Length, count),
        RecursiveDifferenciator recursive => new GeneratedRecursiveDifferenciatorState(recursive.Length, recursive.Alpha),
        RecursiveRelativeStrengthIndex strength => new GeneratedRecursiveRsiState(strength.Length),
        RecursiveStochastic stochastic => new GeneratedRecursiveStochasticState(stochastic.Length, stochastic.Alpha),
        RelativeVigorIndex vigor => new GeneratedRelativeVigorState(vigor.Length, count),
        Repulse repulse => new GeneratedRepulseState(repulse.Length),
        TimePriceIndicator sharp => new GeneratedTimePriceState(sharp.Length, count),
        TFSMboIndicator tfs => new GeneratedTfsPairState(tfs.FastLength, tfs.SlowLength, tfs.SignalLength, count, false),
        TFSMboPercentagePriceOscillator => new GeneratedTfsPairState(25, 200, 18, count, true),
        TrendAnalysisIndex trend => new GeneratedTrendAnalysisIndexState(trend.Length1, trend.Length2),
        TrendAnalysisIndicator trend => new GeneratedTrendAnalysisIndicatorState(trend.Length1, trend.Length2),
        TrendDetectionIndex trend => new GeneratedTrendDetectionState(trend.Length1, trend.Length2, count),
        Trender trend => new GeneratedTrenderState(trend.Length, trend.AtrMult),
        TrendExhaustionIndicator trend => new GeneratedTrendExhaustionState(trend.Length, count),
        Trimean rank => new GeneratedTrimeanState(rank.Length, count),
        TurboScaler scaler => new GeneratedTurboScalerState(scaler.Length, 0.5),
        RangeIdentifier => new GeneratedRangeIdentifierState(),
        RatioOchlAverager => new GeneratedRatioOchlState(),
        RangeActionVerificationIndex ravi => new GeneratedRaviState(ravi.FastLength, ravi.SlowLength, count),
        RelativeSpreadStrength spread => new GeneratedRelativeSpreadState(spread.FastLength, spread.SlowLength, spread.Length, spread.SmoothLength),
        RetrospectiveCandlestickChart candle => new GeneratedRetrospectiveCandleState(candle.Length),
        ReverseEngineeringRsi inverse => new GeneratedReverseRsiState(inverse.Length, inverse.RsiLevel),
        RetentionAccelerationFilter retention => new GeneratedRetentionAccelerationState(retention.Length),
        RobustWeightingOscillator robust => new GeneratedRobustWeightingState(robust.Length),
        ReverseMovingAverageConvergenceDivergence inverse => new GeneratedReverseMacdState(inverse.FastLength, inverse.SlowLength),
        SimpleCycle simple => new GeneratedSimpleCycleState(simple.Length),
        SimplePriceZone zone => new GeneratedSimplePriceZoneState(zone.Length),
        SigmaSpikes sigma => new GeneratedSigmaSpikesState(sigma.Length),
        SimpleLines lines => new GeneratedSimpleLinesState(lines.Length, lines.Multiplier),
        SentimentZoneOscillator sentiment => new GeneratedSentimentZoneState(sentiment.Length),
        RandomWalkIndex walk => new GeneratedRandomWalkState(walk.Length),
        RSINGIndicator rsing => new GeneratedRsingState(rsing.Length),
        SellGravitationIndex sell => new GeneratedSellGravitationState(sell.Length),
        RainbowOscillator rainbow => new GeneratedRainbowState(rainbow.Length),
        SchaffTrendCycle schaff => new GeneratedSchaffFirstState(schaff.FastLength, schaff.SlowLength, schaff.CycleLength),
        SchaffTrendCycleShk shk => new GeneratedSchaffShkState(shk.FastLength, shk.SlowLength, shk.CycleLength, shk.D1Length, shk.D2Length),
        StationaryExtrapolatedLevels levels => new GeneratedStationaryLevelsState(levels.Length),
        StationaryExtrapolatedLevelsOscillator oscillator => new GeneratedStationaryOscillatorState(oscillator.Length),
        SettingLessTrendStepFiltering => new GeneratedSettingLessStepState(),
        SqueezeMomentumIndicator squeeze => new GeneratedSqueezeMomentumState(squeeze.Length),
        SharpModifiedMovingAverage sharp => new GeneratedSharpRollingState(sharp.Length, count),
        SequentiallyFilteredMovingAverage sharp => new GeneratedSequentialStreakState(sharp.Length, count),
        ReversalPoints sharp => new GeneratedReversalBoundedState(sharp.Length, count),
        ShapeshiftingMovingAverage sharp => new GeneratedShapeshiftingWeightsState(sharp.Length, count),
        SmoothedRateOfChange sharp => new GeneratedSmoothedReturnState(sharp.RocLength, sharp.SmoothLength, count),
        SimplifiedWeightedMovingAverage sharp => new GeneratedSimplifiedWeightedState(sharp.Length, count),
        SmoothedDeltaRatioOscillator sharp => new GeneratedSmoothedDeltaState(sharp.Length, count),
        SuperTrendFilter sharp => new GeneratedSuperTrendFilterState(sharp.Length, sharp.Factor),
        SurfaceRoughnessEstimator sharp => new GeneratedSurfaceRoughnessState(sharp.Length, count),
        SupportAndResistanceOscillator sharp => new GeneratedSupportResistanceOscillatorState(),
        StiffnessIndicator sharp => new GeneratedStiffnessState(sharp.Length1, sharp.Length2, sharp.SmoothingLength),
        StrengthOfMovement sharp => new GeneratedMovementStrengthState(sharp.Length1, sharp.Length2),
        SuperTrend sharp => new GeneratedSuperTrendState(sharp.Length),
        SwingIndex sharp => new GeneratedSwingIndexState(sharp.LimitMove),
        TickLineMomentumOscillator sharp => new GeneratedTickLineState(sharp.Length, count),
        TFSTetherLineIndicator sharp => new GeneratedTetherLineState(sharp.Length, count),
        TopsAndBottomsFinder sharp => new GeneratedTopsBottomsState(sharp.Length),
        TrendDirectionForceIndex sharp => new GeneratedTrendDirectionForceState(sharp.Length1, sharp.Length2),
        TrendForceHistogram trend => new GeneratedTrendForceHistogramState(trend.Length, count),
        TrendImpulseFilter trend => new GeneratedTrendImpulseState(trend.Length1, trend.Length2, count),
        TrendPersistenceRate trend => new GeneratedTrendPersistenceState(trend.Length, count),
        TrueRangeAdjustedExponentialMovingAverage trend => new GeneratedTrueRangeAdjustedState(trend.Length, trend.Mult),
        TreynorRatio ratio => new GeneratedTargetReturnState(ratio.Length, ratio.Bmk, false, count, ratio.Beta),
        UpsidePotentialRatio ratio => new GeneratedTargetReturnState(ratio.Length, ratio.Bmk, true, count),
        TTMScalperIndicator => new GeneratedTtmScalperState(),
        UberTrendIndicator trend => new GeneratedUberTrendState(trend.Length),
        VaradiOscillator oscillator => new GeneratedVaradiState(oscillator.Length),
        UhlMaCrossoverSystem crossover => new GeneratedUhlState(Math.Max(1, crossover.Length), count),
        UltimateMovingAverage => new GeneratedUltimateAverageState(),
        UltimateMovingAverageBands bands => new GeneratedUltimateBandsState(bands.MinLength, bands.MaxLength, bands.StdDevMult),
        VariableIndexDynamicAverage average => new GeneratedVidyaAliasState(average.Length, count),
        Vidya average => new GeneratedVidyaAliasState(average.Length, count),
        Vma average => new GeneratedVmaState(average.Length),
        VariableMovingAverageBands bands => new GeneratedVariableBandsState(bands.Length, bands.Mult),
        WaddahAttarExplosion explosion => new GeneratedWaddahState(explosion.FastLength, explosion.SlowLength, explosion.Sensitivity),
        WaveTrendOscillator oscillator => new GeneratedWaveTrendState(oscillator.Length),
        VolatilityMovingAverage average => new GeneratedVolatilityAverageState(Math.Max(1, average.Length), count),
        ZweigMarketBreadthIndicator breadth => new GeneratedZweigState(Math.Max(1, breadth.Length), count),
        ZeroLagSmoothedCycle cycle => new GeneratedZeroLagCycleState(cycle.Length),
        VostroIndicator vostro => new GeneratedVostroState(Math.Max(1, vostro.Length1), Math.Max(1, vostro.Length2), vostro.Level, count),
        WilliamsFractalUp fractal => new GeneratedWilliamsFractalState(Math.Max(2, fractal.Length), count, true),
        WilliamsFractalDown fractal => new GeneratedWilliamsFractalState(Math.Max(2, fractal.Length), count, false),
        WilliamsFractalsUp fractal => new GeneratedWilliamsFractalState(Math.Max(2, fractal.Length), count, true),
        WilliamsFractalsDown fractal => new GeneratedWilliamsFractalState(Math.Max(2, fractal.Length), count, false),
        WilliamsFractals fractal => new GeneratedWilliamsFractalState(Math.Max(2, fractal.Length), count, true),
        WoodiePivotPoint => new GeneratedWoodiePivotState(),
        CamarillaPivotPoint => new GeneratedPivotFamiliesState(0),
        FibonacciPivotPoint => new GeneratedPivotFamiliesState(1),
        DemarkPivotPoint => new GeneratedPivotFamiliesState(2),
        FloorPivotPoint => new GeneratedPivotFamiliesState(3),
        StandardPivotPoints => new GeneratedDailyPivotState(true),
        DynamicPivotPoints => new GeneratedDailyPivotState(false),
        EhlersAdaptiveCyberCycle cycle => new GeneratedAdaptiveCyberState(cycle.Length, cycle.Alpha),
        EhlersAdaptiveCenterOfGravityOscillator gravity => new GeneratedAdaptiveGravityState(gravity.Length),
        EhlersSmoothedAdaptiveMomentum momentum => new GeneratedSmoothedAdaptiveMomentumState(momentum.Length1, momentum.Length2, momentum.MaType),
        EhlersAdaptiveRsiFisherTransformV1 => new GeneratedAdaptiveRsiV1State(.5, true),
        EhlersAdaptiveStochasticIndicatorV1 stochastic => new GeneratedAdaptiveRangeV1State(stochastic.CycPart, false, .015),
        EhlersAdaptiveCommodityChannelIndexV1 commodity => new GeneratedAdaptiveRangeV1State(commodity.CycPart, true, commodity.Constant),
        EhlersAutoCorrelationIndicator correlation => new GeneratedRoofAutocorrelationState(correlation.Length1, correlation.Length2),
        EhlersAutoCorrelationPeriodogram spectrum => new GeneratedAutocorrelationSpectrumState(spectrum.Length1, spectrum.Length2, spectrum.Length3),
        EhlersAdaptiveBandPassFilter band => new GeneratedAdaptiveBandPassState(band.Length1, band.Length2, band.Length3, band.Bw),
        EhlersZeroCrossingsDominantCycle cycle => new GeneratedZeroCrossingCycleState(cycle.Length, cycle.Bw),
        EhlersAdaptiveRelativeStrengthIndexV2 rsi => new GeneratedAdaptiveRsiV2State(rsi.Length1, rsi.Length2, rsi.Length3, false),
        EhlersAdaptiveRsiFisherTransformV2 fisher => new GeneratedAdaptiveRsiV2State(fisher.Length1, fisher.Length2, fisher.Length3, true),
        EhlersAdaptiveStochasticIndicatorV2 stochastic => new GeneratedAdaptiveRangeV2State(stochastic.Length1, stochastic.Length2, stochastic.Length3, false, false),
        EhlersAdaptiveStochasticInverseFisherTransform fisher => new GeneratedAdaptiveRangeV2State(fisher.Length1, fisher.Length2, fisher.Length3, false, true),
        EhlersAdaptiveCommodityChannelIndexV2 commodity => new GeneratedAdaptiveRangeV2State(commodity.Length1, commodity.Length2, commodity.Length3, true, false),
        EhlersEvenBetterSineWaveIndicator sine => new GeneratedEvenBetterSineState(sine.Length1, sine.Length2),
        EhlersReverseEmaIndicatorV1 reverse => new GeneratedReverseEmaState(reverse.Alpha),
        EhlersReverseEmaIndicatorV2 reverse => new GeneratedReverseEmaPairState(reverse.TrendAlpha, reverse.CycleAlpha),
        EhlersUniversalOscillator oscillator => new GeneratedUniversalOscillatorState(oscillator.Length),
        EhlersUniversalTradingFilter filter => new GeneratedUniversalTradingState(filter.Length1, filter.Length2, filter.Mult, false),
        EhlersSnakeUniversalTradingFilter filter => new GeneratedUniversalTradingState(filter.Length1, filter.Length2, filter.Bw, true),
        EhlersVossPredictiveFilter filter => new GeneratedVossState(filter.Length, filter.Predict, filter.Bw),
        EhlersTruncatedBandPassFilter filter => new GeneratedTruncatedBandState(filter.Length1, filter.Length2, filter.Bw),
        EhlersAMDetector detector => new GeneratedAmDetectorState(detector.Length1, detector.Length2),
        EhlersKaufmanAdaptiveMovingAverage filter => new GeneratedEhlersKaufmanState(filter.Length),
        EhlersSimpleCycleIndicator cycle => new GeneratedSimpleCycleIndicatorState(cycle.Alpha),
        EhlersFilter filter => new GeneratedEhlersDistanceState(filter.Length),
        EhlersAverageErrorFilter filter => new GeneratedAverageErrorState(filter.Length),
        EhlersDistanceCoefficientFilter filter => new GeneratedDistanceCoefficientState(filter.Length),
        EhlersReflexIndicator filter => new GeneratedTrendflexState(filter.Length, true),
        EhlersTrendflexIndicator filter => new GeneratedTrendflexState(filter.Length, false),
        EhlersDeviationScaledMovingAverage average => new GeneratedDeviationScaledState(average.Length, DeviationScaledWindow.ResolveSlow(average.Length), false),
        EhlersFisherizedDeviationScaledOscillator oscillator => new GeneratedDeviationScaledState(oscillator.Length, 40, true),
        EhlersDeviationScaledSuperSmoother average => new GeneratedDeviationSuperState(average.Length),
        TypicalPriceVolatility volatility => new GeneratedTypicalVolatilityState(Math.Max(1, volatility.Length), count),
        ParkinsonVolatility volatility => new GeneratedOhlcVolatilityState(OhlcVolatilityWindow.Estimator.Parkinson, volatility.Length, count),
        RogersSatchellVolatility volatility => new GeneratedOhlcVolatilityState(OhlcVolatilityWindow.Estimator.RogersSatchell, volatility.Length, count),
        YangZhangVolatility volatility => new GeneratedOhlcVolatilityState(OhlcVolatilityWindow.Estimator.YangZhang, volatility.Length, count),
        StandardDeviationVolatility volatility => new GeneratedResidualVolatilityState(volatility.Length),
        ClosedFormDistanceVolatility volatility => new GeneratedClosedDistanceState(volatility.Length),
        EhlersRecursiveMedianFilter median => new GeneratedRecursiveMedianState(median.Length),
        EhlersRecursiveMedianOscillator median => new GeneratedRecursiveMedianOscillatorState(median.Length1, median.Length2, median.Length3),
        EhlersMedianAverageAdaptiveFilter median => new GeneratedMedianAdaptiveState(median.Length, median.Threshold),
        VolatilityIndexDynamicAverageIndicator average => new GeneratedVolatilityIndexState(average.Length, average.Alpha1, average.Alpha2),
        ChandeVolatilityIndexDynamicAverageIndicator average => new GeneratedVolatilityIndexState(average.Length, average.Alpha1, average.Alpha2),
        VolatilitySwitchIndicator volatility => new GeneratedVolatilitySwitchState(volatility.Length),
        CloseToCloseVolatility volatility => new GeneratedCloseVolatilityState(Math.Max(1, volatility.Length), count),
        DownsideDeviation deviation => new GeneratedDownsideDeviationState(Math.Max(1, deviation.Length), count),
        PrimeNumberOscillator oscillator => new GeneratedPrimeOscillatorState(oscillator.Length),
        PrimeNumberBands channel => new GeneratedPrimeBandState(channel.Length),
        EfficientTrendStepChannel channel => new GeneratedEfficientTrendStepState(channel.Length, channel.FastLength, channel.SlowLength, count),
        LinearChannels channel => new GeneratedLinearChannelState(Math.Max(1, channel.Length), channel.Mult),
        VervoortVolatilityBands channel => new GeneratedVervoortVolatilityState(channel.Length1, channel.Length2, channel.DevMult, channel.LowBandMult),
        VervoortModifiedBollingerBandIndicator channel => new GeneratedVervoortModifiedState(channel.Length1, channel.Length2, channel.SmoothLength, channel.StdDevMult, vervoortModifiedCustomRange),
        UniChannel channel => new GeneratedUniChannelState(Math.Max(1, channel.Length), channel.UbFac, channel.LbFac, channel.Type1, count),
        PseudoPolynomialChannel channel => new GeneratedPseudoPolynomialState(channel.Length, channel.Morph, count),
        ExtendedRecursiveBands channel => new GeneratedExtendedBandState(channel.Length),
        MotionToAttractionChannels channel => new GeneratedMotionAttractionState(channel.Length),
        VolumeAdaptiveBands channel => new GeneratedVolumeAdaptiveBandState(channel.Length, count),
        HurstCycleChannel channel => new GeneratedHurstCycleState(channel.FastLength, channel.SlowLength, channel.FastMult, channel.SlowMult, count),
        HurstBands channel => new GeneratedHurstBandState(channel.Length, channel.InnerMult, channel.OuterMult, channel.ExtremeMult, count),
        SmoothedVolatilityBands channel => new GeneratedSmoothedVolatilityState(channel.Length1, channel.Length2, channel.Deviation, channel.BandAdjust, count),
        HighLowMovingAverage channel => new GeneratedHighLowAverageState(channel.Length, count),
        MovingAverageSupportResistance channel => new GeneratedSupportResistanceState(Math.Max(1, channel.Length), count),
        MovingAverageChannel channel => new GeneratedAverageChannelState(Math.Max(1, channel.Length), count),
        HighLowBands channel => new GeneratedHighLowBandsState(Math.Max(1, channel.Length), channel.PctShift, count),
        MovingAverageEnvelope envelope => new GeneratedAverageEnvelopeState(Math.Max(1, envelope.Length), envelope.Pct, count),
        MovingAverageDisplacedEnvelope envelope => new GeneratedDisplacedEnvelopeState(Math.Max(1, envelope.Length1), Math.Max(1, envelope.Length2), envelope.Pct, count),
        SmartEnvelope envelope => new GeneratedSmartEnvelopeState(envelope.Length, envelope.Factor),
        DEnvelope envelope => new GeneratedDEnvelopeState(envelope.Length, envelope.DevFactor),
        GChannels channel => new GeneratedGChannelState(channel.Length),
        ChandeTrendScore score => new GeneratedTrendScoreState(Math.Max(1, score.Length), count),
        TrendScore score => new GeneratedTrendScoreState(Math.Max(1, score.Length), count),
        KeltnerChannelMiddle middle => new EmaValueState(middle.Length),
        KeltnerChannels channel => new GeneratedKeltnerChannelState(channel.Length1, channel.Length2, channel.MultFactor, count),
        PriceCycleOscillator cycle => new GeneratedPriceCycleState(cycle.Length, count),
        Qstick quick => new GeneratedQuickStickState(quick.Length),
        DailyAveragePriceDelta delta => new GeneratedDailyDeltaState(delta.Length, count),
        DrunkardWalk walk => new GeneratedDrunkardWalkState(walk.Length1),
        DonchianChannel channel => new GeneratedDonchianMiddleState(Math.Max(1, channel.Length), count),
        DonchianChannelUpper channel => new PriceExtremeState(Math.Max(1, channel.Length), count, true),
        DonchianChannelLower channel => new PriceExtremeState(Math.Max(1, channel.Length), count, false),
        DonchianChannelWidth width => new GeneratedDonchianWidthState(width.Length, count),
        ChandeQuickStick quick => new GeneratedQuickStickState(quick.Length),
        DeltaMovingAverage delta => new GeneratedDeltaAverageState(delta.Length1, delta.Length2),
        CumulativeSum => new GeneratedCumulativeState(false),
        CumulativeVolumeIndex => new GeneratedCumulativeState(true),
        ConditionalAccumulator accumulator => new GeneratedConditionalState(accumulator.Length),
        ContractHigh => new PrefixPriceExtremeState(true),
        ContractLow => new PrefixPriceExtremeState(false),
        IntradayMomentumIndex momentum => new GeneratedIntradayMomentumState(momentum.Length, count),
        ChandeIntradayMomentumIndex momentum => new GeneratedIntradayMomentumState(momentum.Length, count),
        RelativeMomentumIndex momentum => new GeneratedRelativeMomentumState(momentum.Length, momentum.Momentum, count),
        Adr range => new GeneratedDayRangeState(range.Length, count),
        AverageDayRange range => new GeneratedDayRangeState(range.Length, count),
        AlligatorJaw line => new GeneratedAlligatorState(line.Length, 8),
        AlligatorTeeth line => new GeneratedAlligatorState(line.Length, 5),
        AlligatorLips line => new GeneratedAlligatorState(line.Length, 3),
        BullPower power => new GeneratedCandlePowerState(true, power.Length, count),
        BearPower power => new GeneratedCandlePowerState(false, power.Length, count),
        BullPowerIndicator power => new GeneratedCandlePowerState(true, power.Length, count),
        BearPowerIndicator power => new GeneratedCandlePowerState(false, power.Length, count),
        AutoLine line => new GeneratedAutoLineState(line.Length, count),
        AutoLineWithDrift line => new GeneratedAutoDriftState(line.Length),
        AutoFilter filter => new GeneratedAutoFilterState(filter.Length),
        AutoDispersionBands bands => new GeneratedAutoDispersionState(bands.Length, bands.SmoothLength, count),
        AwesomeOscillator oscillator => new GeneratedAwesomeState(oscillator.Length, count, false),
        AcceleratorOscillator oscillator => new GeneratedAwesomeState(oscillator.Length, count, true),
        AnchoredMomentum momentum => new GeneratedAnchoredMomentumState(momentum.Length, count),
        AccumulativeSwingIndex swing => new GeneratedAccumulatedSwingState(swing.Length, swing.LimitMove, count),
        AbsoluteStrengthIndex strength => new GeneratedAbsoluteStrengthState(strength.Length),
        AbsoluteStrengthMTFIndicator strength => new GeneratedAbsoluteStrengthMtfState(strength.Length, strength.SmoothLength, count),
        WilsonRelativePriceChannel channel => new GeneratedWilsonState(channel),
        Vpci vpci => new GeneratedVpciState(vpci.Length),
        VolumeWeightedRsi rsi => new GeneratedVolumeWeightedRsiState(rsi.Length),
        VortexBands bands => new GeneratedVortexBandsState(bands.Length),
        WoodieCommodityChannelIndex cci => new GeneratedWoodieCciState(cci.FastLength, cci.SlowLength, count),
        EhlersCommodityChannelIndexInverseFisherTransform cci => new GeneratedInverseCciState(cci.Length, cci.SignalLength, cci.Constant, count),
        ZScore score => new GeneratedStandardizedScoreState(score.Length, count, false, false),
        FastZScore score => new GeneratedStandardizedScoreState(score.Length, count, true, false),
        InverseFisherZScore score => new GeneratedStandardizedScoreState(score.Length, count, false, true),
        InverseFisherFastZScore score => new GeneratedStandardizedScoreState(score.Length, count, true, true),
        ZDistanceFromVwap distance => new GeneratedZDistanceState(distance.Length),
        WamiOscillator wami => new GeneratedWamiState(wami.Length, count),
        PriceMomentum momentum => new GeneratedLaggedDifferenceState(Math.Max(1, momentum.Length), count, false),
        VolumeMomentum momentum => new GeneratedLaggedDifferenceState(Math.Max(1, momentum.Length), count, true),
        VolumeZoneOscillator zone => new GeneratedVolumeZoneState(zone.Length),
        VolumeMomentumOscillator momentum => new GeneratedVolumeMomentumOscillatorState(momentum.ShortLength, momentum.LongLength),
        ValueChartIndicator chart => new GeneratedValueChartState(chart.Length, valueChartCustomRange),
        UltimateMomentumIndicator momentum => new GeneratedUltimateMomentumState(momentum),
        UltimateTraderOscillator => new GeneratedUltimateTraderState(),
        UltimateVolatilityIndicator volatility => new GeneratedUltimateVolatilityState(volatility.Length),
        SmoothedWilliamsR williams => new GeneratedSmoothedWilliamsRState(williams.Length, williams.SmoothLength),
        EnhancedWilliamsR williams => new GeneratedEnhancedWilliamsRState(williams.Length, williams.SignalLength),
        VerticalHorizontalMovingAverage average => new GeneratedVerticalAverageState(average.Length, count),
        VerticalHorizontalFilter filter => new GeneratedVerticalHorizontalState(filter.Length, count),
        TrigonometricOscillator oscillator => new GeneratedTrigonometricState(oscillator.Length),
        TimeSeriesForecast forecast => new GeneratedForecastState(forecast.Length),
        TrendStep step => new GeneratedTrendStepState(step.Length),
        TechnicalRank rank => new GeneratedTechnicalRankState(rank),
        TironeLevels levels => new GeneratedTironeState(levels.Length),
        TimeAndMoneyChannel channel => new GeneratedTimeMoneyState(channel.Length1, channel.Length2),
        TraderPressureIndex pressure => new GeneratedTraderPressureState(pressure.Length1, pressure.Length2, pressure.SmoothLength),
        VortexPositive vortex => new GeneratedVortexState(vortex.Length, true),
        VortexNegative vortex => new GeneratedVortexState(vortex.Length, false),
        VortexPlus vortex => new GeneratedVortexState(vortex.Length, true),
        VortexMinus vortex => new GeneratedVortexState(vortex.Length, false),
        VortexIndicatorPlus vortex => new GeneratedVortexState(vortex.Length, true),
        VortexIndicatorMinus vortex => new GeneratedVortexState(vortex.Length, false),
        VolumeAccumulationPercent flow => new GeneratedMoneyFlowPercentState(flow.Length, false),
        TwiggsMoneyFlow flow => new GeneratedMoneyFlowPercentState(flow.Length, true),
        BuffAverage average => new GeneratedBuffState(average.Length),
        EquityMovingAverage average => new GeneratedEquityState(average.Length),
        Vwma average => new GeneratedVolumeWeightedState(average.Length),
        VolumeWeightedMovingAverage average => new GeneratedVolumeWeightedState(average.Length),
        WindowedVolumeWeightedMovingAverage average => new GeneratedBartlettVolumeState(average.Length),
        VolumeAdjustedMa average => new GeneratedVolumeAdjustedState(average.Length, .67),
        VolumeAdjustedMovingAverage average => new GeneratedVolumeAdjustedState(average.Length, average.Factor),
        AdaptiveEma average => new GeneratedAdaptiveEmaState(average.Length, count),
        AdaptiveExponentialMovingAverage average => new GeneratedAdaptiveEmaState(average.Length, count),
        BryantAdaptiveMovingAverage average => new GeneratedBryantState(average.Length),
        VariableAdaptiveMovingAverage average => new GeneratedVariableAdaptiveState(average.Length),
        AdaptivePriceZoneIndicator zone => new GeneratedAdaptiveZoneState(zone.Length, zone.Pct, count),
        AutonomousRecursiveMa average => new GeneratedAutonomousState(average.Length),
        AdaptiveAutonomousRecursiveMovingAverage average => new GeneratedAdaptiveAutonomousState(average.Length, average.Lambda),
        AdaptiveAutonomousRecursiveTrailingStop stop => new GeneratedAdaptiveAutonomousStopState(stop.Length, stop.Lambda),
        AtrFilteredExponentialMovingAverage average => new GeneratedAtrFilteredState(average.Length, average.AtrLength, average.StdDevLength, average.LbLength, average.Min),
        R2AdaptiveRegression average => new GeneratedR2FitState(average.Length),
        KaufmanAdaptiveLeastSquaresMovingAverage average => new GeneratedKaufmanFitState(average.Length),
        KaufmanAdaptiveCorrelationOscillator oscillator => new GeneratedKaufmanCorrelationState(oscillator.Length),
        RegressionOscillator oscillator => new GeneratedRegressionResidualState(oscillator.Length, count),
        OneLCLeastSquaresMovingAverage average => new GeneratedOneLcState(average.Length, average.MaType),
        _1LCLeastSquaresMovingAverage average => new GeneratedOneLcState(average.Length, average.MaType),
        AdaptiveLeastSquares average => new GeneratedAdaptiveRangeFitState(average.Length),
        PolynomialLeastSquaresMovingAverage average => new GeneratedPolynomialCellState(average.Length),
        QuadraticRegression average => new GeneratedQuadraticProjectionState(average.Length, count),
        QuadraticLeastSquaresMovingAverage average => new GeneratedQuadraticFitState(average.Length, count),
        FisherLeastSquaresMovingAverage average => new GeneratedFisherLsmaState(average.Length),
        TStepLeastSquaresMovingAverage average => new GeneratedTStepLsmaState(average.Length),
        SimplifiedLeastSquaresMovingAverage average => new GeneratedSimplifiedLsmaState(average.Length, count),
        Lsma average => new GeneratedLsmaState(average.Length, count),
        LinReg fit => new GeneratedRegressionOutputsState(fit.Length, count),
        LinearChannelMiddle fit => new GeneratedRegressionOutputsState(fit.Length, count),
        LinRegSlope fit => new GeneratedRegressionState(fit.Length, count, RegressionReading.Slope),
        LinearRegressionSlope fit => new GeneratedRegressionState(fit.Length, count, RegressionReading.Slope),
        LinRegIntercept fit => new GeneratedRegressionState(fit.Length, count, RegressionReading.GlobalIntercept),
        LinearRegressionIntercept fit => new GeneratedRegressionState(fit.Length, count, RegressionReading.GlobalIntercept),
        LinearRegressionLine fit => new GeneratedRegressionState(fit.Length, count, RegressionReading.Line),
        Alma average => new GeneratedAlmaState(average.Length, count),
        JsaMovingAverage average => new GeneratedJsaState(average.Length, count),
        SelfWeightedMovingAverage average => new GeneratedSelfWeightedState(average.Length),
        SymmetricallyWeightedMovingAverage filter => new GeneratedSymmetricMeanState(filter.Length, count),
        EhlersTriangleMovingAverage filter => new GeneratedSymmetricMeanState(filter.Length, count),
        EhlersTriangleWindowIndicator filter => new GeneratedTriangleIndicatorState(filter.Length),
        EhlersHannMovingAverage filter => new GeneratedHannMeanState(filter.Length, count),
        EhlersHammingMovingAverage filter => new GeneratedHammingMeanState(filter.Length, count),
        HammingMa filter => new GeneratedHammingMeanState(filter.Length, count),
        EhlersHannWindowIndicator filter => new GeneratedHannIndicatorState(filter.Length),
        EhlersHammingWindowIndicator filter => new GeneratedHammingIndicatorState(filter.Length),
        EhlersEarlyOnsetTrendIndicator filter => new GeneratedEarlyOnsetState(filter.Length1, filter.Length2, filter.K),
        EhlersImpulseReaction filter => new GeneratedImpulseReactionState(filter.Length1, filter.Length2, filter.Q),
        EhlersImpulseResponse filter => new GeneratedImpulseResponseState(filter.Length, filter.Bw),
        EhlersBandPassFilterV1 filter => new GeneratedBandPassV1State(filter.Length, filter.Bw),
        EhlersBandPassFilterV2 filter => new GeneratedBandPassState(filter.Length, filter.Bw, 1),
        EhlersCycleBandPassFilter filter => new GeneratedBandPassState(filter.Length, filter.Delta, 2),
        EhlersSuperPassbandFilter filter => new GeneratedSuperPassbandState(filter.FastLength, filter.SlowLength, filter.Length1, filter.Length2),
        EhlersCyberCycle filter => new GeneratedCyberCycleState(2d / (Math.Max(1, filter.Length) + 1d)),
        EhlersCyberCycleOscillator filter => new GeneratedCyberCycleState(filter.Alpha),
        EhlersInstantaneousTrendlineV2 filter => new GeneratedInstantaneousTrendState(filter.Alpha),
        EhlersGaussianFilter filter => new GeneratedGaussianState(filter.Length),
        EhlersRoofingFilter filter => new GeneratedRoofingV1State(filter.HpLength, filter.LpLength),
        EhlersRoofingFilterV1 filter => new GeneratedRoofingV1State(filter.Length1, filter.Length2),
        EhlersRoofingFilterV2 filter => new GeneratedRoofingV2State(filter.UpperLength, filter.LowerLength, false),
        EhlersRoofingFilterIndicator filter => new GeneratedRoofingV2State(filter.Length1, filter.Length2, true),
        EhlersHpLpRoofingFilter filter => new GeneratedHpLpRoofingState(filter.Length1, filter.Length2, false),
        EhlersZeroMeanRoofingFilter filter => new GeneratedHpLpRoofingState(filter.Length1, filter.Length2, true),
        EhlersDetrendedLeadingIndicator filter => new GeneratedDetrendedLeadingState(filter.Length),
        EhlersHighPassFilterV2 filter => new GeneratedHighPassV2State(filter.Length),
        EhlersDecyclerOscillatorV1 filter => new GeneratedDecyclerOscillatorState(filter.Length),
        EhlersDecyclerOscillatorV2 filter => new GeneratedDecyclerV2State(filter.FastLength, filter.SlowLength),
        EhlersDecycler filter => new GeneratedDecyclerState(filter.Length),
        EhlersHighPassFilterV1 filter => new GeneratedHighPassState(filter.Length),
        EhlersLeadingIndicator => new GeneratedLeadingState(),
        EhlersSimpleDecycler filter => new GeneratedSimpleDecyclerState(filter.Length),
        QuickMovingAverage average => new GeneratedQuickState(average.Length, count),
        WellesWilderSummation sum => new GeneratedWilderSumState(sum.Length),
        WildersSummationMethod sum => new GeneratedWilderSumState(sum.Length),
        HullMovingAverage average => new HullValueState(average.Length),
        TriangularMovingAverage average => new GeneratedTriangularState(average.Length),
        EhlersZeroLagEma average => new GeneratedEhlersZeroLagState(average.Length),
        EhlersZeroLagExponentialMovingAverage average => new GeneratedEhlersZeroLagState(average.Length),
        Tsi strength => new GeneratedTrueStrengthState([strength.LongLength, strength.ShortLength], 7, count),
        TrueStrengthIndex strength => new GeneratedTrueStrengthState([strength.Length1, strength.Length2], strength.SignalLength, count),
        ErgodicTrueStrengthIndexV1 strength => new GeneratedTrueStrengthState([strength.Length1, strength.Length2, strength.Length3], strength.SignalLength, count),
        SMIErgodicIndicator strength => new GeneratedTrueStrengthState([strength.FastLength, strength.SlowLength], strength.SignalLength, count),
        Kvo oscillator => new GeneratedKlingerState(oscillator.Length, 55, 13),
        KlingerVolumeOscillator oscillator => new GeneratedKlingerState(oscillator.FastLength, oscillator.SlowLength, oscillator.SignalLength),
        UltimateOscillator oscillator => new GeneratedUltimateState(oscillator.Length1, oscillator.Length2, oscillator.Length3),
        StdDev deviation => new GeneratedStandardDeviationAliasState(deviation.Length, count),
        HistoricalVolatility volatility => new GeneratedHistoricalVolatilityState(Math.Max(1, volatility.Length), count),
        Variance variance => new GeneratedVarianceState(variance.Length, count),
        CoefficientOfVariation variation => new GeneratedCoefficientState(variation.Length, count),
        StandardError error => new GeneratedStandardErrorState(error.Length, count),
        Adl line => new AdlValueState(line.Length, count),
        _ => throw new InvalidOperationException("Unqualified generated state.")
    };

    private sealed class EmaValueState(int period) : IIndicatorState
    {
        private readonly Streaming.EmaState _state = new(period);
        public void Reset() => _state.Reset();
        public double Update(in Bar bar) => _state.GetNext(bar.Close, commit: true);
    }

    private sealed class GeneratedRatioState(int period, int count, bool momentum = false) : IIndicatorState
    {
        private readonly double[] _lag = new double[Math.Min(period, Math.Max(1, count))];
        private int _position, _seen;
        public void Reset() { _position = _seen = 0; }
        public double Update(in Bar bar)
        {
            double prior = _seen >= period ? _lag[_position] : 0;
            double value = momentum ? RoundedMomentumRatio.Of(bar.Close, prior)
                : RoundedPercentageChange.Of(bar.Close, prior);
            _lag[_position] = bar.Close;
            if (++_position == _lag.Length) _position = 0;
            if (_seen < period) _seen++;
            return value;
        }
    }

    private sealed class GeneratedCommodityState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly CommodityIndexWindow _window = new(MovingAvgType.SimpleMovingAverage, period, .015, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(CommodityIndexWindow.TypicalPrice(bar.High, bar.Low, bar.Close), true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAroonState(int period) : IMultiOutputState
    {
        private readonly WindowExtremeDeque _high = new(Math.Max(2, period), true);
        private readonly WindowExtremeDeque _low = new(Math.Max(2, period), false);
        private long _index;
        public void Reset() { _high.Reset(); _low.Reset(); _index = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            _high.Add(bar.Close); _low.Add(bar.Close);
            double up = (double)(period - (_index - _high.Index)) / period * 100;
            double down = (double)(period - (_index - _low.Index)) / period * 100;
            _index++;
            outputs[0] = up - down;
            outputs[1] = up;
            outputs[2] = down;
        }
    }

    private sealed class GeneratedChaikinFlowState(int period) : IIndicatorState, IDisposable
    {
        private readonly ChaikinFlowWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVolumeIndexState(int period, bool positive, int initialValue = 1000) : IMultiOutputState, IDisposable
    {
        private readonly VolumeIndexWindow _window = new(MovingAvgType.ExponentialMovingAverage, period, positive, initialValue);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDirectionalState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly DirectionalIndexWindow _window = new(MovingAvgType.WildersSmoothingMethod, Math.Max(1, period), Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Plus;
            outputs[1] = value.Minus;
            outputs[2] = value.Adx;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVolumeGapState(int fastPeriod, int slowPeriod, int signalPeriod) : IMultiOutputState
    {
        private readonly GeneratedEmaGapSignalState _state = new(fastPeriod, slowPeriod, signalPeriod, percentage: true);
        public void Reset() => _state.Reset();
        public void Update(in Bar bar, Span<double> outputs) =>
            _state.Update(new Bar(default, 0, 0, 0, bar.Volume, 0), outputs);
    }

    private sealed class GeneratedVolumeSmaGapState(int period, int count) : IIndicatorState, IDisposable
    {
        // Unfillable windows publish zero throughout this owned execution.
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _fast = count >= 5 ? new(5) : null;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _slow = Math.Max(1, period) <= count ? new(period) : null;
        public void Reset() { _fast?.Reset(); _slow?.Reset(); }
        public double Update(in Bar bar) => RoundedPercentageChange.Of(
            _fast?.Next(bar.Volume, true) ?? 0, _slow?.Next(bar.Volume, true) ?? 0);
        public void Dispose() { _fast?.Dispose(); _slow?.Dispose(); }
    }

    private sealed class GeneratedPriceVolumeBalanceState(int period, int count) : IMultiOutputState, IDisposable
    {
        // A lag that cannot be reached contributes only exact zero changes.
        private readonly LaggedBalanceWindow? _price = Math.Max(1, period) < count ? new(period) : null;
        private readonly LaggedBalanceWindow? _volume = count > 14 ? new(14) : null;
        public void Reset() { _price?.Reset(); _volume?.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs[0] = _price?.Next(bar.Close, true) ?? 0;
            outputs[1] = _volume?.Next(bar.Volume, true) ?? 0;
        }
        public void Dispose() { _price?.Dispose(); _volume?.Dispose(); }
    }

    private sealed class GeneratedAccumulationState(int period) : IMultiOutputState, IDisposable
    {
        private readonly MoneyFlowAverageWindow _window = new(MovingAvgType.ExponentialMovingAverage, period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedWilliamsAccumulationState : IIndicatorState
    {
        private readonly WilliamsAccumulationWindow _window = new();
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Publish();
    }

    private sealed class GeneratedSmoothedWilliamsState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly WilliamsAccumulationWindow _line = new();
        private readonly RocBankAverage _signal = new(MovingAvgType.SimpleMovingAverage, period, count);
        public void Reset() { _line.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _line.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Publish();
            outputs[1] = _signal.Next(value, true).Publish();
        }
        public void Dispose() => _signal.Dispose();
    }

    private sealed class GeneratedEaseState(double divisor = 1000000) : IIndicatorState
    {
        private readonly EaseWindow _window = new(divisor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Volume, true).Publish();
    }

    private sealed class GeneratedDemarkerState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly DemarkerWindow _window = new(MovingAvgType.SimpleMovingAverage, period, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAtrDerivedState(int period, int count, double? multiplier = null) : IIndicatorState, IDisposable
    {
        private readonly AtrDerivedWindow _window = new(period, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar)
        {
            var atr = _window.Next(bar.High, bar.Low, bar.Close, true);
            return multiplier.HasValue ? AtrDerivedWindow.Width(atr, multiplier.Value) : AtrDerivedWindow.Percent(atr, bar.Close);
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedBetterEmaState(int period) : IIndicatorState
    {
        private readonly BetterEmaWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedHoltState(int period) : IIndicatorState
    {
        private readonly HoltWindow _window = new(period, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedRegularizedState(int period) : IIndicatorState
    {
        private readonly RegularizedWindow _window = new(period, .5);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedAdaptiveLaguerreState(int period) : IIndicatorState
    {
        private readonly AdaptiveLaguerreWindow _window = new(period, 5);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedSelfAdjustingLaguerreState(int period) : IIndicatorState
    {
        private readonly SelfAdjustingLaguerreWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Open, bar.High, bar.Low, true);
    }

    private sealed class GeneratedLaguerreFilterState(int period) : IIndicatorState
    {
        private readonly LaguerreFilterWindow _window = new(2d / (Math.Max(1, period) + 1d));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedLaguerreRsiState(int period) : IIndicatorState
    {
        private readonly LaguerreRsiWindow _window = new(1 - 2d / (Math.Max(1, period) + 1d));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedRepulsionState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly RepulsionWindow _window = new(MovingAvgType.SimpleMovingAverage, period, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedCompoundRatioState(int period) : IIndicatorState, IDisposable
    {
        private readonly CompoundRatioWindow _window = new(MovingAvgType.WeightedMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedGeneralizedDoubleState(int period, double factor) : IIndicatorState, IDisposable
    {
        private readonly GeneralizedDoubleWindow _window = new(MovingAvgType.ExponentialMovingAverage, period, factor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDoubleSmoothingState : IIndicatorState
    {
        private readonly DoubleSmoothingWindow _window = new(.01, .9);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedLightLeastSquaresState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly LightLeastSquaresWindow _window = new(MovingAvgType.SimpleMovingAverage, period, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedLinearExtrapolationState(int period, int count) : IIndicatorState
    {
        private readonly LinearExtrapolationWindow? _window = Math.Max(1, period) >= count ? null : new(period);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window?.Next(bar.Close, true) ?? 0;
    }

    private sealed class GeneratedRecursiveTrendState(int period) : IIndicatorState
    {
        private readonly RecursiveTrendWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedEhlersIirState(int period) : IIndicatorState, IDisposable
    {
        private readonly EhlersIirWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedEhlersFirState : IIndicatorState, IDisposable
    {
        private readonly EhlersFirWindow _window = new();
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedEllipticState(bool modified) : IIndicatorState
    {
        private readonly EllipticWindow _window = new(modified);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedSpencerState(bool longWindow) : IIndicatorState, IDisposable
    {
        private readonly SpencerWindow _window = new(longWindow);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDampedSineState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly DampedSineWindow? _window = count == 0 ? null : new(period, count);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window!.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedHendersonState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly HendersonWindow? _window = count == 0 ? null : new(period, count);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window!.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedRickerState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly RickerWindow? _window = count == 0 ? null : new(period, 60, count);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window!.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedFibonacciMeanState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly FibonacciWindowMean? _window = count == 0 ? null : new(period, count);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window!.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedSquareRootMeanState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly SquareRootWindowMean? _window = count == 0 ? null : new(period, count);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window!.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedSineMeanState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly SineWindowMean? _window = count == 0 ? null : new(period, count);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window!.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedTillsonIe2State(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly TillsonIe2Window _window = new(MovingAvgType.SimpleMovingAverage, period, capacityHint: count, compactFit: true);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedIirLeastSquaresState(int period) : IIndicatorState
    {
        private readonly IirLeastSquaresWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedHullEstimateState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly HullEstimateWindow _window = new(period, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTwoPoleState(int period, int variant) : IIndicatorState
    {
        private readonly TwoPoleWindow _window = new(period, variant);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedThreePoleState(int period, int variant) : IIndicatorState
    {
        private readonly ThreePoleWindow _window = new(period, variant);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedVariableLengthState(int minimum, int maximum) : IMultiOutputState, IDisposable
    {
        private readonly VariableLengthWindow _window = new(MovingAvgType.SimpleMovingAverage, minimum, maximum);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Length;
            outputs[1] = point.Value;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVolumeBalanceState(int period, VolumeBalanceKind kind) : IIndicatorState, IDisposable
    {
        private readonly VolumeBalanceWindow _window = new(period, kind);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    // The legacy named-input helper applies its per-bar range policy only when
    // the entire derived series is not equal to the closes (ordinary == equality).
    private static bool UsesVolumeFlowCustomRange(Bar[] source, CancellationToken cancellation)
    {
        foreach (var bar in source)
        {
            cancellation.ThrowIfCancellationRequested();
            // Preserve the normal input-validation order for invalid source bars.
            if (!AllFieldsFinite(in bar)) return false;
#pragma warning disable S1244 // Legacy routing requires exact numeric equality, including equal signed zeros.
            if (PriceMean.Of(bar.High, bar.Low, bar.Close) != bar.Close) return true;
#pragma warning restore S1244
        }
        return false;
    }

    private sealed class GeneratedVolumeFlowState(int flow, int variance, int signal, int smooth, double coefficient, double volumeCoefficient, bool customRange) : IMultiOutputState, IDisposable
    {
        private readonly VolumeFlowWindow _window = new(MovingAvgType.SimpleMovingAverage, flow, variance, signal, smooth, coefficient, volumeCoefficient);
        private double _previousTypical;
        private bool _started;
        public void Reset() { _window.Reset(); _previousTypical = 0; _started = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var namedPrice = PriceMean.Of(bar.High, bar.Low, bar.Close);
            var high = bar.High; var low = bar.Low;
            if (customRange && !CalculationsHelper.IsWithinBarRange(namedPrice, low, high))
            {
                var previous = _started ? _previousTypical : namedPrice;
                high = Math.Max(previous, namedPrice); low = Math.Min(previous, namedPrice);
            }
            var typical = new ExactMeanAccumulator();
            typical.Add(high); typical.Add(low); typical.Add(bar.Close);
            var point = _window.Next(typical.Mean(3), bar.Close, bar.Volume, true);
            outputs[0] = point.Line;
            outputs[1] = point.Signal;
            outputs[2] = point.Histogram;
            _previousTypical = namedPrice; _started = true;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedFiniteVolumeState(int period, double factor) : IIndicatorState, IDisposable
    {
        private readonly FiniteVolumeWindow _window = new(MovingAvgType.SimpleMovingAverage, period, factor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedRelativeVolumeState(int period) : IMultiOutputState, IDisposable
    {
        private readonly RelativeVolumeWindow _window = new(MovingAvgType.SimpleMovingAverage, period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = point.Score;
            outputs[1] = point.Demand;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedBetterVolumeState(int period, int lookback) : IIndicatorState, IDisposable
    {
        private readonly BetterVolumeWindow _window = new(period, lookback, includeSignals: false);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedAverageMoneyFlowState(int period) : IIndicatorState, IDisposable
    {
        private readonly AverageMoneyFlowWindow _window = new(MovingAvgType.WeightedMovingAverage, period, 3);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Volume, true).Value;
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedTrendContinuationState(int period) : IMultiOutputState, IDisposable
    {
        private readonly TrendContinuationWindow _window = new(period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Plus; outputs[1] = point.Minus;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedTrendTriggerState(int period) : IMultiOutputState, IDisposable
    {
        private readonly TrendTriggerWindow _window = new(period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => outputs[0] = _window.Next(bar.High, bar.Low, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedTrendIntensityState(int period) : IMultiOutputState, IDisposable
    {
        private readonly TrendIntensityWindow _window = new(MovingAvgType.SimpleMovingAverage, period, 60);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => outputs[0] = _window.Next(bar.Close, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private static bool UsesValueChartCustomRange(Bar[] source, CancellationToken cancellation)
    {
        foreach (var bar in source)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!AllFieldsFinite(in bar)) return false;
#pragma warning disable S1244 // Preserve legacy SequenceEqual numeric equality, including equal signed zeros.
            if (ValueChartWindow.Median(bar.High, bar.Low).Publish() != bar.Close) return true;
#pragma warning restore S1244
        }
        return false;
    }

    private static void ValidatePrimeGraphInput(Bar[] source, IReadOnlyList<IIndicator> indicators, CancellationToken cancellation)
    {
        // Raw-input errors precede every domain; domains then follow root order.
        foreach (var bar in source)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!AllFieldsFinite(in bar)) Validation.IndicatorInputDomain.Finite.Validate(in bar);
        }
        foreach (var indicator in indicators)
        {
            if (indicator is not (PrimeNumberOscillator or PrimeNumberBands or AbsoluteStrengthIndex or ClosedFormDistanceVolatility)) continue;
            var domain = Validation.IndicatorInputDomain.For(indicator);
            foreach (var bar in source)
            {
                cancellation.ThrowIfCancellationRequested();
                domain.Validate(in bar);
            }
        }
    }

    private sealed class GeneratedPrimeOscillatorState(int length) : IIndicatorState
    {
        private readonly PrimeOffsetWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedPrimeBandState(int length) : IMultiOutputState
    {
        private readonly PrimeBandWindow _window = new(length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var pair = _window.Next(bar.High, bar.Low, true);
            outputs[0] = pair.Upper; outputs[1] = pair.Lower;
        }
    }

    private sealed class GeneratedEfficientTrendStepState(int length, int fast, int slow, int count) : IMultiOutputState, IDisposable
    {
        private readonly EfficientTrendStepWindow _window = new(length, fast, slow, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedLinearChannelState(int length, double multiplier) : IMultiOutputState
    {
        private double _previous, _older, _upper, _lower;
        private int _history;
        public void Reset() { _previous = _older = _upper = _lower = 0; _history = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var step = 1d / length;
            var previous = _history >= 1 ? _previous : bar.Close;
            var older = _history >= 2 ? _older : bar.Close;
            var predicted = bar.Close + ((previous - older) * multiplier);
            var center = predicted > previous + step ? previous + step : predicted < previous - step ? previous - step : previous;
            var upper = center + (Math.Abs(center - previous) * multiplier);
            var lower = center - (Math.Abs(center - previous) * multiplier);
#pragma warning disable S1244 // Preserve the batch/native boundary retention rule exactly.
            _upper = upper == center ? _upper : upper; _lower = lower == center ? _lower : lower;
#pragma warning restore S1244
            outputs[0] = _upper; outputs[1] = _lower;
            _older = _previous; _previous = center; _history = Math.Min(2, _history + 1);
        }
    }

    private static bool UsesVervoortModifiedCustomRange(Bar[] source, CancellationToken cancellation)
    {
        foreach (var bar in source)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!AllFieldsFinite(in bar)) return false;
#pragma warning disable S1244 // Preserve legacy whole-series numeric equality, including equal signed zeros.
            if (PriceMean.Of(bar.Open, bar.High, bar.Low, bar.Close) != bar.Close) return true;
#pragma warning restore S1244
        }
        return false;
    }

    private sealed class GeneratedVervoortModifiedState(int band, int outer, int smooth, double multiplier, bool customRange) : IMultiOutputState, IDisposable
    {
        private readonly VervoortModifiedWindow _window = new(MovingAvgType.TripleExponentialMovingAverage, band, outer, smooth, multiplier);
        private double _previousTypical;
        private bool _started;
        public void Reset() { _window.Reset(); _previousTypical = 0; _started = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var namedPrice = PriceMean.Of(bar.Open, bar.High, bar.Low, bar.Close);
            var high = bar.High; var low = bar.Low;
            if (customRange && !CalculationsHelper.IsWithinBarRange(namedPrice, low, high))
            {
                var previous = _started ? _previousTypical : namedPrice;
                high = Math.Max(previous, namedPrice); low = Math.Min(previous, namedPrice);
            }
            var mean = new ExactMeanAccumulator(); mean.Add(bar.Open); mean.Add(high); mean.Add(low); mean.Add(bar.Close);
            var value = _window.Next(mean.Mean(4), high, low, true);
            outputs[0] = value.Upper; outputs[1] = 50; outputs[2] = value.Lower; outputs[3] = value.Percent;
            _previousTypical = namedPrice; _started = true;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVervoortVolatilityState(int length1, int length2, double deviation, double lowerMultiplier) : IMultiOutputState, IDisposable
    {
        private readonly VervoortVolatilityWindow _window = new(MovingAvgType.ExponentialMovingAverage, length1, length2, deviation, lowerMultiplier);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, bar.Low, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedProjectionFamilyState(IndicatorName family, int length) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.ProjectionFamilyKernel _window = new(family, length, MovingAvgType.WeightedMovingAverage, 4);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.High, bar.Low, true);
            if (family == IndicatorName.ProjectionBands)
            {
                outputs[0] = point.Bands.Upper; outputs[1] = point.Bands.Middle; outputs[2] = point.Bands.Lower;
            }
            else
            {
                outputs[0] = Streaming.ProjectionBandsSnapshot.Publish(point.Line);
                outputs[1] = Streaming.ProjectionBandsSnapshot.Publish(point.Mean);
            }
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedUniChannelState(int length, double upper, double lower, bool additive, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _mean = length <= count ? new(length) : null;
        public void Reset() => _mean?.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var middle = _mean?.Next(bar.Close, true) ?? 0;
            outputs[0] = UniChannelArithmetic.Band(middle, upper, additive);
            outputs[1] = middle;
            outputs[2] = UniChannelArithmetic.Band(middle, -lower, additive);
        }
        public void Dispose() => _mean?.Dispose();
    }

    private sealed class GeneratedPseudoPolynomialState(int length, double morph, int count) : IMultiOutputState, IDisposable
    {
        private readonly PseudoPolynomialWindow _window = new(MovingAvgType.SimpleMovingAverage, length, morph, capacityHint: Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedFractalChaosState(bool oscillator) : IMultiOutputState, IDisposable
    {
        private readonly PooledRingBuffer<double> _highs = new(4), _lows = new(4);
        private double _upper, _lower;
        public void Reset() { _highs.Clear(); _lows.Clear(); _upper = _lower = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            Streaming.StreamingFractal.Find(_highs, _lows, bar.High, bar.Low, out var up, out var down);
            var upper = up ?? _upper; var lower = down ?? _lower;
#pragma warning disable S1244 // Legacy oscillator detects exact numeric boundary changes, including equal signed zeros.
            if (oscillator) outputs[0] = upper != _upper ? 1 : lower != _lower ? -1 : 0;
#pragma warning restore S1244
            else
            {
                var midpoint = new ExactMeanAccumulator(); midpoint.Add(upper); midpoint.Add(lower);
                outputs[0] = upper; outputs[1] = midpoint.Mean(2); outputs[2] = lower;
            }
            _highs.TryAdd(bar.High, out _); _lows.TryAdd(bar.Low, out _);
            _upper = upper; _lower = lower;
        }
        public void Dispose() { _highs.Dispose(); _lows.Dispose(); }
    }

    private sealed class GeneratedFlaggingBandState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly FlaggingBandWindow _window = new(length, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower; outputs[3] = value.Stop;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedExtendedBandState(int length) : IMultiOutputState
    {
        private readonly ExtendedBandWindow _window = new(length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
    }

    private sealed class GeneratedMotionAttractionState(int length) : IMultiOutputState
    {
        private readonly MotionAttractionWindow _window = new(length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
    }

    private sealed class GeneratedVolumeAdaptiveBandState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly VolumeAdaptiveBandWindow _window = new(MovingAvgType.SimpleMovingAverage, length, capacityHint: Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedScalperChannelState(int rangeLength, int averageLength, int count) : IMultiOutputState, IDisposable
    {
        // Match the batch extrema's physical-slot tie order for small windows.
        // Larger windows retain only observed deque entries, without a period-sized ring.
        private RollingMinMax _high = new(rangeLength), _low = new(rangeLength);
        private readonly KeltnerWindow _average = new(MovingAvgType.SimpleMovingAverage, averageLength, averageLength, MovingAvgType.SimpleMovingAverage, Math.Max(1, count));
        public void Reset() { _high = new(rangeLength); _low = new(rangeLength); _average.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            _high.Add(bar.High); _low.Add(bar.Low);
            var stages = _average.Next(bar.High, bar.Low, bar.Close, true);
            var midpoint = new ExactMeanAccumulator(); midpoint.Add(_high.Max); midpoint.Add(_low.Min);
            outputs[0] = _high.Max; outputs[1] = midpoint.Mean(2); outputs[2] = _low.Min;
            outputs[3] = ScalperChannelWindow.Scalper(stages.Middle, stages.Atr);
        }
        public void Dispose() => _average.Dispose();
    }

    private sealed class GeneratedHurstCycleState(int fastLength, int slowLength, double fastFactor, double slowFactor, int count) : IMultiOutputState, IDisposable
    {
        private readonly HurstCycleWindow _window = new(MovingAvgType.WildersSmoothingMethod, fastLength, slowLength, fastFactor, slowFactor, capacityHint: Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.FastUpper; outputs[1] = value.FastMiddle; outputs[2] = value.FastLower;
            outputs[3] = value.SlowUpper; outputs[4] = value.SlowMiddle; outputs[5] = value.SlowLower;
            outputs[6] = value.OMed; outputs[7] = value.OShort;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedHurstBandState(int length, double inner, double outer, double extreme, int count) : IMultiOutputState, IDisposable
    {
        private readonly HurstBandWindow _window = new(length, inner, outer, extreme, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.UpperExtreme; outputs[1] = value.UpperOuter; outputs[2] = value.UpperInner; outputs[3] = value.Middle;
            outputs[4] = value.LowerExtreme; outputs[5] = value.LowerOuter; outputs[6] = value.LowerInner;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTrendTraderBandState(int length, double multiplier, double step, int count) : IMultiOutputState, IDisposable
    {
        private readonly TrendTraderWindow _window = new(MovingAvgType.WeightedMovingAverage, length, multiplier, step, capacityHint: Math.Max(1, count), maxInputCount: count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRegressionChannelState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly ExactRegressionChannelWindow? _window = length <= count ? new(length, 2) : null;
        // Deviation stays zero for an unfilled window, but the partial fit still evolves.
        private readonly ExactLinearFitWindow? _fit = length > count ? new(length, observedHistory: true) : null;
        public void Reset() { _window?.Reset(); _fit?.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (_window is not null)
            {
                var value = _window.Next(bar.Close, true);
                outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
            }
            else
            {
                var fit = _fit!.Next(bar.Close, true);
                outputs[0] = fit.Offset(0, 2); outputs[1] = fit.Last; outputs[2] = fit.Offset(0, -2);
            }
        }
        public void Dispose() { _window?.Dispose(); _fit?.Dispose(); }
    }

    private sealed class GeneratedRocBandState(int length, int smoothLength, int count) : IMultiOutputState, IDisposable
    {
        private readonly RocBandWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, smoothLength, capacityHint: Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = 0; outputs[2] = -value.Upper; outputs[3] = value.Roc;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedKirshenbaumState(int meanLength, int errorLength, double factor, int count) : IMultiOutputState, IDisposable
    {
        private readonly KirshenbaumWindow _window = new(MovingAvgType.ExponentialMovingAverage, meanLength, errorLength, factor, capacityHint: Math.Max(1, count), maxInputCount: count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSmoothedVolatilityState(int length1, int length2, double deviation, double adjustment, int count) : IMultiOutputState, IDisposable
    {
        private readonly SmoothedVolatilityWindow _window = new(MovingAvgType.ExponentialMovingAverage, length1, length2, deviation, adjustment, capacityHint: Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedBollingerMiddleState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _average = length <= count ? new(length) : null;
        public void Reset() => _average?.Reset();
        public double Update(in Bar bar) => _average?.Next(bar.Close, true) ?? 0;
        public void Dispose() => _average?.Dispose();
    }

    private sealed class GeneratedBollingerAtrState(MovingAvgType kind, int atrLength, int length, double multiplier, int count) : IIndicatorState, IDisposable
    {
        // The only published ratio is zero while its full deviation window is unfilled.
        private readonly BollingerAtrWindow? _window = length <= count ? new(kind, atrLength, length, multiplier, capacityHint: Math.Max(1, count)) : null;
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window?.Next(bar.High, bar.Low, bar.Close, true).Value ?? 0;
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedAtrPercentBandState(int length, int centerLength, double multiplier, int count) : IMultiOutputState, IDisposable
    {
        private readonly AtrPercentBandWindow _window = new(MovingAvgType.SimpleMovingAverage, length, centerLength, multiplier, capacityHint: Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedBollingerState(int length, double multiplier, int count, int kind) : IMultiOutputState, IDisposable
    {
        // Both kernels publish zero until the entire window exists.
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _average = length <= count ? new(length) : null;
        private readonly ExactPopulationWindow? _deviation = length <= count ? new(length) : null;
        public void Reset() { _average?.Reset(); _deviation?.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var middle = _average?.Next(bar.Close, true) ?? 0;
            var deviation = _deviation?.Next(bar.Close, true) ?? 0;
            if (kind == 1) outputs[0] = BollingerArithmetic.Width(middle, deviation, multiplier);
            else if (kind == 2) outputs[0] = BollingerArithmetic.Percent(bar.Close, middle, deviation, multiplier);
            else
            {
                outputs[0] = BollingerArithmetic.Band(middle, deviation, multiplier);
                outputs[1] = middle;
                outputs[2] = BollingerArithmetic.Band(middle, deviation, -multiplier);
            }
        }
        public void Dispose() { _average?.Dispose(); _deviation?.Dispose(); }
    }

    private sealed class GeneratedIqrBandState(int length, double multiplier, int count) : IMultiOutputState, IDisposable
    {
        private RollingOrderStatistic _order = new(Math.Min(length, Math.Max(1, count)));
        public void Reset() { _order.Dispose(); _order = new(Math.Min(length, Math.Max(1, count))); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            _order.Add(bar.Close);
            var first = _order.PercentileNearestRank(25);
            var third = _order.PercentileNearestRank(75);
            outputs[0] = RangeBandArithmetic.Band(third, third, first, multiplier);
            outputs[1] = RangeBandArithmetic.Midpoint(first, third);
            outputs[2] = RangeBandArithmetic.Band(first, third, first, -multiplier);
        }
        public void Dispose() => _order.Dispose();
    }

    private sealed class GeneratedRangeBandState(int length, double factor, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _average = length <= count ? new(length) : null;
        private readonly Streaming.RollingWindowMax _high = new(Math.Min(length, Math.Max(1, count)));
        private readonly Streaming.RollingWindowMin _low = new(Math.Min(length, Math.Max(1, count)));
        public void Reset() { _average?.Reset(); _high.Reset(); _low.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var middle = _average?.Next(bar.Close, true) ?? 0;
            var high = _high.Add(middle, out _);
            var low = _low.Add(middle, out _);
            outputs[0] = RangeBandArithmetic.Band(middle, high, low, factor);
            outputs[1] = middle;
            outputs[2] = RangeBandArithmetic.Band(middle, high, low, -factor);
        }
        public void Dispose() { _average?.Dispose(); _high.Dispose(); _low.Dispose(); }
    }

    private sealed class GeneratedMaeBandState(int length, double factor, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _average = length <= count ? new(length) : null;
        private readonly ExactCumulativeErrorBands _errors = new(factor);
        public void Reset() { _average?.Reset(); _errors.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var middle = _average?.Next(bar.Close, true) ?? 0;
            var value = _errors.Next(bar.Close, middle, true);
            outputs[0] = value.Upper; outputs[1] = middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _average?.Dispose();
    }

    private sealed class GeneratedMadBandState(int length, double factor, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _average = length <= count ? new(length) : null;
        private readonly ExactMeanAbsoluteDeviationWindow _deviation = new(Math.Min(length, Math.Max(1, count)));
        public void Reset() { _average?.Reset(); _deviation.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var middle = _average?.Next(bar.Close, true) ?? 0;
            var deviation = _deviation.Next(bar.Close, true);
            outputs[0] = BollingerArithmetic.Band(middle, deviation, factor);
            outputs[1] = middle;
            outputs[2] = BollingerArithmetic.Band(middle, deviation, -factor);
        }
        public void Dispose() { _average?.Dispose(); _deviation.Dispose(); }
    }

    private sealed class GeneratedHeadleyBandState(int length, double factor, int count) : IMultiOutputState, IDisposable
    {
        private readonly HeadleyBandWindow _window = new(MovingAvgType.SimpleMovingAverage, length, factor, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRmseBandState(int length, double factor, int count) : IMultiOutputState, IDisposable
    {
        private readonly RmseBandWindow _window = new(MovingAvgType.SimpleMovingAverage, length, factor, capacityHint: Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedMovingAverageBandState(int fast, int slow, double multiplier, bool width, int count) : IMultiOutputState, IDisposable
    {
        private readonly MovingAverageBandWindow _window = new(MovingAvgType.ExponentialMovingAverage, fast, slow, multiplier, maxInputCount: count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            if (width) { outputs[0] = value.Width; return; }
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower; outputs[3] = value.Fast;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedPriceBandState(int length, int direction) : IIndicatorState
    {
        private readonly EmaValueState _average = new(length);
        public void Reset() => _average.Reset();
        public double Update(in Bar bar) => RoundedPercentageBand.Of(_average.Update(bar), .06, direction);
    }

    private sealed class GeneratedPriceDriftState(int length, bool curve, int count) : IMultiOutputState, IDisposable
    {
        private readonly PriceDriftWindow _window = new(MovingAvgType.WildersSmoothingMethod, length, curve, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRangeChannelState(int length, double multiplier, bool rounded, int count) : IMultiOutputState, IDisposable
    {
        private readonly KeltnerWindow _window = new(MovingAvgType.SimpleMovingAverage, Math.Max(1, length), Math.Max(1, length), MovingAvgType.SimpleMovingAverage, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var stages = _window.Next(bar.High, bar.Low, bar.Close, true);
            var value = RangeChannelWindow.Output(bar.Close, stages.Middle, stages.Atr, multiplier, rounded);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
            if (rounded) outputs[3] = value.Average;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedKeltnerWidthState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly KeltnerWindow _window = new(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length), Math.Max(1, length), capacityHint: Math.Max(1, count));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar)
        {
            var stages = _window.Next(bar.High, bar.Low, bar.Close, true);
            return KeltnerWindow.Width(stages.Middle, stages.Atr, 2);
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedHighLowAverageState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly HighLowAverageWindow _window = new(MovingAvgType.WeightedMovingAverage, length, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, true);
            outputs[0] = value.Upper;
            outputs[1] = value.Middle;
            outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSupportResistanceState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly SupportResistanceWindow? _window = length <= count ? new(MovingAvgType.SimpleMovingAverage, length) : null;
        public void Reset() => _window?.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var middle = _window?.Next(bar.Close, true) ?? 0;
            outputs[0] = HighLowBandsWindow.Shift(middle, 2);
            outputs[1] = middle;
            outputs[2] = SupportResistanceWindow.Lower(middle, 2);
        }
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedAverageChannelState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly PriceAverageChannelWindow? _window = length <= count ? new(MovingAvgType.SimpleMovingAverage, length) : null;
        public void Reset() => _window?.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window?.Next(bar.High, bar.Low, true) ?? default;
            outputs[0] = value.Upper;
            outputs[1] = value.Middle;
            outputs[2] = value.Lower;
        }
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedHighLowBandsState(int length, double percent, int count) : IMultiOutputState, IDisposable
    {
        private readonly HighLowBandsWindow? _window = length <= count ? new(MovingAvgType.SimpleMovingAverage, length) : null;
        public void Reset() => _window?.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var middle = _window?.Next(bar.Close, true) ?? 0;
            outputs[0] = HighLowBandsWindow.Shift(middle, percent);
            outputs[1] = middle;
            outputs[2] = HighLowBandsWindow.Shift(middle, -percent);
        }
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedAverageEnvelopeState(int length, double fraction, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _average = length <= count ? new(length) : null;
        public void Reset() => _average?.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var middle = _average?.Next(bar.Close, true) ?? 0;
            outputs[0] = RoundedPercentageBand.Of(middle, fraction, 1);
            outputs[1] = middle;
            outputs[2] = RoundedPercentageBand.Of(middle, fraction, -1);
        }
        public void Dispose() => _average?.Dispose();
    }

    private sealed class GeneratedDisplacedEnvelopeState(int length, int lag, double percent, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.ExponentialMovingAverageSmoother _average = new(length);
        private readonly PooledRingBuffer<double>? _delay = lag < count ? new(lag) : null;
        public void Reset() { _average.Reset(); _delay?.Clear(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var average = _average.Next(bar.Close, true);
            var middle = _delay is not null && _delay.Count == lag ? _delay[0] : 0;
            outputs[0] = RoundedPercentageBand.Percent(middle, percent, 1);
            outputs[1] = middle;
            outputs[2] = RoundedPercentageBand.Percent(middle, percent, -1);
            _delay?.TryAdd(average, out _);
        }
        public void Dispose() { _average.Dispose(); _delay?.Dispose(); }
    }

    private sealed class GeneratedSmartEnvelopeState(int length, double factor) : IMultiOutputState
    {
        private readonly SmartEnvelopeWindow _window = new(length, factor);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
    }

    private sealed class GeneratedDEnvelopeState(int length, double factor) : IMultiOutputState
    {
        private readonly DEnvelopeWindow _window = new(length, factor);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
    }

    private sealed class GeneratedGChannelState(int length) : IMultiOutputState
    {
        private readonly GChannelWindow _window = new(length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
    }

    private sealed class GeneratedTrendScoreState(int endLength, int count) : IIndicatorState, IDisposable
    {
        private readonly int _width = Math.Max(0, endLength - 10);
        private readonly PooledRingBuffer<double>? _delay = endLength >= 11 && count > 11 ? new(11) : null;
        private readonly PooledRingBuffer<double>? _values = endLength >= 11 && count > 11 ? new(Math.Min(endLength - 10, count)) : null;
        private OrderStatisticTree? _ranks;
        public void Reset() { _delay?.Clear(); _values?.Clear(); _ranks = null; }
        public double Update(in Bar bar)
        {
            if (_width == 0) return 0;
            if (_delay is not null)
            {
                if (_delay.Count == 11)
                {
                    _ranks ??= new();
                    if (_values!.TryAdd(_delay[0], out var expired)) _ranks.Remove(expired);
                    _ranks.Insert(_delay[0]);
                }
                _delay.TryAdd(bar.Close, out _);
            }
            var arrived = _ranks?.Count ?? 0;
            var atOrBelow = (_ranks?.CountLessThanOrEqual(bar.Close) ?? 0) + (bar.Close >= 0 ? _width - arrived : 0);
            return 2d * atOrBelow - _width;
        }
        public void Dispose() { Reset(); _delay?.Dispose(); _values?.Dispose(); }
    }

    private sealed class GeneratedKeltnerChannelState(int centerLength, int rangeLength, double multiplier, int count) : IMultiOutputState, IDisposable
    {
        private readonly KeltnerWindow _window = new(MovingAvgType.ExponentialMovingAverage, Math.Max(1, centerLength), Math.Max(1, rangeLength), capacityHint: Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var stages = _window.Next(bar.High, bar.Low, bar.Close, true);
            var value = KeltnerWindow.Bands(stages.Middle, stages.Atr, multiplier);
            outputs[0] = value.Upper; outputs[1] = value.Middle; outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedPriceCycleState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly PriceCycleWindow _window = new(MovingAvgType.SimpleMovingAverage, Math.Max(1, length), count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDailyDeltaState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly DailyDeltaWindow _window = new(MovingAvgType.SimpleMovingAverage, Math.Max(1, length), count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, true);
            outputs[0] = value.Upper; outputs[1] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDrunkardWalkState(int length) : IMultiOutputState
    {
        private readonly DrunkardWalkWindow _window = new(Math.Max(1, length));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Up; outputs[1] = value.Down;
        }
    }

    private sealed class GeneratedDonchianMiddleState(int length, int count) : IIndicatorState
    {
        private readonly PriceExtremeState _high = new(length, count, true), _low = new(length, count, false);
        public void Reset() { _high.Reset(); _low.Reset(); }
        public double Update(in Bar bar) => PriceMean.Of(_high.Update(bar), _low.Update(bar));
    }

    private sealed class GeneratedDonchianWidthState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly DonchianWidthWindow _window = new(MovingAvgType.SimpleMovingAverage, Math.Max(1, length), 22, capacityHint: Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Width; outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedQuickStickState(int length) : IIndicatorState, IDisposable
    {
        private readonly OpenCloseAverageWindow _window = new(MovingAvgType.SimpleMovingAverage, Math.Max(1, length));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Open, bar.Close, true).Signal;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDeltaAverageState(int length, int lag) : IMultiOutputState, IDisposable
    {
        private readonly OpenCloseAverageWindow _window = new(MovingAvgType.SimpleMovingAverage, Math.Max(1, length), Math.Max(1, lag));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Open, bar.Close, true);
            outputs[0] = value.Line; outputs[1] = value.Signal; outputs[2] = value.Histogram;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedCumulativeState(bool volume) : IIndicatorState
    {
        private ExactMeanAccumulator _sum;
        private double _previousClose;
        private bool _started;
        public void Reset() { _sum = default; _previousClose = 0; _started = false; }
        public double Update(in Bar bar)
        {
            if (!volume) _sum.Add(bar.Close);
            else if (_started)
            {
                if (bar.Close > _previousClose) _sum.Add(bar.Volume);
                else if (bar.Close < _previousClose) _sum.Add(bar.Volume, -1);
            }
            _previousClose = bar.Close; _started = true;
            return _sum.Mean(1);
        }
    }

    private sealed class GeneratedConditionalState(int length) : IMultiOutputState
    {
        private readonly Streaming.EmaState _signal = new(Math.Max(1, length));
        private double _value, _previousHigh, _previousLow;
        private bool _started;
        public void Reset() { _signal.Reset(); _value = _previousHigh = _previousLow = 0; _started = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (_started)
            {
                if (bar.Low > _previousHigh) _value += 1d;
                else if (bar.High < _previousLow) _value -= 1d;
            }
            _previousHigh = bar.High; _previousLow = bar.Low; _started = true;
            outputs[0] = _value; outputs[1] = _signal.GetNext(_value, commit: true);
        }
    }

    private sealed class GeneratedDemarkCountState(int window, int lag, int count) : IIndicatorState, IDisposable
    {
        // Missing comparisons are ties, so an unfillable window always publishes zero.
        private readonly PooledRingBuffer<sbyte>? _votes = window <= count ? new(window) : null;
        private readonly PooledRingBuffer<double>? _prices = window <= count && lag <= count ? new(lag) : null;
        private int _up, _down;
        public void Reset() { _votes?.Clear(); _prices?.Clear(); _up = _down = 0; }
        public double Update(in Bar bar)
        {
            if (_votes is null) return 0d;
            var previous = _prices is not null && _prices.Count == lag ? _prices[0] : 0d;
            sbyte vote = bar.Close > previous ? (sbyte)1 : bar.Close < previous ? (sbyte)-1 : (sbyte)0;
            if (_votes.Count == window)
            {
                var expired = _votes[0];
                if (expired > 0) _up--; else if (expired < 0) _down--;
            }
            if (vote > 0) _up++; else if (vote < 0) _down++;
            _votes.TryAdd(vote, out _); _prices?.TryAdd(bar.Close, out _);
            return _up == window || _down == window ? bar.Close : 0d;
        }
        public void Dispose() { Reset(); _votes?.Dispose(); _prices?.Dispose(); }
    }

    private sealed class GeneratedIntradayMomentumState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly IntradayGainLossWindow _window = new(Math.Max(1, length), count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Open, true);
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedRelativeMomentumState(int length, int momentum, int count) : IMultiOutputState, IDisposable
    {
        private readonly RelativeMomentumWindow _window = new(MovingAvgType.WildersSmoothingMethod,
            Math.Max(1, length), Math.Max(1, momentum), count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Value; outputs[1] = point.Signal; outputs[2] = point.Histogram;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedDayRangeState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly int _length = Math.Max(1, length);
        // An unfillable full-window mean is zero and needs no history allocation.
        private readonly PooledRingBuffer<(double High, double Low)>? _history = Math.Max(1, length) <= count ? new(length) : null;
        private ExactMeanAccumulator _sum;
        public void Reset() { _history?.Clear(); _sum = default; }
        public double Update(in Bar bar)
        {
            if (_history is null) return 0d;
            _sum.Add(bar.High); _sum.Add(bar.Low, -1);
            if (_history.Count == _length)
            {
                var old = _history[0];
                _sum.Add(old.High, -1); _sum.Add(old.Low);
            }
            _history.TryAdd((bar.High, bar.Low), out _);
            return _history.Count < _length ? 0d : _sum.Mean(_length);
        }
        public void Dispose() { Reset(); _history?.Dispose(); }
    }

    private sealed class GeneratedAlligatorState(int length, int offset) : IIndicatorState, IDisposable
    {
        private readonly AlligatorLineWindow _window = new(MovingAvgType.WildersSmoothingMethod, Math.Max(1, length), offset);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(PriceMean.Of(bar.High, bar.Low), true);
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedCandlePowerState(bool bull, int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly CandlePowerWindow _window = new(bull, MovingAvgType.ExponentialMovingAverage,
            Math.Max(1, length), capacityHint: count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.Open, bar.High, bar.Low, true);
            outputs[0] = point.Value; outputs[1] = point.Signal;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedAutoLineState(int length, int count) : IIndicatorState, IDisposable
    {
        // The legacy translated two-pass deviation has observable rounding.
        // Its warmup is zero, so unfillable windows need no period-sized storage.
        private readonly Streaming.RollingStandardDeviation? _deviation = Math.Max(1, length) <= count ? new(length) : null;
        private double _previous;
        private bool _started;
        public void Reset() { _deviation?.Reset(); _previous = 0; _started = false; }
        public double Update(in Bar bar)
        {
            var deviation = _deviation?.Next(bar.Close, true) ?? 0d;
            var previous = _started ? _previous : bar.Close;
            _previous = bar.Close > previous + deviation || bar.Close < previous - deviation ? bar.Close : previous;
            _started = true;
            return _previous;
        }
        public void Dispose() { Reset(); _deviation?.Dispose(); }
    }

    private sealed class GeneratedAutoDriftState(int length) : IIndicatorState
    {
        private readonly AutoDriftWindow _window = new(Math.Max(1, length));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedAutoFilterState(int length) : IIndicatorState, IDisposable
    {
        private readonly AutoFilterWindow _window = new(MovingAvgType.SimpleMovingAverage, Math.Max(1, length));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Line;
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedAutoDispersionState(int length, int smoothLength, int count) : IMultiOutputState, IDisposable
    {
        private readonly AutoDispersionWindow _window = new(MovingAvgType.WeightedMovingAverage,
            Math.Max(1, length), Math.Max(1, smoothLength), capacityHint: Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Upper; outputs[1] = point.Middle; outputs[2] = point.Lower;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedAwesomeState(int length, int count, bool accelerator) : IIndicatorState, IDisposable
    {
        // An incomplete full-window SMA is identically zero; avoid period-sized storage.
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _fast = Math.Max(1, length) <= count ? new(length) : null;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _slow = count >= 34 ? new(34) : null;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _signal = accelerator && count >= 5 ? new(5) : null;
        private bool _invalidPrefix;
        public void Reset() { _fast?.Reset(); _slow?.Reset(); _signal?.Reset(); _invalidPrefix = false; }
        public double Update(in Bar bar)
        {
            if (_invalidPrefix) return double.NaN;
            var median = PriceMean.Of(bar.High, bar.Low);
            var awesome = (_fast?.Next(median, true) ?? 0d) - (_slow?.Next(median, true) ?? 0d);
            if (!accelerator) return awesome;
            if (!FrameworkCompatibility.IsFinite(awesome)) { _invalidPrefix = true; return double.NaN; }
            return awesome - (_signal?.Next(awesome, true) ?? 0d);
        }
        public void Dispose() { Reset(); _fast?.Dispose(); _slow?.Dispose(); _signal?.Dispose(); }
    }

    private sealed class GeneratedAnchoredMomentumState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly AnchoredMomentumWindow _window = new(MovingAvgType.ExponentialMovingAverage, 7, 8, Math.Max(1, length), count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Value; outputs[1] = point.Signal;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedAccumulatedSwingState(int length, double limitMove, int count) : IMultiOutputState, IDisposable
    {
        private readonly AccumulatedSwingWindow _window = new(MovingAvgType.SimpleMovingAverage, Math.Max(1, length), limitMove, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Value; outputs[1] = point.Signal;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private static void ValidateAbsoluteStrengthInput(Bar[] source, CancellationToken cancellation)
    {
        Bar? firstNonpositive = null;
        foreach (var bar in source)
        {
            cancellation.ThrowIfCancellationRequested();
            // The typed builder validates every raw bar before any indicator domain.
            if (!AllFieldsFinite(in bar)) Validation.IndicatorInputDomain.Finite.Validate(in bar);
            if (bar.Close <= 0 && firstNonpositive is null) firstNonpositive = bar;
        }
        if (firstNonpositive is Bar invalid) Validation.IndicatorInputDomain.PositiveClose.Validate(in invalid);
    }

    private sealed class GeneratedAbsoluteStrengthState(int length) : IIndicatorState
    {
        private readonly AbsoluteStrengthWindow _window = new(length, 21, 34);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedAbsoluteStrengthMtfState(int length, int smooth, int count) : IMultiOutputState, IDisposable
    {
        private readonly AbsoluteStrengthMtfWindow _window = new(MovingAvgType.SimpleMovingAverage, length, smooth, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Bulls; outputs[1] = point.Bears;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedWilsonState(WilsonRelativePriceChannel channel) : IMultiOutputState, IDisposable
    {
        private readonly WilsonWindow _window = new(MovingAvgType.ExponentialMovingAverage,
            Math.Max(1, channel.Length), Math.Max(1, channel.SmoothLength),
            channel.Oversold, channel.LowerNeutralZone, channel.Overbought, channel.UpperNeutralZone);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => _window.Next(bar.Close, true, outputs);
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedVpciState(int length) : IMultiOutputState, IDisposable
    {
        private readonly VpciWindow _window = new(MovingAvgType.SimpleMovingAverage, 5, 20, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = point.Line; outputs[1] = point.SignalLine;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedVolumeWeightedRsiState(int length) : IIndicatorState, IDisposable
    {
        private readonly VolumeWeightedRsiWindow _window = new(MovingAvgType.WeightedMovingAverage, length, 3);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Volume, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedVortexBandsState(int length) : IMultiOutputState, IDisposable
    {
        private readonly VortexBandWindow _window = new(MovingAvgType.McNichollMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Upper; outputs[1] = point.Middle; outputs[2] = point.Lower;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedWoodieCciState(int fast, int slow, int count) : IMultiOutputState, IDisposable
    {
        private readonly CommodityIndexWindow _fast = new(MovingAvgType.SimpleMovingAverage, fast, .015, count);
        private readonly CommodityIndexWindow _slow = new(MovingAvgType.SimpleMovingAverage, slow, .015, count);
        public void Reset() { _fast.Reset(); _slow.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var price = CommodityIndexWindow.TypicalPrice(bar.High, bar.Low, bar.Close);
            var fastValue = _fast.Next(price, true); var slowValue = _slow.Next(price, true);
            outputs[0] = fastValue; outputs[1] = slowValue; outputs[2] = fastValue - slowValue;
        }
        public void Dispose() { Reset(); _fast.Dispose(); _slow.Dispose(); }
    }

    private sealed class GeneratedInverseCciState(int length, int signal, double constant, int count) : IIndicatorState, IDisposable
    {
        private readonly CommodityIndexWindow _cci = new(MovingAvgType.WeightedMovingAverage, length, constant, count);
        private readonly StrengthAverage _signal = new(MovingAvgType.WeightedMovingAverage, signal, count);
        public void Reset() { _cci.Reset(); _signal.Reset(); }
        public double Update(in Bar bar)
        {
            var value = _cci.Next(CommodityIndexWindow.TypicalPrice(bar.High, bar.Low, bar.Close), true);
            var scaled = .1 * (value - 50);
            return Math.Tanh(_signal.Next(new StrengthValue(scaled), true).Mantissa);
        }
        public void Dispose() { Reset(); _cci.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedStandardizedScoreState(int length, int count, bool fast, bool inverse) : IIndicatorState, IDisposable
    {
        private readonly StrengthAverage _mean = new(MovingAvgType.SimpleMovingAverage, length, count);
        private readonly StandardizedScoreWindow _score = new(length, fast, observedHistory: true);
        public void Reset() { _mean.Reset(); _score.Reset(); }
        public double Update(in Bar bar)
        {
            var average = _mean.Next(new StrengthValue(bar.Close), true).Mantissa;
            var score = _score.Next(bar.Close, average, true, true);
            return inverse ? StandardizedScoreWindow.Inverse(score, fast) : score;
        }
        public void Dispose() { Reset(); _mean.Dispose(); _score.Dispose(); }
    }

    private sealed class GeneratedZDistanceState(int length) : IIndicatorState, IDisposable
    {
        private readonly ZDistanceWindow _window = new(MovingAvgType.VolumeWeightedAveragePrice, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Volume, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedWamiState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly WamiWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, 4, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedLaggedDifferenceState(int period, int count, bool volume) : IIndicatorState
    {
        private readonly double[] _lag = new double[Math.Min(period, Math.Max(1, count))];
        private int _position, _seen;
        public void Reset() { _position = _seen = 0; }
        public double Update(in Bar bar)
        {
            double value = volume ? bar.Volume : bar.Close;
            double result = _seen < period ? 0 : value - _lag[_position];
            _lag[_position] = value;
            if (++_position == _lag.Length) _position = 0;
            if (_seen < period) _seen++;
            return result;
        }
    }

    private sealed class GeneratedVolumeZoneState(int length) : IIndicatorState
    {
        private readonly Streaming.EmaState _signed = new(length), _total = new(length);
        private double _previous;
        private bool _hasPrevious;
        public void Reset() { _signed.Reset(); _total.Reset(); _previous = 0; _hasPrevious = false; }
        public double Update(in Bar bar)
        {
            var signed = _hasPrevious ? (bar.Close > _previous ? bar.Volume : -bar.Volume) : 0;
            var result = RoundedMomentumRatio.Of(_signed.GetNext(signed, true), _total.GetNext(bar.Volume, true));
            _previous = bar.Close; _hasPrevious = true;
            return result;
        }
    }

    private sealed class GeneratedVolumeMomentumOscillatorState(int shortLength, int longLength) : IIndicatorState
    {
        private double _short, _long;
        private bool _hasPrevious;
        public void Reset() { _short = _long = 0; _hasPrevious = false; }
        public double Update(in Bar bar)
        {
            if (!_hasPrevious) { _short = _long = bar.Volume; _hasPrevious = true; return 0; }
            _short = RoundedSeededEma.Next(bar.Volume, _short, shortLength);
            _long = RoundedSeededEma.Next(bar.Volume, _long, longLength);
            return RoundedPercentageChange.Of(_short, _long);
        }
    }

    private sealed class GeneratedValueChartState(int length, bool customRange) : IMultiOutputState, IDisposable
    {
        private readonly ValueChartWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) =>
            _window.Next(bar.Open, bar.High, bar.Low, bar.Close, outputs, false, true, customRange);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedUltimateMomentumState(UltimateMomentumIndicator indicator) : IIndicatorState, IDisposable
    {
        private readonly UltimateMomentumWindow _window = new(MovingAvgType.SimpleMovingAverage,
            indicator.Length1, indicator.Length2, indicator.Length3, indicator.Length4, indicator.Length5, indicator.StdDevMult);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close,
            Streaming.RollingMoneyFlowIndex.TypicalPrice(bar.High, bar.Low, bar.Close), bar.Volume, true).Value;
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedUltimateTraderState : IMultiOutputState, IDisposable
    {
        // The typed facade preserves the legacy fixed lookback/range/smoothing periods;
        // its Length parameter affects warmup, not these calculation stages.
        private readonly UltimateTraderWindow _window = new(5, 2);
        private readonly UltimateTraderWindow.Average _first = new(MovingAvgType.WeightedMovingAverage, 5);
        private readonly UltimateTraderWindow.Average _second = new(MovingAvgType.WeightedMovingAverage, 4);
        private readonly UltimateTraderWindow.Average _signal = new(MovingAvgType.WeightedMovingAverage, 4);
        public void Reset() { _window.Reset(); _first.Reset(); _second.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var raw = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
            var line = _second.Next(_first.Next(raw, true), true);
            outputs[0] = line; outputs[1] = _signal.Next(line, true);
        }
        public void Dispose() { Reset(); _first.Dispose(); _second.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedUltimateVolatilityState(int length) : IIndicatorState, IDisposable
    {
        private readonly UltimateVolatilityWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Open, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedSmoothedWilliamsRState(int length, int smoothLength) : IIndicatorState, IDisposable
    {
        private readonly SmoothedWilliamsWindow _window = new(length, smoothLength);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedEnhancedWilliamsRState(int length, int signalLength) : IMultiOutputState, IDisposable
    {
        private readonly EnhancedWilliamsWindow _window = new(MovingAvgType.SimpleMovingAverage, length, signalLength);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = point.Line; outputs[1] = point.SignalLine;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedVerticalAverageState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly VerticalHorizontalAverageWindow _window = new(length, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedVerticalHorizontalState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly VerticalHorizontalWindow _window = new(length, Math.Max(1, count));
        private readonly RocBankAverage _signal = new(MovingAvgType.WeightedMovingAverage, 6, Math.Max(1, count));
        public void Reset() { _window.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value;
            outputs[1] = _signal.Next(new RocBankValue(value), true).Publish();
        }
        public void Dispose() { _window.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedTrigonometricState(int period) : IMultiOutputState, IDisposable
    {
        private readonly TrigonometricWindow _window = new(period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => outputs[0] = _window.Next(bar.Close, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedForecastState(int period) : IMultiOutputState, IDisposable
    {
        private readonly TimeSeriesForecastWindow _window = new(period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Upper; outputs[1] = point.Middle; outputs[2] = point.Lower;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedTrendStepState(int period) : IMultiOutputState, IDisposable
    {
        private readonly TrendStepWindow _window = new(period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => outputs[0] = _window.Next(bar.Close, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedTechnicalRankState(TechnicalRank indicator) : IMultiOutputState, IDisposable
    {
        private readonly TechnicalRankWindow _window = new(indicator.Length1, indicator.Length2, indicator.Length3,
            indicator.Length4, indicator.Length5, indicator.Length6, indicator.Length7, indicator.Length8, indicator.Length9);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => outputs[0] = _window.Next(bar.Close, true).Value;
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedTironeState(int period) : IMultiOutputState, IDisposable
    {
        private readonly TironeWindow _window = new(period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => _window.Next(bar.High, bar.Low, bar.Close, true, outputs);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedVortexState(int period, bool positive) : IMultiOutputState, IDisposable
    {
        private readonly VortexWindow _window = new(period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = positive ? point.Plus : point.Minus;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedMoneyFlowPercentState(int period, bool twiggs) : IMultiOutputState, IDisposable
    {
        private readonly MoneyFlowPercentWindow _window = new(period, twiggs ? MovingAvgType.ExponentialMovingAverage : null);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) =>
            outputs[0] = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedTraderPressureState(int length1, int length2, int smoothLength) : IMultiOutputState, IDisposable
    {
        private readonly TraderPressureWindow _window = new(MovingAvgType.WeightedMovingAverage, length1, length2, smoothLength);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, true);
            outputs[0] = point.Net;
            outputs[1] = point.Bulls;
            outputs[2] = point.Bears;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedTimeMoneyState(int length1, int length2) : IMultiOutputState, IDisposable
    {
        private readonly TimeMoneyWindow _window = new(MovingAvgType.SimpleMovingAverage, length1, length2);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => _window.Next(bar.Close, true, outputs);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedTradeVolumeState(int period) : IMultiOutputState, IDisposable
    {
        private readonly TradeVolumeWindow _window = new(MovingAvgType.SimpleMovingAverage, period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = point.Line;
            outputs[1] = point.Signal;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedBuffState(int period) : IMultiOutputState, IDisposable
    {
        private readonly BuffWindow _fast = new(period), _slow = new(20);
        public void Reset() { _fast.Reset(); _slow.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs[0] = _fast.Next(bar.Close, bar.Volume, true);
            outputs[1] = _slow.Next(bar.Close, bar.Volume, true);
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedEquityState(int period) : IIndicatorState, IDisposable
    {
        private readonly EquityWindow _window = new(MovingAvgType.SimpleMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedVolumeWeightedState(int period) : IIndicatorState, IDisposable
    {
        private readonly VolumeWeightedWindow _window = new(MovingAvgType.SimpleMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Volume, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedBartlettVolumeState(int period) : IIndicatorState, IDisposable
    {
        private readonly BartlettVolumeWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Volume, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVolumeAdjustedState(int period, double factor) : IIndicatorState, IDisposable
    {
        private readonly VolumeAdjustedWindow _window = new(MovingAvgType.SimpleMovingAverage, period, factor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Volume, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedBryantState(int period) : IIndicatorState, IDisposable
    {
        private readonly BryantWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedVariableAdaptiveState(int period) : IIndicatorState, IDisposable
    {
        private readonly VariableAdaptiveWindow _window = new(MovingAvgType.SimpleMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.NextValidated(bar.Close, bar.Open, bar.High, bar.Low, true, includeSignals: false).Value;
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedAdaptiveZoneState(int period, double pct, int count) : IMultiOutputState, IDisposable
    {
        private readonly AdaptiveZoneWindow _window = new(MovingAvgType.ExponentialMovingAverage, period, pct, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.High, bar.Low, true);
            outputs[0] = point.Upper;
            outputs[1] = point.Middle;
            outputs[2] = point.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAdaptiveEmaState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly AdaptiveEmaWindow _window = new(MovingAvgType.SimpleMovingAverage, period, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAutonomousState(int period) : IIndicatorState, IDisposable
    {
        private readonly AutonomousRecursiveWindow _window = new(period, 7, 3);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedAdaptiveAutonomousState(int period, double lambda) : IMultiOutputState, IDisposable
    {
        private readonly AdaptiveAutonomousWindow _window = new(period, lambda);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Deviation;
            outputs[1] = point.Average;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAdaptiveAutonomousStopState(int period, double lambda) : IIndicatorState, IDisposable
    {
        private readonly AdaptiveAutonomousWindow _window = new(period, lambda);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Stop;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAhrensState(int period) : IIndicatorState, IDisposable
    {
        private readonly AhrensWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAlphaDecreasingState : IIndicatorState
    {
        private readonly AlphaDecreasingWindow _window = new();
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedAtrFilteredState(int period, int atrPeriod, int deviationPeriod, int lookback, double cap) : IIndicatorState, IDisposable
    {
        private readonly AtrFilterWindow _window = new(MovingAvgType.SimpleMovingAverage, period, atrPeriod, deviationPeriod, lookback, cap);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedR2FitState(int period) : IIndicatorState, IDisposable
    {
        private readonly R2AdaptiveWindow _window = new(MovingAvgType.SimpleMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedKaufmanFitState(int period) : IIndicatorState, IDisposable
    {
        private readonly Streaming.KaufmanRegressionMoments _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true, out _, out _, out _);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedKaufmanCorrelationState(int period) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.KaufmanRegressionMoments _window = new(period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) =>
            _window.Next(bar.Close, true, out outputs[0], out outputs[1], out outputs[2]);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRegressionResidualState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly ExactLinearFitWindow _window = new(period, observedHistory: Math.Max(1, period) > count, compact: true);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).PercentFitResidual(bar.Close);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedOneLcState(int period, MovingAvgType kind) : IIndicatorState, IDisposable
    {
        private readonly OneLcWindow _window = new(kind, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAdaptiveRangeFitState(int period) : IIndicatorState, IDisposable
    {
        private readonly AdaptiveFitWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedPolynomialCellState(int period) : IIndicatorState
    {
        private readonly PolynomialCellWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedQuadraticProjectionState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly QuadraticProjectionWindow? _window = count == 0 ? null : new(MovingAvgType.SimpleMovingAverage, period, count);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window!.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedQuadraticFitState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.QuadraticLeastSquaresWindow? _window = Math.Max(1, period) > count ? null : new(period);
        public void Reset() => _window?.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var fit = _window?.Next(bar.Close, 14, true) ?? default;
            outputs[0] = fit.Value;
            outputs[1] = fit.Forecast;
        }
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedFisherLsmaState(int period) : IIndicatorState, IDisposable
    {
        private readonly FisherLeastSquaresWindow _window = new(MovingAvgType.SimpleMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Line;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTStepLsmaState(int period) : IIndicatorState, IDisposable
    {
        private readonly TStepLeastSquaresWindow _window = new(MovingAvgType.SimpleMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Line;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSimplifiedLsmaState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly SimplifiedLeastSquaresWindow? _window = count == 0 ? null : new(period, count);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window!.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedLsmaState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly WmaValueState _weighted = new(Math.Max(1, period), count);
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _simple = Math.Max(1, period) > count ? null : new(period);
        public void Reset() { _weighted.Reset(); _simple?.Reset(); }
        public double Update(in Bar bar) => LeastSquaresAverage.Combine(_weighted.Update(in bar), _simple?.Next(bar.Close, true) ?? 0);
        public void Dispose() => _simple?.Dispose();
    }

    private sealed class GeneratedRegressionOutputsState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly ExactLinearFitWindow _window = new(period, observedHistory: Math.Max(1, period) > count, compact: true);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var fit = _window.Next(bar.Close, true);
            outputs[0] = fit.Last;
            outputs[1] = fit.Next;
            outputs[2] = fit.Slope;
            outputs[3] = fit.GlobalIntercept;
        }
        public void Dispose() => _window.Dispose();
    }

    private enum RegressionReading { Slope, GlobalIntercept, Line }

    private sealed class GeneratedRegressionState(int period, int count, RegressionReading reading) : IIndicatorState, IDisposable
    {
        private readonly ExactLinearFitWindow _window = new(period, observedHistory: Math.Max(1, period) > count, compact: true);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar)
        {
            var fit = _window.Next(bar.Close, true);
            return reading switch
            {
                RegressionReading.Slope => fit.Slope,
                RegressionReading.GlobalIntercept => fit.GlobalIntercept,
                _ => fit.Count < Math.Max(1, period) ? 0 : fit.Last
            };
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAlmaState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly AlmaWindowMean? _window = count == 0 ? null : new(period, .85, 6, count);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window!.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedJsaState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.JsaMovingAverageSmoother? _window = Math.Max(1, period) >= count ? null : new(period);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window is null ? PriceMean.Of(bar.Close, 0) : _window.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedSelfWeightedState(int period) : IIndicatorState, IDisposable
    {
        private readonly SelfWeightedWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSymmetricMeanState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly SymmetricWindowMean _window = new(period, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTriangleIndicatorState(int period) : IMultiOutputState, IDisposable
    {
        private readonly TriangleIndicatorWindow _window = new(MovingAvgType.EhlersTriangleMovingAverage, period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Open, bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Roc;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedHannMeanState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly HannWindowMean? _window = count == 0 ? null : new(period, count);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window!.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedHammingMeanState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly HammingWindowMean? _window = count == 0 ? null : new(period, 3, count);
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window!.Next(bar.Close, true);
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedHannIndicatorState(int period) : IMultiOutputState, IDisposable
    {
        private readonly HannIndicatorWindow _window = new(MovingAvgType.EhlersHannMovingAverage, period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Open, bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Roc;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedHammingIndicatorState(int period) : IMultiOutputState, IDisposable
    {
        private readonly HammingIndicatorWindow _window = new(MovingAvgType.EhlersHammingMovingAverage, period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Open, bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Roc;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedEarlyOnsetState(int low, int high, double k) : IIndicatorState
    {
        private readonly EarlyOnsetWindow _window = new(low, high, k);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedImpulseReactionState(int lag, int period, double q) : IIndicatorState
    {
        private readonly ImpulseReactionWindow _window = new(lag, period, q);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedImpulseResponseState(int period, double bandwidth) : IIndicatorState, IDisposable
    {
        private readonly ImpulseResponseWindow _window = new(MovingAvgType.EhlersHannMovingAverage, period, bandwidth);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedBandPassV1State(int period, double bandwidth) : IMultiOutputState
    {
        private readonly ClampedBandPassWindow _window = new(period, bandwidth, 0);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Value;
            outputs[1] = value.Signal;
        }
    }

    private sealed class GeneratedBandPassState(int period, double bandwidth, int variant) : IIndicatorState
    {
        private readonly ClampedBandPassWindow _window = new(period, bandwidth, variant);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
    }

    private sealed class GeneratedSuperPassbandState(int fast, int slow, int numerator, int period) : IMultiOutputState
    {
        private readonly SuperPassbandWindow _window = new(fast, slow, numerator, period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Upper;
            outputs[2] = value.Lower;
        }
    }

    private sealed class GeneratedCyberCycleState(double alpha) : IIndicatorState
    {
        private readonly CyberCycleWindow _window = new(alpha);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedInstantaneousTrendState(double alpha) : IMultiOutputState
    {
        private readonly InstantaneousTrendWindow _window = new(alpha);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
    }

    private sealed class GeneratedGaussianState : IMultiOutputState
    {
        private readonly double[] _gains;
        private readonly double[][] _stages = [new double[1], new double[2], new double[3], new double[4]];
        internal GeneratedGaussianState(int period) => _gains =
            [EhlersGaussian.Gain(period, 1), EhlersGaussian.Gain(period, 2), EhlersGaussian.Gain(period, 3), EhlersGaussian.Gain(period, 4)];
        public void Reset() { foreach (var stages in _stages) Array.Clear(stages, 0, stages.Length); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            for (int i = 0; i < 4; i++) outputs[i] = EhlersGaussian.Next(bar.Close, _gains[i], _stages[i], true);
        }
    }

    private sealed class GeneratedRoofingV1State(int high, int low) : IIndicatorState, IDisposable
    {
        private readonly Streaming.EhlersRoofingV1Window _window = new(MovingAvgType.Ehlers2PoleSuperSmootherFilterV1, high, low);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRoofingV2State(int high, int low, bool original) : IIndicatorState
    {
        private readonly Streaming.EhlersRoofingFilterV2Kernel _window = new(high, low, original);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedHpLpRoofingState(int high, int low, bool zero) : IIndicatorState
    {
        private readonly HpLpRoofingWindow _window = new(high, low);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar)
        {
            var value = _window.Next(bar.Close, true);
            return zero ? value.Zero : value.Roof;
        }
    }

    private sealed class GeneratedDetrendedLeadingState(int period) : IMultiOutputState
    {
        private readonly DetrendedLeadingWindow _window = new(period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, true);
            outputs[0] = value.Dsp;
            outputs[1] = value.Deli;
        }
    }

    private sealed class GeneratedHighPassV2State(int period) : IIndicatorState, IDisposable
    {
        private readonly HighPassV2Window _window = new(MovingAvgType.WeightedMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Publish();
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDecyclerV2State(int fast, int slow) : IIndicatorState, IDisposable
    {
        private readonly DecyclerV2Window _window = new(MovingAvgType.WeightedMovingAverage, fast, slow);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDecyclerOscillatorState(int period) : IMultiOutputState
    {
        private readonly DecyclerOscillatorWindow _fast = new(period);
        private readonly DecyclerOscillatorWindow _slow = new(period, 1, 2);
        public void Reset() { _fast.Reset(); _slow.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs[0] = _fast.Next(bar.Close, true);
            outputs[1] = _slow.Next(bar.Close, true);
        }
    }

    private sealed class GeneratedDecyclerState(int period) : IIndicatorState
    {
        private readonly DecyclerWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedHighPassState(int period) : IIndicatorState
    {
        private readonly HighPassWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedLeadingState : IIndicatorState
    {
        private readonly LeadingWindow _window = new(.25, .33);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedSimpleDecyclerState(int period) : IMultiOutputState
    {
        private readonly SimpleDecyclerWindow _window = new(period, .5, .5);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Upper;
            outputs[1] = value.Middle;
            outputs[2] = value.Lower;
        }
    }

    private sealed class GeneratedQuickState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly QuickWindowMean _window = new(period, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedMcNichollState(int period) : IIndicatorState, IDisposable
    {
        private readonly McNichollWindow _window = new(MovingAvgType.ExponentialMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedWellRoundedState(int period) : IIndicatorState
    {
        private readonly WellRoundedWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedWilderSumState(int period) : IIndicatorState
    {
        private readonly WilderSummationWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedZeroLowLagState(int period) : IIndicatorState, IDisposable
    {
        private readonly ZeroLowLagWindow _window = new(period, 1.4);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedZeroLagTripleState(int period) : IIndicatorState, IDisposable
    {
        private readonly ZeroLagTripleWindow _window = new(MovingAvgType.TripleExponentialMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSlowSmoothedState : IIndicatorState
    {
        private readonly WmaValueState _first, _second, _third;
        internal GeneratedSlowSmoothedState(int period, int count)
        {
            // Empty legacy runs never evaluate the potentially overflowing split.
            if (count == 0) period = 1;
            int second = Math.Max(1, Math.Min(530, (int)Math.Ceiling(period / 3d)));
            int first = Math.Max(1, Math.Min(530, (int)Math.Ceiling((double)(period - second) / 2)));
            int third = Math.Max(1, Math.Min(530, (int)Math.Floor((double)(period - second) / 2)));
            _first = new(first, count); _second = new(second, count); _third = new(third, count);
        }
        public void Reset() { _first.Reset(); _second.Reset(); _third.Reset(); }
        public double Update(in Bar bar)
        {
            double first = _first.Update(bar);
            double second = _second.Update(new Bar(default, 0, 0, 0, first, 0));
            return _third.Update(new Bar(default, 0, 0, 0, second, 0));
        }
    }

    private sealed class GeneratedEhlersZeroLagState(int period) : IIndicatorState, IDisposable
    {
        private readonly EhlersZeroLagWindow _window = new(MovingAvgType.ExponentialMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTrueStrengthState(int[] lengths, int signal, int count) : IMultiOutputState, IDisposable
    {
        private readonly StrengthWindow _line = new(MovingAvgType.ExponentialMovingAverage, lengths, count);
        private readonly StrengthAverage _signal = new(MovingAvgType.ExponentialMovingAverage, signal, count);
        public void Reset() { _line.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = _line.Next(bar.Close, true);
            outputs[0] = value;
            outputs[1] = _signal.Next(new StrengthValue(value), true).Mantissa;
        }
        public void Dispose() { _line.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedKlingerState(int fast, int slow, int signal) : IMultiOutputState, IDisposable
    {
        private readonly KlingerWindow _window = new(MovingAvgType.ExponentialMovingAverage, fast, slow, signal);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true);
            outputs[0] = point.Line;
            outputs[1] = point.SignalLine;
            outputs[2] = point.Histogram;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedUltimateState(int first, int second, int third) : IIndicatorState
    {
        private readonly UltimatePressureWindow _window = new(first, second, third);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true);
    }

    // Both deviation batch facades follow SmaCpuKernel.ProcessGuarded.
    // The streaming SMA has different cancellation fallback rounding.
    private sealed class GeneratedStandardDeviationAliasState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RollingStandardDeviation? _line = period <= count ? new(period) : null;
        private readonly double[] _window = period > 1 && period <= count ? new double[period] : [];
        private int _seen, _next;
        private double _sum, _roundoff;
        private bool _exactRequired;

        public void Reset()
        {
            _line?.Reset();
            Array.Clear(_window, 0, _window.Length);
            _seen = _next = 0;
            _sum = _roundoff = 0;
            _exactRequired = false;
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = _line?.Next(bar.Close, true) ?? 0;
            outputs[0] = value;
            if (_window.Length == 0) { outputs[1] = period == 1 ? value : 0; return; }

            double expired = _window[_next];
            _window[_next] = value;
            if (++_next == period) _next = 0;
            double previous = _sum;
            _sum += value;
            _roundoff = MeanRoundoff.AfterAddition(_roundoff, _sum);
            _exactRequired |= ExactMeanAccumulator.SevereCancellation(previous, value, _sum);
            if (_seen >= period)
            {
                previous = _sum;
                _sum -= expired;
                _roundoff = MeanRoundoff.AfterAddition(_roundoff, _sum);
                _exactRequired |= ExactMeanAccumulator.SevereCancellation(previous, -expired, _sum);
                if (Math.Abs(_sum) <= 1e-4 * Math.Max(Math.Abs(expired), Math.Abs(value)))
                    Rebuild(false);
            }
            _seen++;
            double mean = _seen >= period ? _sum / period : 0;
            if (_seen >= period && (_exactRequired || MeanRoundoff.RequiresExact(_sum, period, _roundoff)))
            {
                var exact = new ExactMeanAccumulator();
                int index = _next;
                for (int j = 0; j < period; j++)
                {
                    exact.Add(_window[index]);
                    if (++index == period) index = 0;
                }
                mean = exact.Mean(period);
            }
            outputs[1] = mean;
            if (_seen % period == 0) Rebuild(true);
        }

        private void Rebuild(bool periodic)
        {
            _sum = _roundoff = 0;
            // Cancellation rebuilds preserve the flag; only periodic rebuilds reset it.
            if (periodic) _exactRequired = false;
            int index = _next;
            for (int j = 0; j < period; j++)
            {
                double previous = _sum, value = _window[index];
                _sum += value;
                _roundoff = MeanRoundoff.AfterAddition(_roundoff, _sum);
                if (periodic) _exactRequired |= ExactMeanAccumulator.SevereCancellation(previous, value, _sum);
                if (++index == period) index = 0;
            }
        }

        public void Dispose() => _line?.Dispose();
    }


    private sealed class GeneratedOrderedDeviationState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RollingStandardDeviation? _line = period <= count ? new(period) : null;
        private readonly Streaming.SimpleMovingAverageSmoother? _signal = period <= count ? new(period) : null;
        public void Reset() { _line?.Reset(); _signal?.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = _line?.Next(bar.Close, true) ?? 0;
            outputs[0] = value;
            outputs[1] = _signal?.Next(value, true) ?? 0;
        }
        public void Dispose() { _line?.Dispose(); _signal?.Dispose(); }
    }

    private sealed class GeneratedStatisticalVolatilityState(int length, int annual) : IMultiOutputState, IDisposable
    {
        private readonly StatisticalVolatilityWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, annual);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVolatilityQualityState(int fast, int slow) : IMultiOutputState, IDisposable
    {
        private readonly VolatilityQualityWindow _window = new(MovingAvgType.SimpleMovingAverage, fast, slow);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.Fast; outputs[2] = point.Slow;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedVolatilityStopState(int length, double multiplier) : IIndicatorState, IDisposable
    {
        private readonly VolatilityStopWindow _window = new(length, multiplier);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedWilderVolatilityState(int trend, int range, double factor) : IIndicatorState, IDisposable
    {
        private readonly WilderVolatilityWindow _window = new(MovingAvgType.ExponentialMovingAverage, trend, range, factor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRelativeVolatilityState(int length, int smooth, bool paired, int count) : IIndicatorState, IDisposable
    {
        private readonly RelativeVolatilityWindow? _first = Math.Max(1, length) <= count ? new(MovingAvgType.WildersSmoothingMethod, length, smooth, capacityHint: count) : null;
        private readonly RelativeVolatilityWindow? _second = paired && Math.Max(1, length) <= count ? new(MovingAvgType.WildersSmoothingMethod, length, smooth, capacityHint: count) : null;
        public void Reset() { _first?.Reset(); _second?.Reset(); }
        public double Update(in Bar bar)
        {
            var first = _first?.Next(paired ? bar.High : bar.Close, true) ?? 100;
            return paired ? Helpers.RelativeVolatilityWindow.Mean(first, _second?.Next(bar.Low, true) ?? 100) : first;
        }
        public void Dispose() { _first?.Dispose(); _second?.Dispose(); }
    }

    private sealed class GeneratedSeededRelativeVolatilityState(bool high, int length, int deviation, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.RelativeVolatilityIndexCore? _window = length < count && deviation <= count ? new(length, deviation) : null;
        private int _seen;
        public void Reset() { _window?.Reset(); _seen = 0; }
        public double Update(in Bar bar)
        {
            if (_window is not null) return _window.Next(high ? bar.High : bar.Low, true);
            // No deviation can appear, or the seed cannot finish within this owned input.
            return _seen++ < length ? 0 : 50;
        }
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedVolatilityIndexState(int length, double alpha1, double alpha2) : IMultiOutputState, IDisposable
    {
        private readonly VolatilityIndexWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, alpha1, alpha2);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.First;
            outputs[1] = point.Second;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    // The legacy typed facade ignores Alpha and fixes the smoothing period at 12.
    private sealed class GeneratedRecursiveMedianState(int length) : IIndicatorState
    {
        private readonly RecursiveMedianWindow _window = new(length, 12);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedRecursiveMedianOscillatorState(int length1, int length2, int length3) : IIndicatorState
    {
        private readonly RecursiveMedianOscillatorWindow _window = new(length1, length2, length3);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedMedianAdaptiveState(int length, double threshold) : IIndicatorState
    {
        private readonly MedianAdaptiveWindow _window = new(length, threshold);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
    }

    private sealed class GeneratedAllPassPhaseState(int length) : IIndicatorState, IDisposable
    {
        private readonly AllPassPhaseWindow _window = new(length, .5);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedCenterGravityState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly CenterGravityWindow _window = new(length, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChebyshevWaveState : IMultiOutputState, IDisposable
    {
        private readonly ChebyshevWaveWindow[] _windows = Enumerable.Range(0, 9).Select(wave => new ChebyshevWaveWindow(wave)).ToArray();
        public void Reset() { foreach (var window in _windows) window.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            for (var wave = 0; wave < _windows.Length; wave++) outputs[wave] = _windows[wave].Next(bar.Close, true);
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedEhlersDerivState(int length, int signal) : IMultiOutputState, IDisposable
    {
        private readonly EhlersDerivWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, signal);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedEhlersClipState(int lag, int length, int signal) : IMultiOutputState, IDisposable
    {
        private readonly EhlersClipWindow _window = new(MovingAvgType.ExponentialMovingAverage, lag, length, signal);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedEhlersSimpleWindowState(int length) : IMultiOutputState, IDisposable
    {
        private readonly EhlersSimpleWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Open, bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Roc;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTripleDelayState(int length) : IMultiOutputState, IDisposable
    {
        private readonly TripleDelayWindow _window = new(MovingAvgType.EhlersModifiedOptimumEllipticFilter, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedStochasticGravityState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly StochasticGravityWindow _window = new(length, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRoofingStochasticState(int length) : IIndicatorState, IDisposable
    {
        private readonly RoofingStochasticWindow _window = new(MovingAvgType.Ehlers2PoleSuperSmootherFilterV1, 48, 10, length, false);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedStochasticCyberState(int length, double alpha) : IMultiOutputState, IDisposable
    {
        private readonly StochasticCyberWindow _window = new(length, alpha);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedEhlersVigorState(int length, int signal, int count) : IMultiOutputState, IDisposable
    {
        private readonly EhlersVigorWindow _window = new(MovingAvgType.SimpleMovingAverage, length, signal, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, bar.Open, bar.High, bar.Low, true);
            outputs[0] = value.Value;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedModifiedRsiState(int upper, int lower, int length) : IMultiOutputState, IDisposable
    {
        private readonly ModifiedRsiWindow _window = new(upper, lower, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedModifiedStochasticState(int upper, int lower, int length) : IIndicatorState, IDisposable
    {
        private readonly RoofingStochasticWindow _window = new(MovingAvgType.Ehlers2PoleSuperSmootherFilterV1, upper, lower, length, true);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRocketRsiState(int length, int smooth, double multiplier) : IIndicatorState, IDisposable
    {
        private readonly RocketRsiWindow _window = new(MovingAvgType.Ehlers2PoleSuperSmootherFilterV2, length, smooth, multiplier);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAverageDifferenceState(int fast, int slow, int count) : IIndicatorState, IDisposable
    {
        private readonly AverageGapWindow _window = new(MovingAvgType.WeightedMovingAverage, fast, slow, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedFramaState(int length) : IIndicatorState, IDisposable
    {
        private readonly FramaWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedFisherState(int length) : IIndicatorState
    {
        private RollingMinMax _range = new(length);
        private double _normalized, _previous;
        public void Reset() { _range = new(length); _normalized = _previous = 0; }
        public double Update(in Bar bar)
        {
            _range.Add(bar.Close);
            var ratio = FisherArithmetic.Position(bar.Close, _range.Min, _range.Max);
            var value = (0.33 * 2 * (ratio - 0.5)) + (0.67 * _normalized);
            _normalized = Math.Max(-0.999, Math.Min(0.999, value));
            _previous = FisherArithmetic.Transform(_normalized) + (0.5 * _previous);
            return _previous;
        }
    }

    private sealed class GeneratedInverseRsiFisherState(int length, int smooth, int count) : IIndicatorState, IDisposable
    {
        private readonly PriceRsiWindow _rsi = new(MovingAvgType.WeightedMovingAverage, length, count);
        private readonly StrengthAverage _average = new(MovingAvgType.WeightedMovingAverage, smooth, count);
        public void Reset() { _rsi.Reset(); _average.Reset(); }
        public double Update(in Bar bar)
        {
            var value = .1 * (_rsi.Next(bar.Close, true) - 50);
            return Math.Tanh(_average.Next(new StrengthValue(value), true).Mantissa);
        }
        public void Dispose() { _rsi.Dispose(); _average.Dispose(); }
    }

    private enum CorrelationKind { Trend, Cycle, Angle, MarketState }

    private sealed class GeneratedCorrelationState(int length, CorrelationKind kind) : IMultiOutputState, IDisposable
    {
        private readonly EhlersCorrelationWindow _window = new(length, kind == CorrelationKind.Trend);
        private double _previousAngle;
        public void Reset() { _window.Reset(); _previousAngle = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            if (kind == CorrelationKind.Trend) { outputs[0] = value.Real; return; }
            if (kind == CorrelationKind.Cycle) { outputs[0] = value.Real; outputs[1] = value.Imag; return; }
            var angle = EhlersCorrelationPhase.Angle(value.Real, value.Imag);
            angle = _previousAngle - angle < 270 && angle < _previousAngle ? _previousAngle : angle;
            outputs[0] = kind == CorrelationKind.MarketState ? EhlersCorrelationPhase.MarketState(angle, _previousAngle) : angle;
            _previousAngle = angle;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedHurstCoefficientState(int length, int smooth) : IIndicatorState, IDisposable
    {
        private readonly HurstCoefficientWindow _window = new(length, smooth);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedNoiseEliminationState(int length) : IIndicatorState, IDisposable
    {
        private readonly Streaming.NoiseEliminationTechnologyEngine _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChronologicalSpearmanState(int length) : IIndicatorState, IDisposable
    {
        private readonly int _length = Math.Max(1, length);
        private readonly PooledRingBuffer<double> _values = new(Math.Max(1, length));
        private readonly double[] _prices = new double[Math.Max(1, length)], _positions = new double[Math.Max(1, length)];
        public void Reset() => _values.Clear();
        public double Update(in Bar bar)
        {
            for (var j = 0; j < _length; j++)
                _prices[j] = Streaming.EhlersStreamingWindow.GetOffsetValue(_values, bar.Close, _length - 1 - j);
            var result = ChronologicalSpearman.Compute(_prices, _positions);
            _values.TryAdd(bar.Close, out _);
            return result;
        }
        public void Dispose() => _values.Dispose();
    }

    private sealed class GeneratedClassicHilbertState(int upper, int lower) : IMultiOutputState, IDisposable
    {
        private readonly ClassicHilbertWindow _window = new(upper, lower);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Real;
            outputs[1] = point.Imaginary;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedHilbertTransformerState(int upper, int lower, int smoothing, bool smooth) : IMultiOutputState, IDisposable
    {
        private readonly HilbertTransformerWindow _window = new(upper, lower, smoothing, smooth);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Real;
            outputs[1] = point.Imaginary;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedHilbertPhaseState(int lag, double real, double imaginary) : IMultiOutputState, IDisposable
    {
        private readonly HilbertPhaseWindow _window = new(lag, real, imaginary, 1, false);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Imaginary;
            outputs[1] = point.Real;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedInstantaneousPhaseState(int lag, int horizon) : IIndicatorState, IDisposable
    {
        private readonly HilbertPhaseWindow _window = new(lag, .635, .338, horizon, true);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Cycle;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedMamaDerivedState(int mode) : IMultiOutputState, IDisposable
    {
        private readonly MamaDerivedWindow _window = new(mode);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.First;
            outputs[1] = point.Second;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedCyberSineState(int length, double alpha) : IMultiOutputState, IDisposable
    {
        private readonly CyberSineWindow _window = new(length, alpha);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Sine;
            outputs[1] = point.Lead;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedFollowingMamaState : IIndicatorState, IDisposable
    {
        private readonly MamaWindow _window = new(.5, .05);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Values.Fama;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedMamaOutputsState(double fast, double slow) : IMultiOutputState, IDisposable
    {
        private readonly MamaWindow _window = new(fast, slow);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true).Values;
            outputs[0] = point.Fama;
            outputs[1] = point.Mama;
            outputs[2] = point.I1;
            outputs[3] = point.Q1;
            outputs[4] = point.SmoothPeriod;
            outputs[5] = point.Smooth;
            outputs[6] = point.Real;
            outputs[7] = point.Imag;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedHilbertCycleState(int upper, int lower, int minimum, int horizon, int mode) : IIndicatorState, IDisposable
    {
        private readonly HilbertCycleWindow _window = new(upper, lower, minimum, horizon, mode);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedHilbertNoiseState(int length) : IIndicatorState, IDisposable
    {
        private readonly HilbertNoiseWindow _window = new(MovingAvgType.ExponentialMovingAverage, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedMamaNoiseState(int length, int mode) : IIndicatorState, IDisposable
    {
        private readonly MamaNoiseWindow _window = new(length, mode);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true).Snr;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedEnhancedMamaNoiseState(int length) : IMultiOutputState, IDisposable
    {
        private readonly MamaNoiseWindow _window = new(length, 2);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.High, bar.Low, true);
            outputs[0] = point.Snr;
            outputs[1] = point.InPhase;
            outputs[2] = point.Quadrature;
            outputs[3] = point.Period;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedCycleAmplitudeState(int length, double delta, int count) : IIndicatorState, IDisposable
    {
        private readonly CycleAmplitudeWindow _window = new(length, delta, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTrendExtractionState(int length, double delta) : IMultiOutputState, IDisposable
    {
        private readonly TrendExtractionWindow _window = new(MovingAvgType.SimpleMovingAverage, length, delta);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Trend;
            outputs[1] = point.Band;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedEmpiricalDecompositionState(int length, int extremaLength, double delta, double fraction) : IMultiOutputState, IDisposable
    {
        private readonly EmpiricalDecompositionWindow _window = new(MovingAvgType.SimpleMovingAverage, length, extremaLength, delta, fraction);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Trend;
            outputs[1] = point.Peak;
            outputs[2] = point.Valley;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedFourierPhaseState(int length) : IMultiOutputState, IDisposable
    {
        private readonly FourierPhaseWindow _window = new(length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Phase;
            outputs[1] = point.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedEhlersConvolutionState(int highLength, int lowLength, int correlationLength) : IMultiOutputState, IDisposable
    {
        private readonly EhlersConvolutionWindow _window = new(highLength, lowLength, correlationLength);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Value;
            outputs[1] = point.Slope;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedFourierSeriesState(int length, double bandwidth) : IMultiOutputState, IDisposable
    {
        private readonly FourierSeriesWindow _window = new(length, bandwidth);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Wave;
            outputs[1] = point.Roc;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDiscreteFourierState(int minimum, int maximum, int cutoff) : IIndicatorState, IDisposable
    {
        private readonly OoplesFinance.StockIndicators.Streaming.DiscreteFourierCycle _window = new(minimum, maximum, cutoff);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true, out _);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDftSpectrumState(int upper, int lower) : IIndicatorState, IDisposable
    {
        private readonly DftSpectrumWindow _window = new(upper, lower);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAutocorrelationReversalState(int length, int smoothing, int firstLag) : IIndicatorState, IDisposable
    {
        private readonly AutocorrelationReversalWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, smoothing, firstLag, true);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true, 0).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedCombSpectrumState(int upper, int lower, double bandwidth) : IIndicatorState, IDisposable
    {
        private readonly CombSpectrumWindow _window = new(upper, lower, bandwidth);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedTunedBypassState(int minimum, int maximum, int cutoff, int median) : IMultiOutputState, IDisposable
    {
        private readonly TunedBypassWindow _window = new(minimum, maximum, cutoff, median);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.First;
            outputs[1] = point.Second;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedMesaPredictV1State(int horizon, int order, int smooth, int window) : IMultiOutputState, IDisposable
    {
        private readonly OoplesFinance.StockIndicators.Streaming.MesaPredictionKernel _window = new(horizon, order, smooth, window);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Ssf;
            outputs[1] = point.Predict;
            outputs[2] = point.PrePredict;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedMesaPredictV2State(int history, int high, int smooth, int horizon) : IMultiOutputState, IDisposable
    {
        private readonly MesaPredictV2Window _window = new(MovingAvgType.EhlersHannMovingAverage, history, high, smooth, horizon);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Filter;
            outputs[1] = point.Predict;
            outputs[2] = point.Extrap;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAnticipateState(int length, double bandwidth) : IIndicatorState, IDisposable
    {
        private readonly AnticipateWindow _window = new(MovingAvgType.EhlersHannMovingAverage, length, bandwidth);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedFmDemodulatorState(int fast, int slow) : IIndicatorState
    {
        private readonly int _fast = Math.Max(1, fast);
        private readonly TwoPoleWindow _window = new(slow, 3);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar)
        {
            var difference = bar.Close - bar.Open;
            var scaled = _fast * difference;
            return _window.Next(Math.Max(-1, Math.Min(1, scaled)), true);
        }
    }

    private sealed class GeneratedSuperSmootherAliasState(int length) : IIndicatorState
    {
        private readonly OoplesFinance.StockIndicators.Streaming.EhlersSuperSmootherFilterEngine _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
    }

    private sealed class GeneratedEhlersVidyaState : IIndicatorState, IDisposable
    {
        private readonly EhlersVidyaWindow _window = new(MovingAvgType.WeightedMovingAverage, 9, 30);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSquelchState(int lag, int threshold, int horizon) : IIndicatorState, IDisposable
    {
        private readonly SquelchWindow _window = new(lag, threshold, horizon);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedSwissArmyState(int length, double delta) : IMultiOutputState, IDisposable
    {
        private readonly SwissArmyWindow _window = new(length, delta);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => _window.Next(bar.Close, true).Values.AsSpan().CopyTo(outputs);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSpectrumBankState(int minimum, int maximum, int cutoff, int median) : IIndicatorState, IDisposable
    {
        private readonly OoplesFinance.StockIndicators.Streaming.EhlersSpectrumDerivedFilterBankEngine _window = new(minimum, maximum, cutoff, median);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRestoringPullState(int minimum, int maximum, int cutoff, int median) : IMultiOutputState, IDisposable
    {
        private readonly OoplesFinance.StockIndicators.Streaming.EhlersSpectrumDerivedFilterBankEngine _window = new(minimum, maximum, cutoff, median);
        private readonly OoplesFinance.StockIndicators.Streaming.EmaState _signal = new(Math.Max(1, minimum));
        private bool _signalInvalid;
        public void Reset() { _window.Reset(); _signal.Reset(); _signalInvalid = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var cycle = _window.Next(bar.Close, true);
            var ratio = 2 * Math.PI / cycle;
            var pull = bar.Volume * (ratio * ratio);
            _signalInvalid |= !double.IsFinite(pull);
            outputs[0] = pull;
            outputs[1] = _signalInvalid ? double.NaN : _signal.GetNext(pull, commit: true);
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedKlingerSignalState(int fast, int slow, int signal) : IIndicatorState, IDisposable
    {
        private readonly KlingerWindow _window = new(MovingAvgType.ExponentialMovingAverage, fast, slow, signal);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true).SignalLine;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChandeSignalState(int period, int count, int signalLength) : IIndicatorState, IDisposable
    {
        private readonly GeneratedChandeState _state = new(period, count, signalLength);
        public void Reset() => _state.Reset();
        public double Update(in Bar bar)
        {
            Span<double> outputs = stackalloc double[2];
            _state.Update(in bar, outputs);
            return outputs[1];
        }
        public void Dispose() => _state.Dispose();
    }

    private sealed class GeneratedMacdAliasState(int fast, int slow, int signal, int slot) : IIndicatorState
    {
        private readonly GeneratedEmaGapSignalState _state = new(fast, slow, signal);
        public void Reset() => _state.Reset();
        public double Update(in Bar bar)
        {
            Span<double> outputs = stackalloc double[3];
            _state.Update(in bar, outputs);
            return outputs[slot];
        }
    }

    private sealed class GeneratedAdaptiveRangeState(int period) : IIndicatorState, IDisposable
    {
        private readonly AdaptiveRangeMeanWindow _window = new(2, 14, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAdaptivePriceRsiState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly PriceRsiWindow _rsi = new(MovingAvgType.WildersSmoothingMethod, period, count);
        private double _previous;
        public void Reset() { _rsi.Reset(); _previous = 0; }
        public double Update(in Bar bar) => _previous = AdaptiveRsiBlend.Next(bar.Close, _previous, _rsi.Next(bar.Close, true));
        public void Dispose() => _rsi.Dispose();
    }

    private sealed class GeneratedAsymmetricRsiState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly AsymmetricGainLossWindow _window = new(period, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAdaptiveStochasticState(int fast, int slow) : IIndicatorState, IDisposable
    {
        private readonly AdaptiveStochasticWindow _window = new(50, fast, slow);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedApirineRsiState(int period, int smooth, int count) : IIndicatorState, IDisposable
    {
        private readonly ApirineRsiWindow _window = new(MovingAvgType.WildersSmoothingMethod, period, smooth, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedPoweredKaufmanState(int period) : IMultiOutputState, IDisposable
    {
        private readonly PoweredKaufmanWindow _window = new(period, 3);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Power;
            outputs[1] = point.Average;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAdaptiveStopState(int period, double factor) : IIndicatorState, IDisposable
    {
        private readonly PoweredKaufmanWindow _window = new(period, factor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Stop;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAbsoluteErrorState(int period) : IIndicatorState, IDisposable
    {
        private readonly AbsoluteErrorWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedBelkhayateState : IIndicatorState, IDisposable
    {
        private readonly BelkhayateWindow _window = new();
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedBayesianState(int period) : IMultiOutputState, IDisposable
    {
        private readonly BayesianWindow _window = new(MovingAvgType.SimpleMovingAverage, period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Down;
            outputs[1] = point.Up;
            outputs[2] = point.Prime;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedAtrStopsState(int period, double factor, int count) : IIndicatorState, IDisposable
    {
        private readonly AtrTrailingWindow _window = new(MovingAvgType.ExponentialMovingAverage, 63, period, factor, false, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChandelierAliasState(int period, double factor, bool shortSide) : IIndicatorState, IDisposable
    {
        private readonly ChandelierWindow _window = new(MovingAvgType.WildersSmoothingMethod, period, factor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            return shortSide ? point.Short : point.Long;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedMotionStopState(int period) : IIndicatorState
    {
        private readonly MotionAttractionWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Stop;
    }

    private sealed class GeneratedElderSafeStopState(int period, double factor) : IIndicatorState, IDisposable
    {
        private readonly ElderSafeZoneWindow _window = new(MovingAvgType.ExponentialMovingAverage, 63, period, 3, factor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Value;
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedKaseStopV1State(KaseDevStopV1 stop) : IMultiOutputState, IDisposable
    {
        private readonly KaseStopV1Window _window = new(MovingAvgType.SimpleMovingAverage, stop.FastLength, stop.SlowLength, stop.Length, stop.StdDev1, stop.StdDev2, stop.StdDev3, stop.StdDev4);
        private double _previousTypical;
        private bool _started;
        public void Reset() { _window.Reset(); _previousTypical = 0; _started = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var input = KaseStopV1Window.Typical(bar.High, bar.Low, bar.Close);
            var high = bar.High; var low = bar.Low;
            if (!CalculationsHelper.IsWithinBarRange(input, low, high))
            {
                var previous = _started ? _previousTypical : input;
                high = Math.Max(previous, input); low = Math.Min(previous, input);
            }
            var point = _window.Next(high, low, bar.Close, input, true);
            outputs[0] = point.Dev1; outputs[1] = point.Dev2; outputs[2] = point.Dev3; outputs[3] = point.WarningLine;
            _previousTypical = input; _started = true;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedKaseStopV2State(KaseDevStopV2 stop) : IMultiOutputState, IDisposable
    {
        private readonly KaseStopV2Window _window = new(MovingAvgType.SimpleMovingAverage, stop.FastLength, stop.SlowLength, stop.Length, stop.StdDev1, stop.StdDev2, stop.StdDev3, stop.StdDev4);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Dev1; outputs[1] = point.Dev2; outputs[2] = point.Dev3; outputs[3] = point.Dev4;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedLinearStopState(int period, double multiplier) : IIndicatorState
    {
        private readonly GeneratedLinearChannelState _channel = new(period, multiplier);
        private double _side;
        public void Reset() { _channel.Reset(); _side = 0; }
        public double Update(in Bar bar)
        {
            Span<double> bounds = stackalloc double[2];
            _channel.Update(in bar, bounds);
            _side = bar.Close > bounds[0] ? 1 : bar.Close < bounds[1] ? 0 : _side;
            return (_side * bounds[1]) + ((1 - _side) * bounds[0]);
        }
    }

    private sealed class GeneratedPercentageStopsState(int period, double percentage, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RollingWindowMax _high = new(Math.Min(Math.Max(1, period), Math.Max(1, count)));
        private readonly Streaming.RollingWindowMin _low = new(Math.Min(Math.Max(1, period), Math.Max(1, count)));
        private double _previousHigh, _previousLow, _longStop, _shortStop;
        private bool _started;
        public void Reset() { _high.Reset(); _low.Reset(); _previousHigh = _previousLow = _longStop = _shortStop = 0; _started = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var previousHigh = _started ? _previousHigh : bar.Close;
            var previousLow = _started ? _previousLow : bar.Close;
            var longStop = _started ? _longStop : bar.Close;
            var shortStop = _started ? _shortStop : bar.Close;
            _longStop = bar.High > previousHigh ? RoundedPercentageBand.Percent(bar.High, percentage, -1) : longStop;
            _shortStop = bar.Low < previousLow ? RoundedPercentageBand.Percent(bar.Low, percentage, 1) : shortStop;
            _previousHigh = _high.Add(bar.High, out _); _previousLow = _low.Add(bar.Low, out _); _started = true;
            outputs[0] = _longStop; outputs[1] = _shortStop;
        }
        public void Dispose() { _high.Dispose(); _low.Dispose(); }
    }

    private sealed class GeneratedBilateralState(int length) : IMultiOutputState, IDisposable
    {
        private readonly BilateralStochasticWindow _window = new(MovingAvgType.SimpleMovingAverage, length, 20);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Bull; outputs[1] = point.Bear;
            outputs[2] = point.Bso; outputs[3] = point.SignalLine;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedBreakoutRsiState(int length) : IIndicatorState, IDisposable
    {
        private readonly BreakoutRsiWindow _window = new(length, 2);
        private double _previousPrice;
        private bool _started;
        public void Reset() { _window.Reset(); _previousPrice = 0; _started = false; }
        public double Update(in Bar bar)
        {
            var price = BreakoutRsiWindow.Price(bar.Open, bar.High, bar.Low, bar.Close);
            var high = bar.High; var low = bar.Low;
            if (!CalculationsHelper.IsWithinBarRange(price, low, high))
            {
                var previous = _started ? _previousPrice : price;
                high = Math.Max(previous, price); low = Math.Min(previous, price);
            }
            var value = _window.Next(price, bar.Open, high, low, bar.Close, bar.Volume, true).Value;
            _previousPrice = price; _started = true;
            return value;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedCctRsiState : IMultiOutputState, IDisposable
    {
        private readonly PriceRsiWindow[] _rsi;
        private readonly StrengthAverage[] _means;
        private readonly int[] _periods;
        private RollingMinMax[] _extrema;
        public GeneratedCctRsiState(int length1, int length2, int length3, int length4, int length5, int smooth1, int smooth2, MovingAvgType kind, int count)
        {
            var lengths = new[] { length1, length2, length3, length4, length5 }.Select(n => Math.Max(1, n)).ToArray();
            _rsi = lengths.Select(n => new PriceRsiWindow(kind, n, count)).ToArray();
            _means = new[] { smooth2, smooth1, smooth1, smooth1, 9 }.Select(n => new StrengthAverage(kind, Math.Max(1, n), count)).ToArray();
            _periods = [lengths[1], lengths[2], lengths[4], lengths[3], lengths[0], lengths[2], lengths[1]];
            _extrema = _periods.Select(n => new RollingMinMax(n)).ToArray();
        }
        public void Reset()
        {
            foreach (var rsi in _rsi) rsi.Reset();
            foreach (var mean in _means) mean.Reset();
            _extrema = _periods.Select(n => new RollingMinMax(n)).ToArray();
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            Span<double> strengths = stackalloc double[5];
            for (var i = 0; i < 5; i++) strengths[i] = _rsi[i].Next(bar.Close, true);
            _extrema[0].Add(strengths[4]); _extrema[1].Add(strengths[4]); _extrema[2].Add(strengths[4]);
            _extrema[3].Add(strengths[3]); _extrema[4].Add(strengths[0]); _extrema[5].Add(strengths[2]); _extrema[6].Add(strengths[1]);
            outputs[0] = CctRsiRatio.Percent(strengths[4], _extrema[0].Min, _extrema[1].Min, _extrema[1].Max);
            outputs[1] = CctRsiRatio.Percent(strengths[4], _extrema[2].Min, _extrema[2].Min, _extrema[2].Max);
            outputs[2] = CctRsiRatio.Percent(strengths[3], _extrema[3].Min, _extrema[3].Min, _extrema[3].Max);
            var type4 = CctRsiRatio.Percent(strengths[4], _extrema[1].Min, _extrema[1].Min, _extrema[0].Max);
            var type5 = CctRsiRatio.Percent(strengths[0], _extrema[4].Min, _extrema[4].Min, _extrema[4].Max);
            var type6 = CctRsiRatio.Percent(strengths[2], _extrema[5].Min, _extrema[5].Min, _extrema[5].Max);
            var custom = CctRsiRatio.Percent(strengths[1], _extrema[6].Min, _extrema[6].Min, _extrema[6].Max);
            outputs[3] = _means[0].Next(new StrengthValue(type4), true).Mantissa;
            outputs[4] = _means[1].Next(new StrengthValue(type5), true).Mantissa;
            outputs[5] = _means[2].Next(new StrengthValue(type6), true).Mantissa;
            outputs[6] = _means[3].Next(new StrengthValue(custom), true).Mantissa;
            outputs[7] = _means[4].Next(new StrengthValue(outputs[0]), true).Mantissa;
        }
        public void Dispose()
        {
            foreach (var rsi in _rsi) rsi.Dispose();
            foreach (var mean in _means) mean.Dispose();
            _extrema = [];
        }
    }

    private sealed class GeneratedChandeCompositeState : IMultiOutputState, IDisposable
    {
        private readonly ChandeCompositeWindow _window = new(MovingAvgType.DoubleExponentialMovingAverage, 5, 10, 20, 3);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.SignalLine;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedChandeForecastState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly ExactLinearFitWindow _window = new(length, observedHistory: Math.Max(1, length) > count, compact: true);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).PercentResidual(bar.Close);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChandeKrollState(int length) : IIndicatorState, IDisposable
    {
        private readonly ChandeKrollWindow _window = new(MovingAvgType.SimpleMovingAverage, length, 3);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedChandeAbsoluteState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly ChandeMomentumWindow _window = new(Math.Min(period, Math.Max(1, count)));
        private int _seen;
        public void Reset() { _window.Reset(); _seen = 0; }
        public double Update(in Bar bar)
        {
            var value = _window.Next(bar.Close, true);
            if (_seen < period) { _seen++; return 0; }
            return Math.Abs(value);
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChandeAverageState(int count, bool absolute) : IIndicatorState, IDisposable
    {
        private readonly ChandeMomentumAverageWindow _window = new(Math.Min(5, Math.Max(1, count)), Math.Min(10, Math.Max(1, count)), Math.Min(20, Math.Max(1, count)));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) { var value = _window.Next(bar.Close, true); return absolute ? Math.Abs(value) : value; }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChandeDisparityState : IIndicatorState, IDisposable
    {
        private readonly ChandeDisparityWindow _window = new(MovingAvgType.ExponentialMovingAverage, 200, 50, 20);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedChandeFilterState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly ChandeMomentumWindow _window = new(Math.Min(period, Math.Max(1, count)), 3);
        private readonly WmaValueState _signal = new(period, count);
        public void Reset() { _window.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value; outputs[1] = _signal.Update(new Bar(default, 0, 0, 0, value, 0));
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChartmillState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly ChartmillWindow _window = new(MovingAvgType.SimpleMovingAverage, length, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Open, bar.Close, PriceMean.Of(bar.High, bar.Low), true);
            outputs[0] = point.Close; outputs[1] = point.Open; outputs[2] = point.High; outputs[3] = point.Low;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedCenterLinearityState(int length) : IIndicatorState, IDisposable
    {
        private readonly CenterLinearityWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChopZoneState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly ChopZoneWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, 34, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, false, true);
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedCommoditySelectionState(int length) : IMultiOutputState, IDisposable
    {
        private readonly CommoditySelectionWindow _window = new(MovingAvgType.WildersSmoothingMethod, length, 50, 3000, 10);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Value; outputs[1] = point.Signal;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedBrownCompositeState(ConstanceBrownCompositeIndex brown) : IMultiOutputState, IDisposable
    {
        private readonly BrownCompositeWindow _window = new(MovingAvgType.SimpleMovingAverage, brown.FastLength, brown.SlowLength, brown.Length1, brown.Length2, brown.SmoothLength);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.Fast; outputs[2] = point.Slow;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedConfluenceState(int length) : IIndicatorState, IDisposable
    {
        private readonly ConfluenceWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar)
        {
            var close = UltimatePowerWeights.Fraction.Of(bar.Close);
            var full = (UltimatePowerWeights.Fraction.Of(bar.Open) + UltimatePowerWeights.Fraction.Of(bar.High)
                + UltimatePowerWeights.Fraction.Of(bar.Low) + close) / 4;
            return _window.Next(close, full, true).Value;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedCoppockState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly RocBankWindow _window = new(MovingAvgType.WeightedMovingAverage, [11, 14], [1, 1], [1, 1], length, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Signal;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDerivativeState(int length) : IIndicatorState, IDisposable
    {
        private readonly DerivativeWindow _window = new(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length), 9, 5, 3);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDidiState(int shortLength, int mediumLength, int longLength, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _short = Math.Max(1, shortLength) <= count ? new(Math.Max(1, shortLength)) : null;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _medium = Math.Max(1, mediumLength) <= count ? new(Math.Max(1, mediumLength)) : null;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _long = Math.Max(1, longLength) <= count ? new(Math.Max(1, longLength)) : null;
        public void Reset() { _short?.Reset(); _medium?.Reset(); _long?.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var medium = _medium?.Next(bar.Close, true) ?? 0;
            var first = _short?.Next(bar.Close, true) ?? 0;
            var last = _long?.Next(bar.Close, true) ?? 0;
#pragma warning disable S1244 // Preserve the exact zero-denominator rule from the batch outputs.
            outputs[0] = medium == 0 ? 0 : first / medium;
            outputs[1] = medium == 0 ? 0 : 1;
            outputs[2] = medium == 0 ? 0 : last / medium;
#pragma warning restore S1244
        }
        public void Dispose() { _short?.Dispose(); _medium?.Dispose(); _long?.Dispose(); }
    }

    private sealed class GeneratedPriceMomentumState(int first, int second, int signal, int count, bool histogram) : IMultiOutputState, IDisposable
    {
        private readonly PriceMomentumWindow _window = new(MovingAvgType.ExponentialMovingAverage, first, second, signal, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Value; outputs[1] = point.Signal;
            if (histogram) outputs[2] = point.Histogram;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDoubleSmoothedStochasticState(int length) : IMultiOutputState, IDisposable
    {
        private readonly DoubleSmoothedStochasticWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, 3, 15, 3);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Dss; outputs[1] = point.SignalLine;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedDtOscillatorState(int length) : IMultiOutputState, IDisposable
    {
        private readonly DtOscillatorWindow _window = new(MovingAvgType.WildersSmoothingMethod, length, 8, 5, 3);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.SignalLine;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedDoubleStochasticState(int length) : IMultiOutputState, IDisposable
    {
        private RollingMinMax _high = new(length), _low = new(length), _oscillator = new(Math.Max(2, length));
        private readonly Streaming.RoundedSimpleMovingAverageSmoother _line = new(3), _signal = new(3);
        public void Reset()
        {
            _high = new(length); _low = new(length); _oscillator = new(Math.Max(2, length));
            _line.Reset(); _signal.Reset();
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            _high.Add(bar.High); _low.Add(bar.Low);
            var first = ClampedRangePosition.Percent(bar.Close, _low.Min, _high.Max);
            _oscillator.Add(first);
            var second = ClampedRangePosition.Percent(first, _oscillator.Min, _oscillator.Max);
            var line = _line.Next(second, true);
            outputs[0] = line; outputs[1] = _signal.Next(line, true);
        }
        public void Dispose() { _line.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedDmiStochasticState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly DirectionalIndexWindow _directional = new(MovingAvgType.SimpleMovingAverage, length, Math.Max(1, count));
        private RollingMinMax _range = new(10);
        private readonly Streaming.SimpleMovingAverageSmoother _first = new(3), _second = new(3);
        public void Reset() { _directional.Reset(); _range = new(10); _first.Reset(); _second.Reset(); }
        public double Update(in Bar bar)
        {
            var point = _directional.Next(bar.High, bar.Low, bar.Close, true);
            var oscillator = point.Minus - point.Plus; _range.Add(oscillator);
            var range = _range.Max - _range.Min;
#pragma warning disable S1244 // Preserve the batch formula's exact zero-range rule and operation order.
            var fast = range != 0 ? MathHelper.MinOrMax((oscillator - _range.Min) / range * 100, 100, 0) : 0;
#pragma warning restore S1244
            return _second.Next(_first.Next(fast, true), true);
        }
        public void Dispose() { _directional.Dispose(); _first.Dispose(); _second.Dispose(); }
    }

    private sealed class GeneratedCalmarState(int length) : IIndicatorState, IDisposable
    {
        private readonly CalmarWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDemandIndexState : IIndicatorState
    {
        private readonly DemandIndexWindow _window = new();
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true).Value;
    }

    private sealed class GeneratedDemandOscillatorState : IMultiOutputState, IDisposable
    {
        private readonly DemandOscillatorWindow _window = new(MovingAvgType.ExponentialMovingAverage, 10, 2, 20);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true);
            outputs[0] = point.Line; outputs[1] = point.SignalLine;
        }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedDampingState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly DampingWindow _window = new(MovingAvgType.SimpleMovingAverage, length, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSyntheticPriceState(int length) : IIndicatorState
    {
        private readonly double _alpha = length > 2 ? 2d / (length + 1d) : .67;
        private double _high, _low, _fast, _slow;
        private bool _seeded;
        public void Reset() { _high = _low = _fast = _slow = 0; _seeded = false; }
        public double Update(in Bar bar)
        {
            var price = PriceMean.Of(Math.Max(bar.High, _high), Math.Min(bar.Low, _low));
            var fast = VidyaBlend.Compute(_seeded ? _fast : price, price, _alpha);
            var slow = VidyaBlend.Compute(_seeded ? _slow : price, price, _alpha / 2);
            _high = bar.High; _low = bar.Low; _fast = fast; _slow = slow; _seeded = true;
            return fast - slow;
        }
    }

    private sealed class GeneratedDirectionalTrendState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly StrengthWindow _strength = new(MovingAvgType.ExponentialMovingAverage, [length, 10, 5], count);
        private double _high, _low;
        public void Reset() { _strength.Reset(); _high = _low = 0; }
        public double Update(in Bar bar)
        {
            var up = bar.High > _high ? bar.High - _high : 0;
            var down = bar.Low < _low ? _low - bar.Low : 0;
            StrengthValue change;
            if (double.IsFinite(up) && double.IsFinite(down))
            {
                // Each subtraction rounds once, just like the exact sum divided by one.
                // Nonnegative finite differences cannot overflow when subtracted.
                change = new StrengthValue(up - down);
            }
            else
            {
                var wideUp = new ExactMeanAccumulator();
                if (bar.High > _high) { wideUp.Add(bar.High); wideUp.Add(_high, -1); }
                var wideDown = new ExactMeanAccumulator();
                if (bar.Low < _low) { wideDown.Add(_low); wideDown.Add(bar.Low, -1); }
                var net = new ExactMeanAccumulator();
                StrengthValue.Round(wideUp, 1).AddTo(ref net);
                StrengthValue.Round(wideDown, 1).AddTo(ref net, -1);
                change = StrengthValue.Round(net, 1);
            }
            var result = _strength.NextChange(change, true);
            _high = bar.High; _low = bar.Low;
            return result;
        }
        public void Dispose() => _strength.Dispose();
    }

    private sealed class GeneratedCoralTrendState(int length) : IIndicatorState
    {
        private readonly CoralTrendWindow _window = new(length, .4);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
    }

    private sealed class GeneratedCorrectedAverageState(int length) : IIndicatorState, IDisposable
    {
        private readonly CorrectedAverageWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedCubicWeightedState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly PooledRingBuffer<double> _values;
        private readonly System.Numerics.BigInteger _squareWeight, _cubeWeight;
        private readonly ExactMeanAccumulator _denominator;
        private ExactMeanAccumulator _sum, _linear, _square, _cube;
        public GeneratedCubicWeightedState(int length, int count)
        {
            _length = Math.Max(1, length);
            _values = new(Math.Min(_length, Math.Max(1, count)));
            var n = new System.Numerics.BigInteger(_length);
            _squareWeight = n * n; _cubeWeight = _squareWeight * n;
            var triangular = n * (n + 1) / 2;
            var denominator = new ExactMeanAccumulator(); denominator.Add(1, triangular * triangular);
            _denominator = denominator;
        }
        public void Reset() { _values.Clear(); _sum = _linear = _square = _cube = default; }
        public double Update(in Bar bar)
        {
            // Shift each old weight w to w-1 before adding the new n-weighted
            // sample. Update highest moments first; every operation is exact.
            _cube.Subtract(_square); _cube.Subtract(_square); _cube.Subtract(_square);
            _cube.AddExact(_linear); _cube.AddExact(_linear); _cube.AddExact(_linear);
            _cube.Subtract(_sum); _cube.Add(bar.Close, _cubeWeight);
            _square.Subtract(_linear); _square.Subtract(_linear);
            _square.AddExact(_sum); _square.Add(bar.Close, _squareWeight);
            _linear.Subtract(_sum); _linear.Add(bar.Close, _length);
            if (_values.Count == _length) _sum.Add(_values[0], -1);
            _sum.Add(bar.Close); _values.TryAdd(bar.Close, out _);
            return _cube.Ratio(_denominator);
        }
        public void Dispose() { Reset(); _values.Dispose(); }
    }

    private sealed class GeneratedDemaTwoLinesState(int fast, int slow) : IMultiOutputState
    {
        private readonly Streaming.EmaState _fastFirst = new(fast), _fastSecond = new(fast);
        private readonly Streaming.EmaState _slowFirst = new(slow), _slowSecond = new(slow);
        public void Reset() { _fastFirst.Reset(); _slowFirst.Reset(); _fastSecond.Reset(); _slowSecond.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var first = _fastFirst.GetNext(bar.Close, true);
            var second = _slowFirst.GetNext(bar.Close, true);
            outputs[0] = _fastSecond.GetNext(first, true);
            outputs[1] = _slowSecond.GetNext(second, true);
        }
    }

    private sealed class GeneratedDemarkPressureState(int length, int count, bool second) : IIndicatorState, IDisposable
    {
        // The period only controls eviction. A window larger than the complete
        // input cannot evict, so its storage may be capped at the input count.
        private readonly DemarkPressureWindow _window = new(Math.Min(Math.Max(1, length), Math.Max(1, count)), second);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedDemarkRangeState(int length) : IIndicatorState, IDisposable
    {
        private readonly DemarkRangeWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true);
        public void Dispose() => Reset();
    }

    private sealed class GeneratedDiNapoliOscillatorState(double lc, double sc, double sp, bool percentage) : IMultiOutputState
    {
        private readonly double _fastCoefficient = RoundedFractionalEma.Coefficient(sc, nameof(sc));
        private readonly double _slowCoefficient = RoundedFractionalEma.Coefficient(lc, nameof(lc));
        private readonly double _signalCoefficient = RoundedFractionalEma.Coefficient(sp, nameof(sp));
        private double _fast, _slow, _signal;
        public void Reset() { _fast = _slow = _signal = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            _fast = RoundedFractionalEma.Next(bar.Close, _fast, _fastCoefficient);
            _slow = RoundedFractionalEma.Next(bar.Close, _slow, _slowCoefficient);
            var line = percentage ? RoundedFractionalEma.Percentage(_fast, _slow) : _fast - _slow;
            _signal = RoundedFractionalEma.Next(line, _signal, _signalCoefficient);
            var offset = percentage ? 0 : 2;
            if (!percentage) { outputs[0] = _fast; outputs[1] = _slow; }
            outputs[offset] = line; outputs[offset + 1] = _signal; outputs[offset + 2] = line - _signal;
        }
    }

    private sealed class GeneratedDiNapoliStochasticState(int length) : IMultiOutputState, IDisposable
    {
        private RollingMinMax _high = new(length), _low = new(length);
        private double _line, _signal;
        public void Reset() { _high = new(length); _low = new(length); _line = _signal = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            _high.Add(bar.High); _low.Add(bar.Low);
            var fast = ClampedRangePosition.Percent(bar.Close, _low.Min, _high.Max);
            _line += (fast - _line) / 3;
            _signal += (_line - _signal) / 3;
            outputs[0] = _line; outputs[1] = _signal;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedDistanceMassState(int length, int count, bool reciprocal) : IIndicatorState, IDisposable
    {
        private readonly DistanceMassWindowMean _window = new(length, reciprocal, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDisparityState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _average = length <= count ? new(length) : null;
        public void Reset() => _average?.Reset();
        public double Update(in Bar bar) => RoundedPercentageChange.Of(bar.Close, _average?.Next(bar.Close, true) ?? 0);
        public void Dispose() => _average?.Dispose();
    }

    private sealed class GeneratedDoubleMomentaState(int lookback, int first, int second, int count) : IMultiOutputState, IDisposable
    {
        private readonly MomentaRangeWindow _window = new(MovingAvgType.ExponentialMovingAverage, lookback, first, second, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Value; outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDoubleRangeStrengthState(int count) : IMultiOutputState, IDisposable
    {
        private readonly RangeGainLossWindow _window = new(MovingAvgType.ExponentialMovingAverage, 2, [5, 25], 25, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Value; outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDynamicMomentumIndexState : IMultiOutputState, IDisposable
    {
        private readonly DynamicMomentumWindow _window = new(MovingAvgType.SimpleMovingAverage, 5, 10, 14, 5, 30);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Line; outputs[1] = value.SignalLine; outputs[2] = value.Histogram;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedDynamicMomentumOscillatorState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.RollingWindowMax _high = new(Math.Min(length, Math.Max(1, count)));
        private readonly Streaming.RollingWindowMin _low = new(Math.Min(length, Math.Max(1, count)));
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _fast = length <= count ? new(length) : null;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _slow = 20 <= count ? new(20) : null;
        private double _highest, _lowest = double.MaxValue;
        public void Reset() { _high.Reset(); _low.Reset(); _fast?.Reset(); _slow?.Reset(); _highest = 0; _lowest = double.MaxValue; }
        public double Update(in Bar bar)
        {
            var high = _high.Add(bar.High, out _); var low = _low.Add(bar.Low, out _);
            var fastK = ClampedRangePosition.Percent(bar.Close, low, high);
            var fastD = _fast?.Next(fastK, true) ?? 0;
            var slowD = _slow?.Next(fastD, true) ?? 0;
            _highest = fastD > _highest ? fastD : _highest;
            _lowest = fastD < _lowest ? fastD : _lowest;
            var midpoint = MathHelper.MinOrMax((_lowest + _highest) / 2, 100, 0);
            return MathHelper.MinOrMax(midpoint - (slowD - fastD), 100, 0);
        }
        public void Dispose() { _high.Dispose(); _low.Dispose(); _fast?.Dispose(); _slow?.Dispose(); }
    }

    private sealed class GeneratedDynamicSupportState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly DynamicSupportWindow _window = new(MovingAvgType.WildersSmoothingMethod, length, false, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Support; outputs[1] = value.Resistance; outputs[2] = value.Middle;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDynamicFilterState(int length) : IIndicatorState, IDisposable
    {
        private readonly DynamicFilterWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => Reset();
    }

    private sealed class GeneratedDynamicAverageState(int fast, int slow) : IIndicatorState, IDisposable
    {
        private readonly DynamicAverageWindow _window = new(fast, slow);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => Reset();
    }

    private sealed class GeneratedEdgePreservingState(int length) : IIndicatorState, IDisposable
    {
        private readonly EdgePreservingWindow _window = new(MovingAvgType.SimpleMovingAverage, length, 50);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedEfficiencyOutputState(int length, bool autoLine, double fast, double slow) : IIndicatorState, IDisposable
    {
        private readonly EfficiencyOutputWindow _window = new(length, autoLine, fast, slow);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => Reset();
    }

    private sealed class GeneratedEarningLevelsState : IIndicatorState
    {
        private double _previousLow, _laggedLow;
        public void Reset() { _previousLow = _laggedLow = 0; }
        public double Update(in Bar bar)
        {
            var mean = new ExactMeanAccumulator(); mean.Add(bar.High); mean.Add(_laggedLow);
            _laggedLow = _previousLow; _previousLow = bar.Low;
            return mean.Mean(2);
        }
    }

    private sealed class GeneratedElderThermometerState(int length) : IMultiOutputState, IDisposable
    {
        private readonly ElderThermometerWindow _window = new(MovingAvgType.ExponentialMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, true);
            outputs[0] = value.Value; outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedElderImpulseState(int length) : IIndicatorState
    {
        private readonly Streaming.EmaState _trend = new(length), _fast = new(12), _slow = new(26), _signal = new(9);
        private double _previousTrend, _previousHistogram;
        private bool _started;
        public void Reset() { _trend.Reset(); _fast.Reset(); _slow.Reset(); _signal.Reset(); _previousTrend = _previousHistogram = 0; _started = false; }
        public double Update(in Bar bar)
        {
            var trend = _trend.GetNext(bar.Close, true);
            var line = _fast.GetNext(bar.Close, true) - _slow.GetNext(bar.Close, true);
            var histogram = line - _signal.GetNext(line, true);
            var result = !_started ? 0 : trend > _previousTrend && histogram > _previousHistogram ? 1
                : trend < _previousTrend && histogram < _previousHistogram ? -1 : 0;
            _previousTrend = trend; _previousHistogram = histogram; _started = true;
            return result;
        }
    }

    private sealed class GeneratedElderRayState(int length) : IIndicatorState
    {
        private readonly Streaming.EmaState _average = new(length);
        private double _previousClose;
        private bool _started;
        public void Reset() { _average.Reset(); _previousClose = 0; _started = false; }
        public double Update(in Bar bar)
        {
            // The legacy builder derives the range even for an unchained close.
            var high = CalculationsHelper.IsWithinBarRange(bar.Close, bar.Low, bar.High)
                ? bar.High : Math.Max(_started ? _previousClose : bar.Close, bar.Close);
            var average = _average.GetNext(bar.Close, true);
            _previousClose = bar.Close; _started = true;
            return high - average;
        }
    }

    private sealed class GeneratedEndPointState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly PooledRingBuffer<double> _history = new(Math.Min(length, Math.Max(1, count)));
        private readonly System.Numerics.BigInteger _divisor = new System.Numerics.BigInteger(length) * (length - 7L) / 2;
        private ExactMeanAccumulator _sum, _weighted;
        public void Reset() { _history.Clear(); _sum = _weighted = default; }
        public double Update(in Bar bar)
        {
            var expired = _history.Count == length ? _history[0] : 0;
            _weighted.Subtract(_sum);
            _weighted.Add(expired, 4);
            _weighted.Add(bar.Close, length - 4L);
            _sum.Add(expired, -1); _sum.Add(bar.Close);
            _history.TryAdd(bar.Close, out _);
            var denominator = new ExactMeanAccumulator(); denominator.Add(1, _divisor);
            return _weighted.Ratio(denominator);
        }
        public void Dispose() => _history.Dispose();
    }

    private sealed class GeneratedElliottWaveState(int fast, int slow, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _fast = fast <= count ? new(fast) : null;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _slow = slow <= count ? new(slow) : null;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _signal = fast <= count ? new(fast) : null;
        private bool _invalid;
        public void Reset() { _fast?.Reset(); _slow?.Reset(); _signal?.Reset(); _invalid = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var line = (_fast?.Next(bar.Close, true) ?? 0) - (_slow?.Next(bar.Close, true) ?? 0);
            _invalid |= double.IsInfinity(line);
            var signal = _invalid ? double.NaN : _signal?.Next(line, true) ?? 0;
            outputs[0] = line; outputs[1] = signal; outputs[2] = line - signal;
        }
        public void Dispose() { _fast?.Dispose(); _slow?.Dispose(); _signal?.Dispose(); }
    }

    private sealed class GeneratedEmaWaveState(int first, int second, int third, int smooth, int count) : IMultiOutputState, IDisposable
    {
        private readonly ResidualAverageWindow _a = new(MovingAvgType.ExponentialMovingAverage, first, MovingAvgType.SimpleMovingAverage, [smooth], count);
        private readonly ResidualAverageWindow _b = new(MovingAvgType.ExponentialMovingAverage, second, MovingAvgType.SimpleMovingAverage, [smooth], count);
        private readonly ResidualAverageWindow _c = new(MovingAvgType.ExponentialMovingAverage, third, MovingAvgType.SimpleMovingAverage, [smooth], count);
        public void Reset() { _a.Reset(); _b.Reset(); _c.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        { outputs[0] = _a.Next(bar.Close, true).Value; outputs[1] = _b.Next(bar.Close, true).Value; outputs[2] = _c.Next(bar.Close, true).Value; }
        public void Dispose() { _a.Dispose(); _b.Dispose(); _c.Dispose(); }
    }

    private sealed class GeneratedEnhancedIndexState(int length, int signal) : IMultiOutputState, IDisposable
    {
        private readonly EnhancedIndexWindow _window = new(MovingAvgType.SimpleMovingAverage, length, signal);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var value = _window.Next(bar.High, bar.Low, bar.Close, true); outputs[0] = value.Line; outputs[1] = value.SignalLine; }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedFallingRisingState(int length) : IIndicatorState, IDisposable
    {
        private readonly FallingRisingWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Line;
        public void Dispose() => Reset();
    }

    private sealed class GeneratedFxSniperState(int cciLength, int t3Length, double factor) : IIndicatorState, IDisposable
    {
        private readonly FxSniperWindow _window = new(MovingAvgType.SimpleMovingAverage, cciLength, t3Length, factor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(CommodityIndexWindow.TypicalPrice(bar.High, bar.Low, bar.Close), true).Line;
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedFastSlowKurtosisState(int length, double ratio) : IMultiOutputState, IDisposable
    {
        private readonly FastSlowKurtosisWindow _window = new(length, ratio, MovingAvgType.WeightedMovingAverage);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Line; outputs[1] = point.Signal; }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedFastSlowCompositeState(bool rsi) : IMultiOutputState, IDisposable
    {
        private readonly FastSlowCompositeWindow _window = new(rsi, MovingAvgType.WeightedMovingAverage, 3, 6, 9, rsi ? 6 : 9);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.High, bar.Low, bar.Close, true); outputs[0] = point.Line; outputs[1] = point.SignalLine; }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedFastSlowDegreeState(int length) : IMultiOutputState, IDisposable
    {
        private readonly FastSlowDegreeWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, 3, 2, 14);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Line; outputs[1] = point.SignalLine; outputs[2] = point.Histogram; }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedFearGreedState(int fast, int slow, int smooth) : IMultiOutputState, IDisposable
    {
        private readonly FearGreedWindow _window = new(MovingAvgType.WeightedMovingAverage, fast, slow, smooth);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.High, bar.Low, bar.Close, true); outputs[0] = point.Line; outputs[1] = point.SignalLine; }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedFireflyState(int length) : IMultiOutputState, IDisposable
    {
        private readonly FireflyWindow _window = new(MovingAvgType.ZeroLagExponentialMovingAverage, length, 3);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.High, bar.Low, bar.Close, true); outputs[0] = point.Line; outputs[1] = point.SignalLine; }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedFoldedRsiState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly PriceRsiWindow _rsi = new(MovingAvgType.ExponentialMovingAverage, length, count);
        // Shared runs have a known total count: a longer sum cannot expire during this run.
        private readonly FoldedRsiSum _sum = new(Math.Min(Math.Max(1, length), Math.Max(1, count)));
        private readonly StrengthAverage _signal = new(MovingAvgType.ExponentialMovingAverage, length, count);
        public void Reset() { _rsi.Reset(); _sum.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        { var line = _sum.Next(_rsi.Next(bar.Close, true), true); outputs[0] = line; outputs[1] = _signal.Next(new StrengthValue(line), true).Mantissa; }
        public void Dispose() { _rsi.Dispose(); _sum.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedFisherStochasticState(int length) : IIndicatorState, IDisposable
    {
        private readonly FisherStochasticWindow _window = new(MovingAvgType.WeightedMovingAverage, length, 30, 5);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Line;
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedFibonacciRetraceState(int range, double factor) : IMultiOutputState, IDisposable
    {
        // Both published bands depend only on the range; the mean is used only for trade signals.
        private readonly FibonacciRetraceWindow _window = new(MovingAvgType.SimpleMovingAverage, 1, range, factor, includeMean: false);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.High, bar.Low, bar.Close, true); outputs[0] = point.Upper; outputs[1] = point.Lower; }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedForecastOscillatorState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly OneBarReturnWindow _window = new(false, MovingAvgType.SimpleMovingAverage, length, capacityHint: count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Value; outputs[1] = point.Signal; }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedFreedomState(int length) : IMultiOutputState, IDisposable
    {
        private readonly FreedomWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, bar.Volume, true); outputs[0] = point.Score; outputs[1] = point.Demand; }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedFunctionCandlesState(int length) : IMultiOutputState, IDisposable
    {
        private readonly FunctionCandleRsi _close = new(MovingAvgType.WildersSmoothingMethod, length);
        private readonly FunctionCandleRsi _open = new(MovingAvgType.WildersSmoothingMethod, length);
        private readonly FunctionCandleRsi _high = new(MovingAvgType.WildersSmoothingMethod, length);
        private readonly FunctionCandleRsi _low = new(MovingAvgType.WildersSmoothingMethod, length);
        private double _previousClose;
        private bool _started;
        public void Reset() { _close.Reset(); _open.Reset(); _high.Reset(); _low.Reset(); _previousClose = 0; _started = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            // Match the builder's CustomRange for selected inputs outside the candle.
            var withinRange = CalculationsHelper.IsWithinBarRange(bar.Close, bar.Low, bar.High);
            var previous = _started ? _previousClose : bar.Close;
            var high = withinRange ? bar.High : Math.Max(previous, bar.Close);
            var low = withinRange ? bar.Low : Math.Min(previous, bar.Close);
            outputs[0] = _close.Next(bar.Close, true); outputs[1] = _open.Next(bar.Open, true);
            outputs[2] = _high.Next(high, true); outputs[3] = _low.Next(low, true);
            _previousClose = bar.Close; _started = true;
        }
        public void Dispose() { _close.Dispose(); _open.Dispose(); _high.Dispose(); _low.Dispose(); }
    }

    private sealed class GeneratedGainLossAverageState(int length, int signal, int count) : IMultiOutputState, IDisposable
    {
        private readonly GainLossAverageWindow _window = new(MovingAvgType.WildersSmoothingMethod, length, signal, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Value; outputs[1] = point.Signal; }
        public void Dispose() { Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedGannHiLoState(int length, int count) : IIndicatorState, IDisposable
    {
        // Match the builder's guarded SMA rounding; periods beyond the source never warm up.
        private readonly Streaming.SimpleMovingAverageSmoother? _high = Math.Max(1, length) <= count ? new(length) : null;
        private readonly Streaming.SimpleMovingAverageSmoother? _low = Math.Max(1, length) <= count ? new(length) : null;
        private double _previousHigh, _previousLow, _line;
        public void Reset() { _high?.Reset(); _low?.Reset(); _previousHigh = _previousLow = _line = 0; }
        public double Update(in Bar bar)
        {
            var high = _high?.Next(bar.High, true) ?? 0; var low = _low?.Next(bar.Low, true) ?? 0;
            _line = bar.Close > _previousHigh ? low : bar.Close < _previousLow ? high : _line;
            _previousHigh = high; _previousLow = low; return _line;
        }
        public void Dispose() { _high?.Dispose(); _low?.Dispose(); }
    }

    private sealed class GeneratedGannTrendState(int length) : IIndicatorState, IDisposable
    {
        private RollingMinMax _high = new(length), _low = new(length);
        private double _highOne, _highTwo, _lowOne, _lowTwo, _line;
        public void Reset() { _high = new(length); _low = new(length); _highOne = _highTwo = _lowOne = _lowTwo = _line = 0; }
        public double Update(in Bar bar)
        {
            _high.Add(bar.High); _low.Add(bar.Low); var high = _high.Max; var low = _low.Min;
            _line = _highTwo > _highOne && high > _highOne ? 1 : _lowTwo < _lowOne && low < _lowOne ? -1 : _line;
            _highTwo = _highOne; _lowTwo = _lowOne; _highOne = high; _lowOne = low; return _line;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedGopalakrishnanState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly GopalakrishnanWindow _window = new(length);
        private readonly StrengthAverage _signal = new(MovingAvgType.WeightedMovingAverage, Math.Max(2, length), count);
        public void Reset() { _window.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        { var line = _window.Next(bar.High, bar.Low, true); outputs[0] = line; outputs[1] = _signal.Next(new StrengthValue(line), true).Mantissa; }
        public void Dispose() { _window.Reset(); _signal.Dispose(); }
    }

    private sealed class GeneratedGrandTrendState(int length, int horizon, double multiplier) : IMultiOutputState, IDisposable
    {
        private readonly GrandForecastWindow _window = new(length, horizon, multiplier);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Trend; outputs[1] = point.Upper; outputs[2] = point.Middle; outputs[3] = point.Lower; }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedGroverState(int length, double multiplier, bool cycle) : IIndicatorState, IDisposable
    {
        private readonly GroverWindow _window = new(MovingAvgType.WildersSmoothingMethod, length, cycle ? 20 : 1, multiplier, cycle);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true).Line;
        public void Dispose() => Reset();
    }

    private sealed class GeneratedHampelState(int length, double factor) : IIndicatorState, IDisposable
    {
        private readonly HampelWindow _window = new(length, factor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedHarmonicState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly PooledRingBuffer<double> _history;
        public GeneratedHarmonicState(int length, int count)
        {
            _length = Math.Max(1, length);
            _history = new(Math.Max(1, Math.Min(_length, count)));
        }
        public void Reset() => _history.Clear();
        public double Update(in Bar bar)
        {
            var value = bar.Close;
            if (_history.Count + 1 >= _length)
            {
                var sum = new ExactReciprocalSum();
                sum.Add(value);
                var start = _history.Count - (_length - 1);
                for (var i = _history.Count - 1; i >= start; i--) sum.Add(_history[i]);
                value = sum.Mean;
            }
            _history.TryAdd(bar.Close, out _);
            return value;
        }
        public void Dispose() => _history.Dispose();
    }

    private sealed class GeneratedHalfTrendState(int length) : IIndicatorState, IDisposable
    {
        private readonly HalfTrendWindow _window = new(MovingAvgType.SimpleMovingAverage, length, 100);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedHawkeyeState : IMultiOutputState, IDisposable
    {
        private static readonly System.Numerics.BigInteger Unit = System.Numerics.BigInteger.One << 1074;
        private readonly System.Numerics.BigInteger _denominator;
        private readonly int _sign;
        private System.Numerics.BigInteger _previousHigh, _previousLow, _previousMidpoint;
        public GeneratedHawkeyeState(double divisor)
        {
            var units = ExactVarianceWindow.Units(divisor);
            _sign = units.Sign;
            _denominator = units.IsZero ? System.Numerics.BigInteger.One : System.Numerics.BigInteger.Abs(units);
        }
        public void Reset() => _previousHigh = _previousLow = _previousMidpoint = default;
        public void Update(in Bar bar, Span<double> outputs)
        {
            // Rolling range/volume means affect trade signals, not the two published thresholds.
            var offset = (_previousHigh - _previousLow) * Unit * _sign;
            var midpoint = _previousMidpoint * _denominator;
            outputs[0] = ExactMeanAccumulator.UnitRatio(midpoint + offset, _denominator);
            outputs[1] = ExactMeanAccumulator.UnitRatio(midpoint - offset, _denominator);
            _previousHigh = ExactVarianceWindow.Units(bar.High);
            _previousLow = ExactVarianceWindow.Units(bar.Low);
            _previousMidpoint = ExactVarianceWindow.Units(PriceMean.Of(bar.High, bar.Low));
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedGuppyDistanceState(int[] lengths) : IMultiOutputState
    {
        private readonly Streaming.EmaState[] _averages = lengths.Select(length => new Streaming.EmaState(length)).ToArray();
        public void Reset() { foreach (var average in _averages) average.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            Span<double> values = stackalloc double[12];
            for (var i = 0; i < values.Length; i++) values[i] = _averages[i].GetNext(bar.Close, commit: true);
            outputs[0] = GuppyRibbonArithmetic.Distance(values[0], values[1], values[2], values[3], values[4], values[5]);
            outputs[1] = GuppyRibbonArithmetic.Distance(values[6], values[7], values[8], values[9], values[10], values[11]);
        }
    }

    private sealed class GeneratedGuppyMultipleState(int[] lengths) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.EmaState[] _averages = lengths.Select(length => new Streaming.EmaState(length)).ToArray();
        private readonly ExactPartialMeanWindow _smooth = new(1);
        private readonly Streaming.EmaState _signal = new(13);
        private bool _invalidSignal;
        public void Reset() { foreach (var average in _averages) average.Reset(); _smooth.Reset(); _signal.Reset(); _invalidSignal = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            Span<double> values = stackalloc double[27];
            for (var i = 0; i < values.Length; i++) values[i] = _averages[i].GetNext(bar.Close, commit: true);
            var fast = GuppyRibbonArithmetic.Mean(values[..11]);
            var slow = GuppyRibbonArithmetic.Mean(values[11..]);
            var raw = GuppyRibbonArithmetic.Percent(fast, slow);
            _invalidSignal |= !double.IsFinite(raw);
            // Preserve the unrepresentable primary output for the public output
            // validator; finite-only smoothers must not obscure that rejection.
            outputs[0] = double.IsFinite(raw) ? _smooth.Next(raw, true) : raw;
            outputs[1] = _invalidSignal ? double.NaN : _signal.GetNext(raw, commit: true);
        }
        public void Dispose() => _smooth.Dispose();
    }

    private readonly record struct GeneratedCountBackPoint(double High, double Low, int Higher, int Lower);

    private sealed class GeneratedCountBackDeque : IDisposable
    {
        private int[] _values;
        private int _start;
        public int Count { get; private set; }
        public GeneratedCountBackDeque(int capacity) => _values = System.Buffers.ArrayPool<int>.Shared.Rent(capacity);
        public int First => _values[_start];
        public int Last => _values[(int)(((long)_start + Count - 1) % _values.Length)];
        public void Add(int value) { _values[(int)(((long)_start + Count) % _values.Length)] = value; Count++; }
        public void RemoveFirst() { _start = (_start + 1) % _values.Length; Count--; }
        public void RemoveLast() => Count--;
        public void Reset() { _start = 0; Count = 0; }
        public void Dispose() { if (_values.Length == 0) return; System.Buffers.ArrayPool<int>.Shared.Return(_values); _values = Array.Empty<int>(); Reset(); }
    }

    private sealed class GeneratedCountBackState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly PooledRingBuffer<GeneratedCountBackPoint> _history;
        private readonly GeneratedCountBackDeque _higher, _lower, _tops, _bottoms;
        private int _position = -1, _firstIndex;
        public GeneratedCountBackState(int length, int count)
        {
            _length = Math.Max(1, length);
            var capacity = (int)Math.Min(Math.Max(1, count), 2L * _length);
            _history = new(capacity);
            _higher = new(capacity); _lower = new(capacity); _tops = new(capacity); _bottoms = new(capacity);
        }
        private GeneratedCountBackPoint At(int index) => _history[index - _firstIndex];
        public void Reset() { _position = -1; _firstIndex = 0; _history.Clear(); _higher.Reset(); _lower.Reset(); _tops.Reset(); _bottoms.Reset(); }
        public double Update(in Bar bar)
        {
            _position++;
            var retainedStart = _position - _history.Capacity + 1;
            while (_higher.Count > 0 && _higher.First < retainedStart) _higher.RemoveFirst();
            while (_lower.Count > 0 && _lower.First < retainedStart) _lower.RemoveFirst();
            while (_higher.Count > 0 && At(_higher.Last).High <= bar.High) _higher.RemoveLast();
            while (_lower.Count > 0 && At(_lower.Last).Low >= bar.Low) _lower.RemoveLast();
            var point = new GeneratedCountBackPoint(bar.High, bar.Low, _higher.Count == 0 ? -1 : _higher.Last, _lower.Count == 0 ? -1 : _lower.Last);
            if (_history.TryAdd(point, out _)) _firstIndex++;
            _higher.Add(_position); _lower.Add(_position);

            var windowStart = _position - _length + 1;
            while (_tops.Count > 0 && _tops.First < windowStart) _tops.RemoveFirst();
            while (_bottoms.Count > 0 && _bottoms.First < windowStart) _bottoms.RemoveFirst();
            while (_tops.Count > 0 && At(_tops.Last).High <= bar.High) _tops.RemoveLast();
            while (_bottoms.Count > 0 && At(_bottoms.Last).Low >= bar.Low) _bottoms.RemoveLast();
            _tops.Add(_position); _bottoms.Add(_position);

            // Equal extremes select the latest bar; an outside-bar tie selects its high.
            var rising = _tops.First >= _bottoms.First;
            var pivot = Math.Max(_tops.First, _bottoms.First);
            var bound = pivot - _length;
            var previous = rising ? At(pivot).Lower : At(pivot).Higher;
            if (previous >= 0 && previous >= bound)
            {
                var second = rising ? At(previous).Lower : At(previous).Higher;
                if (second >= 0 && second >= bound) return rising ? At(second).Low : At(second).High;
            }
            return bar.Close;
        }
        public void Dispose() { _history.Dispose(); _higher.Dispose(); _lower.Dispose(); _tops.Dispose(); _bottoms.Dispose(); }
    }

    private sealed class GeneratedHirashimaState(int length) : IMultiOutputState, IDisposable
    {
        private readonly HirashimaWindow _window = new(MovingAvgType.WeightedMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => _window.Next(bar.Close, true, outputs);
        public void Dispose() => Reset();
    }

    private sealed class GeneratedHerrickState(double pointValue) : IIndicatorState, IDisposable
    {
        private readonly HerrickPayoffWindow _window = new(pointValue);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(PriceMean.Of(bar.High, bar.Low), bar.Open, bar.Close, bar.Volume, true).Value;
        public void Dispose() => Reset();
    }

    private sealed class GeneratedHighLowIndexState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.RollingWindowMax _high = new(Math.Min(length, Math.Max(1, count)));
        private readonly Streaming.RollingWindowMin _low = new(Math.Min(length, Math.Max(1, count)));
        private readonly Streaming.RollingWindowSum _advances = new(Math.Min(length, Math.Max(1, count)));
        private readonly Streaming.RollingWindowSum _declines = new(Math.Min(length, Math.Max(1, count)));
        private readonly Streaming.EmaState _ema = new(length);
        private double _previousHigh, _previousLow;
        public void Reset() { _high.Reset(); _low.Reset(); _advances.Reset(); _declines.Reset(); _ema.Reset(); _previousHigh = _previousLow = 0; }
        public double Update(in Bar bar)
        {
            var high = _high.Add(bar.High, out _); var low = _low.Add(bar.Low, out _);
            var advances = _advances.Add(high > _previousHigh ? 1 : 0, out _);
            var declines = _declines.Add(low < _previousLow ? 1 : 0, out _);
            _previousHigh = high; _previousLow = low;
            var total = advances + declines;
            return _ema.GetNext(total == 0 ? 0 : 100 * advances / total, true);
        }
        public void Dispose() { _high.Dispose(); _low.Dispose(); _advances.Dispose(); _declines.Dispose(); }
    }

    private sealed class GeneratedInternalBarState(int length) : IMultiOutputState, IDisposable
    {
        private readonly InternalBarStrengthWindow _window = new(length, 3);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Value; outputs[1] = point.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedInertiaState(int length, int rviLength) : IIndicatorState, IDisposable
    {
        private readonly RelativeVolatilityWindow _high = new(MovingAvgType.WildersSmoothingMethod, 10, rviLength);
        private readonly RelativeVolatilityWindow _low = new(MovingAvgType.WildersSmoothingMethod, 10, rviLength);
        private readonly InertiaSmoother _regression = new(MovingAvgType.LinearRegression, length);
        public void Reset() { _high.Reset(); _low.Reset(); _regression.Reset(); }
        public double Update(in Bar bar) => _regression.Next(Helpers.RelativeVolatilityWindow.Mean(_high.Next(bar.High, true), _low.Next(bar.Low, true)), true);
        public void Dispose() { _high.Dispose(); _low.Dispose(); _regression.Reset(); }
    }

    private sealed class GeneratedHybridState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly PooledRingBuffer<double> _values;
        private readonly (double Sign, double Difference)[] _weights;
        private double _previous;
        private bool _started;
        public GeneratedHybridState(int length, int count)
        {
            _length = length; _values = new(Math.Min(length, Math.Max(1, count)));
            _weights = new (double, double)[Math.Min(length, 256)];
            for (var j = 1; j <= _weights.Length; j++) _weights[j - 1] = Weight(j);
        }
        private (double Sign, double Difference) Weight(int j)
        {
            var sign = 0.5 * (1 - Math.Cos((double)j / _length * Math.PI));
            var difference = sign - (0.5 * (1 - Math.Cos((double)(j - 1) / _length * Math.PI)));
            return (sign, difference);
        }
        public void Reset() { _values.Clear(); _previous = 0; _started = false; }
        public double Update(in Bar bar)
        {
            var previous = _started ? _previous : bar.Close;
            double value = 0;
            for (var j = 1; j <= _length; j++)
            {
                var weight = j <= _weights.Length ? _weights[j - 1] : Weight(j);
                var prior = Streaming.EhlersStreamingWindow.GetOffsetValue(_values, bar.Close, j - 1);
                value += ((weight.Sign * previous) + ((1 - weight.Sign) * prior)) * weight.Difference;
            }
            _values.TryAdd(bar.Close, out _); _previous = value; _started = true;
            return value;
        }
        public void Dispose() => _values.Dispose();
    }

    private sealed class GeneratedInsyncState(InsyncIndex spec) : IIndicatorState, IDisposable
    {
        private readonly InsyncWindow _window = new(spec.FastLength, spec.SlowLength, spec.MfiLength, spec.BbLength,
            spec.CciLength, spec.DpoLength, spec.RocLength, spec.RsiLength, spec.StochLength,
            spec.StochKLength, spec.StochDLength, spec.SmaLength, spec.StdDevMult, spec.Divisor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, CommodityIndexWindow.TypicalPrice(bar.High, bar.Low, bar.Close),
            bar.High, bar.Low, bar.Volume, bar.High, bar.Low, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedRsxState(int length) : IIndicatorState, IDisposable
    {
        private readonly RsxWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => Reset();
    }
    private sealed class GeneratedJrcState(int length, int scale, int smooth) : IMultiOutputState, IDisposable
    {
        private readonly JrcWindow _window = new(MovingAvgType.SimpleMovingAverage, length, scale, smooth);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.SignalLine;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedJapaneseState : IIndicatorState, IDisposable
    {
        private readonly int _lag;
        private readonly Streaming.SimpleMovingAverageSmoother _highMean, _lowMean, _closeMean;
        private readonly Streaming.RollingWindowMax _high;
        private readonly Streaming.RollingWindowMin _low;
        private readonly PooledRingBuffer<double> _closes;
        public GeneratedJapaneseState(int length, int count)
        {
            _lag = length; var half = MathHelper.MinOrMax((int)Math.Ceiling(length / 2d));
            _highMean = new(half); _lowMean = new(half); _closeMean = new(half);
            _high = new(half); _low = new(half); _closes = new(Math.Min(length, Math.Max(1, count)));
        }
        public void Reset() { _highMean.Reset(); _lowMean.Reset(); _closeMean.Reset(); _high.Reset(); _low.Reset(); _closes.Clear(); }
        public double Update(in Bar bar)
        {
            var high = _high.Add(_highMean.Next(bar.High, true), out _);
            var low = _low.Add(_lowMean.Next(bar.Low, true), out _);
            var close = _closeMean.Next(bar.Close, true);
            var prior = Streaming.EhlersStreamingWindow.GetOffsetValue(_closes, close, _lag);
            var value = ExactDifferenceRatio.Of(close, prior, high, low);
            _closes.TryAdd(close, out _); return value;
        }
        public void Dispose() { _highMean.Dispose(); _lowMean.Dispose(); _closeMean.Dispose(); _high.Dispose(); _low.Dispose(); _closes.Dispose(); }
    }

    private sealed class GeneratedMayerState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.RollingWindowSum _mean = new(Math.Min(length, Math.Max(1, count)), trackMeanRoundoff: true);
        public void Reset() => _mean.Reset();
        public double Update(in Bar bar)
        {
            var mean = bar.Close;
            if (length != 1)
            {
                mean = _mean.Average(bar.Close, true, out var samples);
                if (samples < length) mean = 0;
            }
            return mean != 0 ? bar.Close / mean : 0;
        }
        public void Dispose() => _mean.Dispose();
    }
    private sealed class GeneratedOceanState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly PooledRingBuffer<double> _logs = new(Math.Min(length, Math.Max(1, count)));
        private readonly Streaming.EmaState _signal = new(length);
        private readonly double _rootLength = MathHelper.Sqrt(length);
        public void Reset() { _logs.Clear(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var log = bar.Close > 0 ? Math.Log(bar.Close) * 1000 : 0;
            var prior = Streaming.EhlersStreamingWindow.GetOffsetValue(_logs, log, length);
            var value = (log - prior) / _rootLength * 100;
            var signal = _signal.GetNext(value, true);
            _logs.TryAdd(log, out _); outputs[0] = value; outputs[1] = signal;
        }
        public void Dispose() => _logs.Dispose();
    }

    private sealed class GeneratedKaseRatioState(int length) : IMultiOutputState, IDisposable
    {
        private readonly KaseRatioWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true);
            outputs[0] = point.Up; outputs[1] = point.Down;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }
    private sealed class GeneratedKaseConvergenceState(int length, int peakLength, int signalLength) : IIndicatorState, IDisposable
    {
        private readonly KaseConvergenceWindow _window = new(MovingAvgType.SimpleMovingAverage, length, peakLength, signalLength);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Value;
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedKasePeakV1State(int length, int smooth) : IMultiOutputState, IDisposable
    {
        private readonly KasePeakV1Window _window = new(length, smooth);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Level; outputs[1] = point.Peak;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }
    private sealed class GeneratedKasePeakV2State(int length) : IIndicatorState, IDisposable
    {
        private readonly KasePeakV2Window _window = new(MovingAvgType.SimpleMovingAverage, 8, 65, 9, length, 3, 40);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Value;
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedKaufmanBandsState(int length, double exponent) : IMultiOutputState, IDisposable
    {
        private readonly KaufmanAdaptiveBandWindow _window = new(length, exponent);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Upper; outputs[1] = point.Middle; outputs[2] = point.Lower;
        }
        public void Dispose() => _window.Reset();
    }
    private sealed class GeneratedKaufmanBinaryState(int length, double fast, double slow, double filter) : IIndicatorState, IDisposable
    {
        private readonly CertifiedKaufmanBinaryWindow _window = new(length, fast, slow, filter);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedKarobeinState(int length) : IIndicatorState, IDisposable
    {
        private readonly KarobeinWindow _window = new(MovingAvgType.ExponentialMovingAverage, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Line;
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }
    private sealed class GeneratedKwanState(int length, int delay) : IIndicatorState, IDisposable
    {
        private readonly KwanWindow _window = new(MovingAvgType.WildersSmoothingMethod, length, delay);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Value;
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedLbrPaintState(int length, int lookback, double multiplier) : IMultiOutputState, IDisposable
    {
        private readonly LbrPaintWindow _window = new(MovingAvgType.SimpleMovingAverage, length, lookback, multiplier);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Upper; outputs[1] = point.Lower; outputs[2] = point.Width;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }
    private sealed class GeneratedLindaState(int fast, int slow, int signal, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _fast = Math.Max(1, fast) <= count ? new(fast) : null;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _slow = Math.Max(1, slow) <= count ? new(slow) : null;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _macdSignal = Math.Max(1, signal) <= count ? new(signal) : null;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother? _ppoSignal = Math.Max(1, signal) <= count ? new(signal) : null;
        private bool _macdInvalid, _ppoInvalid;
        public void Reset()
        { _fast?.Reset(); _slow?.Reset(); _macdSignal?.Reset(); _ppoSignal?.Reset(); _macdInvalid = _ppoInvalid = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var f = _fast?.Next(bar.Close, true) ?? 0;
            var s = _slow?.Next(bar.Close, true) ?? 0;
            var ppo = RoundedPercentageChange.Of(f, s);
            var macd = f - s;
            _macdInvalid |= double.IsInfinity(macd); _ppoInvalid |= double.IsInfinity(ppo);
            var macdSignal = _macdInvalid ? double.NaN : _macdSignal?.Next(macd, true) ?? 0;
            var ppoSignal = _ppoInvalid ? double.NaN : _ppoSignal?.Next(ppo, true) ?? 0;
            outputs[0] = macd; outputs[1] = macdSignal; outputs[2] = macd - macdSignal;
            outputs[3] = ppo; outputs[4] = ppoSignal; outputs[5] = ppo - ppoSignal;
        }
        public void Dispose() { _fast?.Dispose(); _slow?.Dispose(); _macdSignal?.Dispose(); _ppoSignal?.Dispose(); }
    }

    private sealed class GeneratedLinearQuadraticState(int length) : IIndicatorState, IDisposable
    {
        private readonly LinearQuadraticWindow _window = new(MovingAvgType.SimpleMovingAverage, length, 25);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedLogisticState(int length, double gain, int count) : IIndicatorState, IDisposable
    {
        private readonly LogisticCorrelationWindow _window = new(Math.Min(Math.Max(1, length), Math.Max(1, count)), gain);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedMotionSmoothnessState(int length) : IIndicatorState, IDisposable
    {
        private readonly MotionSmoothnessWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedMacZState(int fast, int slow, int signal, int length, double multiplier) : IMultiOutputState, IDisposable
    {
        private readonly MacZWindow _window = new(MovingAvgType.SimpleMovingAverage, fast, slow, signal, length, multiplier);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.SignalLine; outputs[2] = point.Histogram;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedMacZVwapState(int fast, int slow, int signal, int volumeLength, int deviationLength, double gamma) : IMultiOutputState, IDisposable
    {
        private readonly MacZVwapWindow _window = new(MovingAvgType.SimpleMovingAverage, fast, slow, signal, volumeLength, deviationLength, gamma);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = point.Line; outputs[1] = point.SignalLine; outputs[2] = point.Histogram;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedKurtosisState(int count) : IMultiOutputState, IDisposable
    {
        private readonly KurtosisWindow _window = new(3, 1, 3, 65, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.Signal;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedMarketDirectionState(int length) : IIndicatorState, IDisposable
    {
        private readonly MarketDirectionWindow _window = new(length, 55);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }
    private sealed class GeneratedMassThrustState(bool oscillator, int length) : IMultiOutputState, IDisposable
    {
        private readonly MassThrustWindow _window = new(oscillator, MovingAvgType.ExponentialMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = point.Value; outputs[1] = point.SignalLine;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedMobilityState(int length) : IMultiOutputState, IDisposable
    {
        private readonly MobilityWindow _window = new(MovingAvgType.WeightedMovingAverage, 10, length, 7);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.SignalLine;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedMcClellanState : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RollingWindowSum _advances = new(19), _declines = new(19);
        private readonly Streaming.MacdEngine _macd = new(MovingAvgType.ExponentialMovingAverage, 19, 39, 9);
        private double _previous;
        public void Reset() { _advances.Reset(); _declines.Reset(); _macd.Reset(); _previous = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var advances = _advances.Add(bar.Close > _previous ? 1d : 0d, out _);
            var declines = _declines.Add(bar.Close < _previous ? 1d : 0d, out _);
            var rana = advances + declines != 0 ? 1000 * (advances - declines) / (advances + declines) : 0;
            var line = _macd.Next(rana, true, out var signal, out var histogram);
            _previous = bar.Close;
            outputs[0] = advances; outputs[1] = declines; outputs[2] = line; outputs[3] = signal; outputs[4] = histogram;
        }
        public void Dispose() { _advances.Dispose(); _declines.Dispose(); _macd.Dispose(); }
    }
    private sealed class GeneratedMidpointOscillatorState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.RollingWindowMax _high = new(Math.Min(Math.Max(1, length), Math.Max(1, count)));
        private readonly Streaming.RollingWindowMin _low = new(Math.Min(Math.Max(1, length), Math.Max(1, count)));
        private readonly Streaming.IMovingAverageSmoother _signal = Streaming.MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, 9);
        public void Reset() { _high.Reset(); _low.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var line = RoundedMidpointOscillator.Of(bar.Close, _high.Add(bar.High, out _), _low.Add(bar.Low, out _));
            outputs[0] = line; outputs[1] = _signal.Next(line, true);
        }
        public void Dispose() { _high.Dispose(); _low.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedModifiedGannState(int length) : IIndicatorState, IDisposable
    {
        private readonly ModifiedGannWindow _window = new(MovingAvgType.SimpleMovingAverage, length, 1);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Open, bar.Close, true).Value;
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }
    private sealed class GeneratedMorphedSineState(int length) : IIndicatorState, IDisposable
    {
        private readonly MorphedSineWindow _window = new(length, 100);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }
    private sealed class GeneratedMoveTrackerState : IMultiOutputState, IDisposable
    {
        private double _previousPrice, _previousMove;
        private bool _started;
        public void Reset() { _previousPrice = _previousMove = 0; _started = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var move = _started ? bar.Close - _previousPrice : 0;
            outputs[0] = move; outputs[1] = move - _previousMove;
            _previousPrice = bar.Close; _previousMove = move; _started = true;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedMultiLevelState(int length, double factor) : IIndicatorState, IDisposable
    {
        private readonly MultiLevelWindow _window = new(length, factor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Open, true);
        public void Dispose() => Reset();
    }
    private sealed class GeneratedMomentaRsiState(int lookback, int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly RangeGainLossWindow _window = new(MovingAvgType.ExponentialMovingAverage,
            Math.Max(1, lookback), new[] { length }, length, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var next = _window.Next(bar.Close, true);
            outputs[0] = next.Value; outputs[1] = next.Signal;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedMultiVoteState(int length) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.EmaState _signal = new(length);
        private double _high, _low, _close, _value;
        public void Reset() { _high = _low = _close = _value = 0; _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double highVote = bar.High > _high ? 1 : bar.High < _high ? -1 : 0;
            double lowVote = bar.Low > _low ? 1 : bar.Low < _low ? -1 : 0;
            double closeVote = bar.Close > _close ? 1 : bar.Close < _close ? -1 : 0;
            var votes = highVote + lowVote + closeVote;
            var volume = bar.Volume / 1000000;
            var value = _value + (volume * votes);
            outputs[0] = value; outputs[1] = _signal.GetNext(value, commit: true);
            _high = bar.High; _low = bar.Low; _close = bar.Close; _value = value;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedNormalizedVigorState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly NormalizedVigorWindow _window = new(MovingAvgType.SymmetricallyWeightedMovingAverage, length, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var next = _window.Next(bar.Close, bar.Open, bar.High, bar.Low, true);
            outputs[0] = next.Value; outputs[1] = next.Signal;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }
    private sealed class GeneratedNthDifferenceState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly NthDifferenceWindow _window = new(length, 2, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedNegativeDisparityState(int length, int signal, double top, double bottom) : IMultiOutputState, IDisposable
    {
        private readonly NegativeVolumeDisparityWindow _window = new(MovingAvgType.SimpleMovingAverage, length, signal, top, bottom);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var next = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = next.Line.Publish(); outputs[1] = next.SignalLine.Publish();
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedNarrowBandpassState : IIndicatorState, IDisposable
    {
        private readonly double[] _weights;
        private readonly PooledRingBuffer<double> _prices;
        public GeneratedNarrowBandpassState(int length, int count)
        {
            length = Math.Max(2, length);
            // A run of count bars can consume only lags 1 through count - 1.
            // Keep the original period in the weight formula and endpoint tests.
            var capacity = Math.Min(length, Math.Max(1, count));
            _weights = new double[capacity];
            for (var j = 1; j < capacity && j < length - 1; j++)
            {
                if (2L * j == length) continue;
                var x = (double)j / (length - 1);
                var window = new ExactMeanAccumulator(); window.Add(.42);
                window.AddProduct(-.5, Math.Cos(2 * Math.PI * x));
                window.AddProduct(.08, Math.Cos(4 * Math.PI * x));
                var product = new ExactMeanAccumulator();
                product.AddProduct(Math.Sin(2 * Math.PI * j / length), window.Mean(1));
                _weights[j] = product.Mean(1);
            }
            _prices = new(capacity);
        }
        public void Reset() => _prices.Clear();
        public double Update(in Bar bar)
        {
            var sum = new ExactMeanAccumulator();
            for (var j = 1; j < _weights.Length && j <= _prices.Count; j++)
                sum.AddProduct(_prices[_prices.Count - j], _weights[j]);
            var value = sum.Mean(1); _prices.TryAdd(bar.Close, out _); return value;
        }
        public void Dispose() { _prices.Clear(); _prices.Dispose(); }
    }

    private sealed class GeneratedNaturalSlopeState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly ExactLinearFitWindow _window = new(length, observedHistory: Math.Max(1, length) > count, compact: true);
        private readonly double _scale = Math.Log(Math.Max(1, length));
        private double _previous;
        public void Reset() { _window.Reset(); _previous = 0; }
        public double Update(in Bar bar)
        {
            var logarithm = bar.Close > 0 ? Math.Log(bar.Close) * 1000 : 0;
            var current = _window.Next(logarithm, true).Last;
            var value = (current - _previous) * _scale; _previous = current; return value;
        }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private static double[] NaturalLagWeights(int length)
    {
        var weights = new double[Math.Min(Math.Max(1, length), 256)];
        for (var j = 0; j < weights.Length; j++) weights[j] = 1 / Math.Sqrt(j + 1);
        return weights;
    }
    private sealed class GeneratedNaturalDirectionalState(int length, int smooth, int count) : IIndicatorState, IDisposable
    {
        private readonly int _length = Math.Max(1, length);
        private readonly double[] _weights = NaturalLagWeights(length);
        private readonly PooledRingBuffer<double> _logs = new(Math.Min(Math.Max(1, length), Math.Max(1, count)));
        private readonly Streaming.EmaState _signal = new(smooth);
        public void Reset() { _logs.Clear(); _signal.Reset(); }
        public double Update(in Bar bar)
        {
            var logarithm = bar.Close > 0 ? Math.Log(bar.Close) * 1000 : 0;
            double sum = 0, denominator = 0, absolute = 0;
            for (var j = 0; j < _length; j++)
            {
                var previous = j < _logs.Count ? _logs[_logs.Count - j - 1] : 0;
                var current = j == 0 ? logarithm : j <= _logs.Count ? _logs[_logs.Count - j] : 0;
                absolute += Math.Abs(previous - current);
                var fraction = absolute != 0 ? (logarithm - current) / absolute : 0;
                var ratio = j < _weights.Length ? _weights[j] : 1 / Math.Sqrt(j + 1);
                sum += fraction * ratio; denominator += ratio;
            }
            var value = _signal.GetNext(denominator != 0 ? sum / denominator * 100 : 0, true);
            _logs.TryAdd(logarithm, out _); return value;
        }
        public void Dispose() { Reset(); _logs.Dispose(); }
    }
    private sealed class GeneratedNaturalStochasticState(int length, int smooth, int count) : IIndicatorState, IDisposable
    {
        private readonly int _length = Math.Max(1, length);
        private readonly double[] _weights = NaturalLagWeights(length);
        private readonly Streaming.RollingWindowMax _high = new(Math.Min(Math.Max(1, length), Math.Max(1, count)));
        private readonly Streaming.RollingWindowMin _low = new(Math.Min(Math.Max(1, length), Math.Max(1, count)));
        private readonly PooledRingBuffer<double> _fractions = new(Math.Min(Math.Max(1, length), Math.Max(1, count)));
        private readonly Streaming.EmaState _signal = new(smooth);
        public void Reset() { _high.Reset(); _low.Reset(); _fractions.Clear(); _signal.Reset(); }
        public double Update(in Bar bar)
        {
            var high = _high.Add(bar.High, out _); var low = _low.Add(bar.Low, out _);
            var current = ExactRangePosition.Fraction(bar.Close, low, high);
            double sum = 0, denominator = 0;
            for (var j = 0; j < _length; j++)
            {
                var fraction = j == 0 ? current : j <= _fractions.Count ? _fractions[_fractions.Count - j] : 0;
                var ratio = j < _weights.Length ? _weights[j] : 1 / Math.Sqrt(j + 1);
                sum += fraction * ratio; denominator += ratio;
            }
            var value = _signal.GetNext(denominator != 0 ? (200 * sum / denominator) - 100 : 0, true);
            _fractions.TryAdd(current, out _); return value;
        }
        public void Dispose() { Reset(); _high.Dispose(); _low.Dispose(); _fractions.Dispose(); }
    }

    private sealed class GeneratedNaturalComboState(int length, int smooth, int count) : IIndicatorState, IDisposable
    {
        private readonly GeneratedNaturalDirectionalState _directional = new(length, smooth, count);
        private readonly GeneratedNaturalStochasticState _stochastic = new(length, smooth, count);
        public void Reset() { _directional.Reset(); _stochastic.Reset(); }
        public double Update(in Bar bar)
        {
            var directional = _directional.Update(in bar); var stochastic = _stochastic.Update(in bar);
            var combined = Math.Sign(directional) != Math.Sign(stochastic) ? directional * stochastic
                : ((Math.Abs(directional) * stochastic) + (Math.Abs(stochastic) * directional)) / 2;
            return Math.Sign(combined) * Math.Sqrt(Math.Abs(combined));
        }
        public void Dispose() { _directional.Dispose(); _stochastic.Dispose(); }
    }

    private sealed class GeneratedNaturalMarketState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly bool _mirror;
        private readonly double[] _coefficients;
        private readonly PooledRingBuffer<double> _logs;
        private readonly Streaming.EmaState? _ema;
        private readonly WmaValueState? _weighted;
        public GeneratedNaturalMarketState(int length, int count, bool mirror, bool weighted = false)
        {
            _length = Math.Max(1, length); _mirror = mirror;
            _logs = new(Math.Min(_length, Math.Max(1, count)));
            _coefficients = new double[Math.Min(_length, 256)];
            for (var j = 0; j < _coefficients.Length; j++)
                _coefficients[j] = mirror ? Math.Sqrt(j + 1) : Math.Sqrt(j) - Math.Sqrt(j + 1);
            if (weighted) _weighted = new(_length, count); else _ema = new(_length);
        }
        public void Reset() { _logs.Clear(); _ema?.Reset(); _weighted?.Reset(); }
        public double Update(in Bar bar)
        {
            var logarithm = bar.Close > 0 ? Math.Log(bar.Close) * 1000 : 0;
            double sum = 0;
            for (var j = 0; j < _length; j++)
            {
                var previous = j < _logs.Count ? _logs[_logs.Count - j - 1] : 0;
                if (_mirror)
                {
                    var root = j < _coefficients.Length ? _coefficients[j] : Math.Sqrt(j + 1);
                    sum += (logarithm - previous) / root * 100;
                }
                else
                {
                    var current = j == 0 ? logarithm : j <= _logs.Count ? _logs[_logs.Count - j] : 0;
                    var coefficient = j < _coefficients.Length ? _coefficients[j] : Math.Sqrt(j) - Math.Sqrt(j + 1);
                    sum += (previous - current) * coefficient;
                }
            }
            var raw = _mirror ? sum / _length : sum;
            var sample = new Bar(default, 0, 0, 0, raw, 0);
            var value = _weighted is null ? _ema!.GetNext(raw, true) : _weighted.Update(in sample);
            _logs.TryAdd(logarithm, out _); return value;
        }
        public void Dispose() { Reset(); _logs.Dispose(); }
    }
    private sealed class GeneratedNaturalMarketComboState(int length, int smooth, int count) : IIndicatorState, IDisposable
    {
        private readonly GeneratedNaturalMarketState _river = new(length, count, false, true);
        private readonly GeneratedNaturalMarketState _mirror = new(length, count, true, true);
        private readonly WmaValueState _signal = new(Math.Max(1, smooth), count);
        public void Reset() { _river.Reset(); _mirror.Reset(); _signal.Reset(); }
        public double Update(in Bar bar)
        {
            var river = _river.Update(in bar); var mirror = _mirror.Update(in bar);
            var combined = Math.Sign(mirror) != Math.Sign(river) ? mirror * river
                : ((Math.Abs(mirror) * river) + (Math.Abs(river) * mirror)) / 2;
            var value = Math.Sign(combined) * Math.Sqrt(Math.Abs(combined));
            // The published Nmc is unsmoothed; retain evaluation of the signal path.
            var sample = new Bar(default, 0, 0, 0, value, 0); _ = _signal.Update(in sample);
            return value;
        }
        public void Dispose() { _river.Dispose(); _mirror.Dispose(); _signal.Reset(); }
    }

    private sealed class GeneratedMirroredPairState(int length, int signalLength, bool percentage) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.EmaState _open = new(length), _close = new(length);
        private readonly Streaming.EmaState _signal = new(signalLength), _mirrorSignal = new(signalLength);
        private bool _invalid, _mirrorInvalid;
        public void Reset() { _open.Reset(); _close.Reset(); _signal.Reset(); _mirrorSignal.Reset(); _invalid = _mirrorInvalid = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var open = _open.GetNext(bar.Open, true); var close = _close.GetNext(bar.Close, true);
            var value = percentage ? RoundedPercentageChange.Of(close, open) : close - open;
            var mirror = percentage ? RoundedPercentageChange.Of(open, close) : open - close;
            _invalid |= !double.IsFinite(value); _mirrorInvalid |= !double.IsFinite(mirror);
            var signal = _invalid ? double.NaN : _signal.GetNext(value, true);
            var mirrorSignal = _mirrorInvalid ? double.NaN : _mirrorSignal.GetNext(mirror, true);
            outputs[0] = value; outputs[1] = signal; outputs[2] = value - signal;
            outputs[3] = mirror; outputs[4] = mirrorSignal; outputs[5] = mirror - mirrorSignal;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedAdaptiveQState(int length) : IIndicatorState, IDisposable
    {
        private readonly MovingAverageAdaptiveQWindow _window = new(length, .667, .0645);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => Reset();
    }
    private sealed class GeneratedAdaptiveFilterState(int length, double filter, double fast, double slow) : IIndicatorState, IDisposable
    {
        private readonly MovingAverageAdaptiveFilterWindow _window = new(length, filter, fast, slow);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => Reset();
    }

    private sealed class GeneratedMovingAverageV3State(int length) : IIndicatorState, IDisposable
    {
        private readonly MovingAverageV3Window _window = new(MovingAvgType.ExponentialMovingAverage, length, 3);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }
    private sealed class GeneratedModularState(int length, double beta) : IIndicatorState, IDisposable
    {
        private readonly ModularWindow _window = new(length, beta);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => Reset();
    }

    private sealed class GeneratedMultiDepthState(int length) : IMultiOutputState, IDisposable
    {
        private readonly MultiDepthWindow _window = new(length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Two; outputs[1] = point.One; outputs[2] = point.Three;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedNormalizedMacdState(int fastLength, int slowLength) : IIndicatorState, IDisposable
    {
        private double _fast, _slow;
        private bool _started;
        public void Reset() { _fast = _slow = 0; _started = false; }
        public double Update(in Bar bar)
        {
            if (!_started) { _fast = _slow = bar.Close; _started = true; return 0; }
            _fast = RoundedSeededEma.Next(bar.Close, _fast, fastLength);
            _slow = RoundedSeededEma.Next(bar.Close, _slow, slowLength);
            return RoundedPercentageChange.Of(_fast, _slow);
        }
        public void Dispose() => Reset();
    }
    private sealed class GeneratedNickReverseState(int length) : IIndicatorState, IDisposable
    {
        private readonly double _percent = Math.Max(1, length);
        private double _high, _low;
        private int _trend;
        public void Reset() { _high = _low = 0; _trend = 0; }
        public double Update(in Bar bar)
        {
            double high = 0, low = 0, value; var trend = _trend;
            if (_trend >= 0)
            {
                high = bar.Close > _high ? bar.Close : _high;
                value = RoundedPercentageBand.Percent(high, _percent, -1);
                if (bar.Close <= value) { trend = -1; low = bar.Close; value = RoundedPercentageBand.Percent(low, _percent, 1); }
            }
            else
            {
                low = bar.Close < _low ? bar.Close : _low;
                value = RoundedPercentageBand.Percent(low, _percent, 1);
                if (bar.Close > value) { trend = 1; high = bar.Close; value = RoundedPercentageBand.Percent(high, _percent, -1); }
            }
            _high = high; _low = low; _trend = trend; return value;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedMacdLeaderState(int fastLength, int slowLength, int signalLength) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.EmaState _fast = new(fastLength), _slow = new(slowLength);
        private readonly Streaming.EmaState _diffFast = new(fastLength), _diffSlow = new(slowLength), _signal = new(signalLength);
        private bool _fastInvalid, _slowInvalid, _signalInvalid;
        public void Reset()
        {
            _fast.Reset(); _slow.Reset(); _diffFast.Reset(); _diffSlow.Reset(); _signal.Reset();
            _fastInvalid = _slowInvalid = _signalInvalid = false;
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var fast = _fast.GetNext(bar.Close, true); var slow = _slow.GetNext(bar.Close, true);
            var diffFast = bar.Close - fast; var diffSlow = bar.Close - slow;
            _fastInvalid |= !double.IsFinite(diffFast); _slowInvalid |= !double.IsFinite(diffSlow);
            var first = fast + (_fastInvalid ? double.NaN : _diffFast.GetNext(diffFast, true));
            var second = slow + (_slowInvalid ? double.NaN : _diffSlow.GetNext(diffSlow, true));
            var value = first - second;
            _signalInvalid |= !double.IsFinite(value);
            if (!_signalInvalid) _ = _signal.GetNext(value, true);
            outputs[0] = value; outputs[1] = first; outputs[2] = second;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedPpoLeaderState(int signalLength) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.EmaState _fast = new(12), _slow = new(26);
        private readonly Streaming.EmaState _diffFast = new(12), _diffSlow = new(26), _signal = new(signalLength);
        private bool _fastInvalid, _slowInvalid, _signalInvalid;
        public void Reset()
        {
            _fast.Reset(); _slow.Reset(); _diffFast.Reset(); _diffSlow.Reset(); _signal.Reset();
            _fastInvalid = _slowInvalid = _signalInvalid = false;
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var fast = _fast.GetNext(bar.Close, true); var slow = _slow.GetNext(bar.Close, true);
            var diffFast = bar.Close - fast; var diffSlow = bar.Close - slow;
            _fastInvalid |= !double.IsFinite(diffFast); _slowInvalid |= !double.IsFinite(diffSlow);
            var first = fast + (_fastInvalid ? double.NaN : _diffFast.GetNext(diffFast, true));
            var second = slow + (_slowInvalid ? double.NaN : _diffSlow.GetNext(diffSlow, true));
            var value = RoundedFractionalEma.Percentage(first, second);
            _signalInvalid |= !double.IsFinite(value);
            var signal = _signalInvalid ? double.NaN : _signal.GetNext(value, true);
            outputs[0] = value; outputs[1] = signal; outputs[2] = value - signal;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedPercentChangeState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly OneBarReturnWindow _window = new(true, MovingAvgType.WeightedMovingAverage, length, capacityHint: count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Value; outputs[1] = point.Signal; }
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedPerformanceIndexState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly PooledRingBuffer<double>? _history = Math.Max(1, length) < count ? new(Math.Max(1, length)) : null;
        public void Reset() => _history?.Clear();
        public double Update(in Bar bar)
        {
            var previous = _history is not null && _history.Count == _history.Capacity ? _history[0] : 0;
            var value = RoundedPercentageChange.Of(bar.Close, previous);
            _history?.TryAdd(bar.Close, out _);
            return value;
        }
        public void Dispose() => _history?.Dispose();
    }

    private sealed class GeneratedPremierState(int length, int smoothLength) : IIndicatorState, IDisposable
    {
        private readonly PremierStochasticWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, smoothLength);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true);
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedPrettyGoodState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly PrettyGoodWindow _window = new(MovingAvgType.SimpleMovingAverage, length, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true);
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedPriceZoneState(int length) : IIndicatorState, IDisposable
    {
        private readonly PriceZoneWindow _window = new(MovingAvgType.ExponentialMovingAverage, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedPriceVolumeRankState(int fastLength, int slowLength, int count) : IMultiOutputState, IDisposable
    {
        private readonly RocBankAverage _fast = new(MovingAvgType.SimpleMovingAverage, Math.Max(1, fastLength), count);
        private readonly RocBankAverage _slow = new(MovingAvgType.SimpleMovingAverage, Math.Max(1, slowLength), count);
        private double _previous, _volume;
        public void Reset() { _fast.Reset(); _slow.Reset(); _previous = _volume = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = bar.Close > _previous ? bar.Volume > _volume ? 1 : 2 : bar.Volume <= _volume ? 3 : 4;
            var fast = _fast.Next(new(value), true).Publish(); var slow = _slow.Next(new(value), true).Publish();
            _previous = bar.Close; _volume = bar.Volume;
            outputs[0] = value; outputs[1] = slow; outputs[2] = fast;
        }
        public void Dispose() { _fast.Dispose(); _slow.Dispose(); }
    }
    private sealed class GeneratedPsychologicalState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly int _length = Math.Max(1, length);
        private readonly PooledRingBuffer<double> _conditions = new(Math.Min(Math.Max(1, length), Math.Max(1, count)));
        private double _previous, _sum;
        private bool _started;
        public void Reset() { _conditions.Clear(); _previous = _sum = 0; _started = false; }
        public double Update(in Bar bar)
        {
            var condition = _started && bar.Close > _previous ? 1d : 0d;
            if (_conditions.Count == _conditions.Capacity) _sum -= _conditions[0];
            _conditions.TryAdd(condition, out _); _sum += condition;
            _previous = bar.Close; _started = true;
            return 100d * _sum / _length;
        }
        public void Dispose() => _conditions.Dispose();
    }

    private sealed class GeneratedPringSpecialState(PringSpecialK indicator, int count) : IMultiOutputState, IDisposable
    {
        private readonly RocBankWindow _window = new(MovingAvgType.SimpleMovingAverage,
            [indicator.Length1, indicator.Length2, indicator.Length3, indicator.Length4, indicator.Length5, indicator.Length7,
                indicator.Length8, indicator.Length9, indicator.Length11, indicator.Length12, indicator.Length13, indicator.Length14],
            [indicator.Length1, indicator.Length1, indicator.Length1, indicator.Length2, indicator.Length6, indicator.Length7,
                indicator.Length8, indicator.Length9, indicator.Length10, indicator.Length10, indicator.Length10, indicator.Length11],
            [1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4], indicator.SmoothLength, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Value; outputs[1] = point.Signal; }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedPercentageTrendState(int length, double pct, int count) : IIndicatorState, IDisposable
    {
        private readonly PooledRingBuffer<double> _prices = new(Math.Min(length, Math.Max(1, count)));
        private readonly double[] _high = new double[Math.Min(length, Math.Max(1, count))];
        private readonly double[] _low = new double[Math.Min(length, Math.Max(1, count))];
        public void Reset() => _prices.Clear();
        public double Update(in Bar bar)
        {
            var observed = Math.Min(length, _prices.Count + 1);
            _high[0] = _low[0] = bar.Close;
            for (var offset = 1; offset < observed; offset++)
            {
                var price = _prices[_prices.Count - offset];
                _high[offset] = Math.Max(_high[offset - 1], price);
                _low[offset] = Math.Min(_low[offset - 1], price);
            }
            var trend = bar.Close; var period = 0; var previous = bar.Close;
            for (var lag = 1; lag <= length; lag++)
            {
                var current = lag <= _prices.Count ? _prices[_prices.Count - lag] : 0;
                if ((previous <= trend && current > trend) || (previous >= trend && current < trend)) period = 0;
                var index = Math.Min(period, observed - 1);
                var high = _high[index]; var low = _low[index];
                if (period >= observed) { high = Math.Max(high, 0); low = Math.Min(low, 0); }
                high = Math.Max(current, high); low = Math.Min(current, low);
                // Before iteration lag, period <= lag - 1, so the original period >= length branch is unreachable.
                period++;
                trend = current > trend ? high * (1 - pct) : low * (1 + pct);
                previous = current;
            }
            _prices.TryAdd(bar.Close, out _);
            return trend;
        }
        public void Dispose() => _prices.Dispose();
    }

    private sealed class GeneratedBinomialCascadeState(int length, bool pentuple) : IIndicatorState, IDisposable
    {
        private readonly BinomialCascadeWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, pentuple);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedPpoAverageState(int fastLength, int slowLength, int count) : IIndicatorState, IDisposable
    {
        private readonly AverageGapWindow _window = new(MovingAvgType.ExponentialMovingAverage, fastLength, slowLength, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedQuadraticMeanState(int length, int count, bool difference) : IIndicatorState, IDisposable
    {
        private readonly RollingRootMeanSquare _mean = new(Math.Min(Math.Max(1, length), Math.Max(1, count)));
        private readonly RocBankAverage? _average = difference ? new(MovingAvgType.SimpleMovingAverage, length, count) : null;
        public void Reset() { _mean.Reset(); _average?.Reset(); }
        public double Update(in Bar bar)
        {
            var mean = _mean.Next(bar.Close, true);
            return _average is null ? mean : mean - _average.Next(new(bar.Close), true).Publish();
        }
        public void Dispose() { _mean.Dispose(); _average?.Dispose(); }
    }

    private sealed class GeneratedSquareWeightState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly PooledRingBuffer<double> _history;
        private readonly System.Numerics.BigInteger _squareWeight;
        private readonly ExactMeanAccumulator _denominator;
        private ExactMeanAccumulator _sum, _linear, _quadratic;
        public GeneratedSquareWeightState(int length, int count)
        {
            _length = Math.Max(1, length);
            _history = new(Math.Min(_length, Math.Max(1, count)));
            var n = new System.Numerics.BigInteger(_length); _squareWeight = n * n;
            var denominator = new ExactMeanAccumulator(); denominator.Add(1, n * (n + 1) * (2 * n + 1) / 6);
            _denominator = denominator;
        }
        public void Reset() { _history.Clear(); _sum = _linear = _quadratic = default; }
        public double Update(in Bar bar)
        {
            // Shifting weights w to w-1 gives Q' = Q-2L+S+n*n*x and L' = L-S+n*x.
            var linear = _linear; linear.Subtract(_sum);
            var quadratic = _quadratic; quadratic.Subtract(_linear); quadratic.Subtract(linear);
            linear.Add(bar.Close, _length); quadratic.Add(bar.Close, _squareWeight);
            var sum = _sum;
            if (_history.Count == _length) sum.Add(_history[0], -1);
            sum.Add(bar.Close); _history.TryAdd(bar.Close, out _);
            _sum = sum; _linear = linear; _quadratic = quadratic;
            return quadratic.Ratio(_denominator);
        }
        public void Dispose() => _history.Dispose();
    }

    private sealed class GeneratedParametricCorrectiveState(int length) : IIndicatorState, IDisposable
    {
        private readonly ParametricCorrectiveWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => Reset();
    }
    private sealed class GeneratedParametricKalmanState(int length) : IIndicatorState, IDisposable
    {
        private readonly ParametricKalmanWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => Reset();
    }
    private sealed class GeneratedPeakValleyState(int length, int smoothLength) : IMultiOutputState, IDisposable
    {
        private readonly PeakValleyWindow _window = new(MovingAvgType.SimpleMovingAverage, length, smoothLength);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {var point = _window.Next(bar.Close, true); outputs[0] = point.Sign1; outputs[1] = point.Sign2; outputs[2] = point.Sign3;}
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedReallySimpleState(int length, int smoothLength) : IMultiOutputState, IDisposable
    {
        private readonly ReallySimpleWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, smoothLength);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {var point = _window.Next(bar.Close, bar.Low, true); outputs[0] = point.Line; outputs[1] = point.Signal;}
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedRexState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly RexWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {var point = _window.Next(bar.Close, bar.Open, bar.High, bar.Low, true); outputs[0] = point.Value; outputs[1] = point.Signal;}
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedRelativeSquaresState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly PooledRingBuffer<int> _votes = new(Math.Min(Math.Max(1, length), Math.Max(1, count)));
        private int _up, _down;
        private double _previous;
        public void Reset() { _votes.Clear(); _up = _down = 0; _previous = 0; }
        public double Update(in Bar bar)
        {
            var vote = bar.Close > _previous ? 1 : bar.Close < _previous ? -1 : 0;
            if (_votes.Count == _votes.Capacity) { if (_votes[0] > 0) _up--; else if (_votes[0] < 0) _down--; }
            _votes.TryAdd(vote, out _); if (vote > 0) _up++; else if (vote < 0) _down++;
            _previous = bar.Close;
            return (MathHelper.Pow(_up, 2) - MathHelper.Pow(_down, 2)) / MathHelper.Pow(_votes.Count, 2);
        }
        public void Dispose() => _votes.Dispose();
    }
    private sealed class GeneratedRecursiveDifferenciatorState(int length, double alpha) : IIndicatorState, IDisposable
    {
        private readonly RecursiveDifferenciatorWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, alpha);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Line;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRecursiveRsiState(int length) : IIndicatorState, IDisposable
    {
        private readonly RecursiveRsiWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedRecursiveStochasticState(int length, double alpha) : IIndicatorState, IDisposable
    {
        private readonly RecursiveStochasticWindow _window = new(length, alpha);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => Reset();
    }

    private sealed class GeneratedRelativeVigorState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly RelativeVigorWindow _window = new(MovingAvgType.SimpleMovingAverage, length, capacityHint: count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, bar.Open, bar.High, bar.Low, true); outputs[0] = point.Value; outputs[1] = point.Signal; }
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedRepulseState(int length) : IMultiOutputState, IDisposable
    {
        private readonly RepulseWindow _window = new(MovingAvgType.ExponentialMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, true); outputs[0] = point.Line; outputs[1] = point.Signal; }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRangeIdentifierState : IMultiOutputState, IDisposable
    {
        private double _upper, _lower;
        public void Reset() { _upper = 0; _lower = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (!(bar.Close < _upper && bar.Close > _lower)) { _upper = bar.High; _lower = bar.Low; }
            outputs[0] = _upper; outputs[1] = PriceMean.Of(_upper, _lower); outputs[2] = _lower;
        }
        public void Dispose() => Reset();
    }
    private sealed class GeneratedRatioOchlState : IIndicatorState, IDisposable
    {
        private readonly RatioOchlWindow _window = new();
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Open, bar.High, bar.Low, bar.Close, true);
        public void Dispose() => Reset();
    }
    private sealed class GeneratedRaviState(int fast, int slow, int count) : IIndicatorState, IDisposable
    {
        private readonly AverageGapWindow _window = new(MovingAvgType.SimpleMovingAverage, fast, slow, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedRelativeSpreadState(int fast, int slow, int length, int smooth) : IIndicatorState, IDisposable
    {
        private readonly OoplesFinance.StockIndicators.Streaming.RelativeSpreadKernel _window = new(MovingAvgType.ExponentialMovingAverage, fast, slow, length, smooth);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedRetrospectiveCandleState(int length) : IIndicatorState, IDisposable
    {
        private readonly RetrospectiveCandleWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Open, bar.High, bar.Low, bar.Close, true);
        public void Dispose() => Reset();
    }

    private sealed class GeneratedReverseRsiState(int length, double target) : IIndicatorState, IDisposable
    {
        private readonly ReverseRsiWindow _window = new(length, target);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => Reset();
    }

    private sealed class GeneratedRetentionAccelerationState(int length) : IIndicatorState, IDisposable
    {
        private readonly RetentionAccelerationWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true).Value;
        public void Dispose() => Reset();
    }
    private sealed class GeneratedRobustWeightingState(int length) : IIndicatorState, IDisposable
    {
        private readonly RobustWeightingWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedReverseMacdState : IMultiOutputState, IDisposable
    {
        private readonly Streaming.IMovingAverageSmoother _fast, _slow, _signal;
        private readonly double _fastAlpha, _slowAlpha;
        private double _previousFast, _previousSlow;
        private bool _signalInvalid;
        internal GeneratedReverseMacdState(int fast, int slow)
        {
            fast = Math.Max(1, fast); slow = Math.Max(1, slow);
            _fastAlpha = 2d / (1d + fast); _slowAlpha = 2d / (1d + slow);
            _fast = Streaming.MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, fast);
            _slow = Streaming.MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, slow);
            _signal = Streaming.MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, 9);
        }
        public void Reset()
        { _fast.Reset(); _slow.Reset(); _signal.Reset(); _previousFast = _previousSlow = 0; _signalInvalid = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var line = RoundedReverseMacd.Equilibrium(_previousFast, _previousSlow, _fastAlpha, _slowAlpha);
            _signalInvalid |= double.IsNaN(line) || double.IsInfinity(line);
            var signal = _signalInvalid ? double.NaN : _signal.Next(line, true);
            outputs[0] = line; outputs[1] = signal; outputs[2] = line - signal;
            _previousFast = _fast.Next(bar.Close, true); _previousSlow = _slow.Next(bar.Close, true);
        }
        public void Dispose() { _fast.Dispose(); _slow.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedSimpleCycleState(int length) : IIndicatorState, IDisposable
    {
        private readonly SimpleCycleWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => Reset();
    }
    private sealed class GeneratedSimplePriceZoneState(int length) : IIndicatorState, IDisposable
    {
        private readonly SimplePriceZoneWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => Reset();
    }
    private sealed class GeneratedSigmaSpikesState(int length) : IMultiOutputState, IDisposable
    {
        private readonly SigmaSpikesWindow _window = new(MovingAvgType.ExponentialMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Line; outputs[1] = point.Signal; }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSimpleLinesState(int length, double multiplier) : IIndicatorState, IDisposable
    {
        private readonly Streaming.SimpleLinesKernel _window = new(length, multiplier);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => Reset();
    }
    private sealed class GeneratedSentimentZoneState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly Streaming.IMovingAverageSmoother _average;
        private double _previous;
        internal GeneratedSentimentZoneState(int length)
        { _length = Math.Max(1, length); _average = Streaming.MovingAverageSmootherFactory.Create(MovingAvgType.TripleExponentialMovingAverage, _length); }
        public void Reset() { _previous = 0; _average.Reset(); }
        public double Update(in Bar bar)
        {
            var vote = bar.Close > _previous ? 1d : -1d;
            var average = _average.Next(vote, true); _previous = bar.Close;
            return 100 * average / _length;
        }
        public void Dispose() => _average.Dispose();
    }

    private sealed class GeneratedRandomWalkState(int length) : IMultiOutputState, IDisposable
    {
        private readonly RandomWalkWindow _window = new(MovingAvgType.WildersSmoothingMethod, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.High, bar.Low, bar.Close, true); outputs[0] = point.High; outputs[1] = point.Low; }
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedRsingState(int length) : IMultiOutputState, IDisposable
    {
        private readonly RsingWindow _window = new(MovingAvgType.WeightedMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, bar.High, bar.Low, bar.Volume, true); outputs[0] = point.Line; outputs[1] = point.Signal; }
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedSellGravitationState(int length) : IMultiOutputState, IDisposable
    {
        private readonly SellGravitationWindow _window = new(MovingAvgType.ExponentialMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, bar.Open, bar.High, bar.Low, true); outputs[0] = point.Line; outputs[1] = point.Signal; }
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedRainbowState(int length) : IMultiOutputState, IDisposable
    {
        private readonly RainbowWindow _window = new(MovingAvgType.SimpleMovingAverage, length, 10);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Line; outputs[1] = point.Upper; outputs[2] = point.Lower; }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSchaffFirstState(int fast, int slow, int cycle) : IIndicatorState, IDisposable
    {
        private readonly SchaffFirstPassWindow _window = new(MovingAvgType.ExponentialMovingAverage, fast, slow, cycle);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedSchaffShkState(int fast, int slow, int cycle, int first, int second) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.SchaffCycleKernel _window = new(MovingAvgType.ExponentialMovingAverage, fast, slow, cycle, first, second);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Stc; outputs[1] = point.Macd; }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedStationaryLevelsState(int length) : IMultiOutputState, IDisposable
    {
        private readonly StationaryLevelsWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Upper; outputs[1] = point.Middle; outputs[2] = point.Lower; outputs[3] = point.Deviation; }
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedStationaryOscillatorState(int length) : IIndicatorState, IDisposable
    {
        private readonly StationaryLevelsOscillatorWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSettingLessStepState : IIndicatorState, IDisposable
    {
        private readonly SettingLessStepWindow _window = new();
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => Reset();
    }
    private sealed class GeneratedSqueezeMomentumState(int length) : IIndicatorState, IDisposable
    {
        private readonly SqueezeMomentumWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSharpRollingState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly PooledRingBuffer<double> _history;
        private readonly System.Numerics.BigInteger _divisor;
        private readonly ExactMeanAccumulator _denominator;
        private ExactMeanAccumulator _sum, _aged;
        internal GeneratedSharpRollingState(int length, int count)
        {
            _length = Math.Max(1, length); _history = new(Math.Min(_length, Math.Max(1, count)));
            _divisor = new System.Numerics.BigInteger(_length) * (_length + 1L);
            var denominator = new ExactMeanAccumulator(); denominator.Add(1, _divisor); _denominator = denominator;
        }
        public void Reset() { _history.Clear(); _sum = _aged = default; }
        public double Update(in Bar bar)
        {
            // A = sum(lag * price). Shifting the window gives A' = A + S - n * expired.
            var aged = _aged; aged.AddExact(_sum); var sum = _sum;
            if (_history.Count == _length) { var expired = _history[0]; aged.Add(expired, -_length); sum.Add(expired, -1); }
            sum.Add(bar.Close);
            var mean = _history.Count >= _length - 1 ? sum.Mean(_length) : 0;
            var numerator = sum; numerator.Multiply(3d * (_length - 1L));
            var correction = aged; correction.Multiply(6); numerator.Subtract(correction);
            numerator.Add(mean, _divisor);
            _sum = sum; _aged = aged; _history.TryAdd(bar.Close, out _);
            return numerator.Ratio(_denominator);
        }
        public void Dispose() => _history.Dispose();
    }

    private sealed class GeneratedSequentialStreakState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly PooledRingBuffer<double> _prices;
        private ExactMeanAccumulator _sum;
        private int _direction, _streak;
        private double _previousMean, _previousOutput;
        private bool _hasPrevious;
        internal GeneratedSequentialStreakState(int length, int count)
        {
            _length = Math.Max(1, length);
            _prices = new(Math.Min(_length, Math.Max(1, count)));
        }
        public void Reset()
        {
            _prices.Clear(); _sum = default; _direction = _streak = 0;
            _previousMean = _previousOutput = 0; _hasPrevious = false;
        }
        public double Update(in Bar bar)
        {
            if (_prices.Count == _length) _sum.Add(_prices[0], -1);
            _sum.Add(bar.Close);
            var mean = _prices.Count >= _length - 1 ? _sum.Mean(_length) : 0;
            var direction = mean.CompareTo(_hasPrevious ? _previousMean : 0);
            // A full window sums to +/-length exactly when its nonzero directions agree.
            _streak = direction == 0 ? 0 : direction == _direction
                ? (_streak < _length ? _streak + 1 : _length) : 1;
            var result = _streak == _length ? mean : _hasPrevious ? _previousOutput : bar.Close;
            _prices.TryAdd(bar.Close, out _);
            _direction = direction; _previousMean = mean; _previousOutput = result; _hasPrevious = true;
            return result;
        }
        public void Dispose() => _prices.Dispose();
    }

    private sealed class GeneratedReversalBoundedState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly PooledRingBuffer<double> _ratios;
        private readonly StrengthAverage _first, _second;
        private ExactMeanAccumulator _sum;
        private double _previous;
        internal GeneratedReversalBoundedState(int length, int count)
        {
            _length = Math.Max(1, length);
            var smooth = MathHelper.MinOrMax((int)Math.Ceiling(_length / 2d));
            _first = new(MovingAvgType.ExponentialMovingAverage, smooth);
            _second = new(MovingAvgType.ExponentialMovingAverage, smooth);
            _ratios = new(Math.Min(_length, Math.Max(1, count)));
        }
        public void Reset() { _previous = 0; _sum = default; _ratios.Clear(); _first.Reset(); _second.Reset(); }
        public double Update(in Bar bar)
        {
            var change = new ExactMeanAccumulator(); change.Add(bar.Close); change.Add(_previous, -1);
            var first = _first.Next(StrengthValue.Round(change, 1).Absolute, true);
            var second = _second.Next(first, true);
            var numerator = new ExactMeanAccumulator(); first.AddTo(ref numerator);
            var denominator = new ExactMeanAccumulator(); second.AddTo(ref denominator);
            var ratio = numerator.Ratio(denominator);
            if (_ratios.Count == _length) _sum.Add(_ratios[0], -1);
            _sum.Add(ratio); _ratios.TryAdd(ratio, out _); _previous = bar.Close;
            return _sum.Mean(1);
        }
        public void Dispose() { _ratios.Dispose(); _first.Dispose(); _second.Dispose(); }
    }

    private sealed class GeneratedShapeshiftingWeightsState : IIndicatorState, IDisposable
    {
        private readonly double[] _weights;
        private readonly ExactMeanAccumulator _mass;
        private readonly PooledRingBuffer<double> _prices;
        internal GeneratedShapeshiftingWeightsState(int length, int count)
        {
            length = Math.Max(2, length);
            _weights = new double[Math.Min(length, Math.Max(1, count))];
            var d = new System.Numerics.BigInteger(length - 1);
            var cube = d * d * d; var fourth = cube * d; var linearStep = 2 * cube;
            System.Numerics.BigInteger power = 0, first = 1, second = 14, third = 36, linear = 0;
            var mass = new ExactMeanAccumulator();
            for (var j = 0; j < length; j++)
            {
                var denominator = fourth + power;
                var weight = ExactMeanAccumulator.UnitRatio((denominator - linear) << 1074, denominator);
                if (j < _weights.Length) _weights[j] = weight;
                mass.Add(weight);
                // Fourth finite differences reproduce j^4 exactly; no floating recurrence.
                power += first; first += second; second += third; third += 24; linear += linearStep;
            }
            _mass = mass; _prices = new(_weights.Length);
        }
        public void Reset() => _prices.Clear();
        public double Update(in Bar bar)
        {
            var sum = new ExactMeanAccumulator(); sum.AddProduct(bar.Close, _weights[0]);
            for (var j = 1; j < _weights.Length && j <= _prices.Count; j++)
                sum.AddProduct(_prices[_prices.Count - j], _weights[j]);
            var value = sum.Ratio(_mass); _prices.TryAdd(bar.Close, out _); return value;
        }
        public void Dispose() => _prices.Dispose();
    }

    private sealed class GeneratedSmoothedReturnState(int lag, int smoothing, int count) : IIndicatorState, IDisposable
    {
        private readonly SmoothedReturnWindow _window = new(MovingAvgType.ExponentialMovingAverage, lag, smoothing, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSimplifiedWeightedState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly StrengthAverage _average = new(MovingAvgType.WeightedMovingAverage, length, count);
        public void Reset() => _average.Reset();
        public double Update(in Bar bar) => _average.Next(new StrengthValue(bar.Close), true).Mantissa;
        public void Dispose() => _average.Dispose();
    }

    private sealed class GeneratedSmoothedDeltaState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly SmoothedDeltaWindow _window = new(MovingAvgType.SimpleMovingAverage, length, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSuperTrendFilterState(int length, double factor) : IIndicatorState, IDisposable
    {
        private readonly SuperTrendFilterWindow _window = new(length, factor);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => Reset();
    }

    private sealed class GeneratedSurfaceRoughnessState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly PooledRingBuffer<(double X, double Y)> _history;
        private System.Numerics.BigInteger _x, _y, _xx, _yy, _xy;
        private double _previous;
        internal GeneratedSurfaceRoughnessState(int length, int count)
        { _length = Math.Max(1, length); _history = new(Math.Min(_length, Math.Max(1, count))); }
        public double Update(in Bar bar)
        {
            var value = bar.Close;
            var x = ExactVarianceWindow.Units(_previous); var y = ExactVarianceWindow.Units(value);
            var full = _history.Count == _length; var oldest = full ? _history[0] : default;
            var expiredX = ExactVarianceWindow.Units(oldest.X); var expiredY = ExactVarianceWindow.Units(oldest.Y);
            var sx = _x + x - expiredX; var sy = _y + y - expiredY;
            var sxx = _xx + x * x - expiredX * expiredX; var syy = _yy + y * y - expiredY * expiredY; var sxy = _xy + x * y - expiredX * expiredY;
            var n = new System.Numerics.BigInteger(full ? _history.Count : _history.Count + 1); var covariance = n * sxy - sx * sy;
            var xSpread = n * sxx - sx * sx; var ySpread = n * syy - sy * sy;
            var correlation = xSpread.IsZero || ySpread.IsZero ? 0 : covariance.Sign * ExactPopulationDeviation.RootRatio((covariance * covariance) << 2148, xSpread * ySpread);
            var result = 1 - ((correlation + 1) / 2);
            _x = sx; _y = sy; _xx = sxx; _yy = syy; _xy = sxy;
            _history.TryAdd((_previous, value), out _); _previous = value;
            return result;
        }
        public void Reset() { _history.Clear(); _previous = 0; _x = _y = _xx = _yy = _xy = default; }
        public void Dispose() => _history.Dispose();
    }

    private sealed class GeneratedSupportResistanceOscillatorState : IIndicatorState, IDisposable
    {
        private readonly SupportResistanceOscillatorWindow _window = new();
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Open, bar.High, bar.Low, bar.Close, true).Line;
        public void Dispose() => Reset();
    }

    private sealed class GeneratedStiffnessState(int length, int lookback, int smoothing) : IIndicatorState, IDisposable
    {
        private readonly StiffnessWindow _window = new(MovingAvgType.SimpleMovingAverage, length, lookback, smoothing);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedMovementStrengthState(int length, int movement) : IIndicatorState, IDisposable
    {
        private readonly MovementStrengthWindow _window = new(MovingAvgType.WeightedMovingAverage, length, movement, 3);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSuperTrendState(int length) : IIndicatorState, IDisposable
    {
        private readonly SuperTrendWindow _window = new(MovingAvgType.WildersSmoothingMethod, length, 3);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Value;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedSwingIndexState(double limitMove) : IIndicatorState, IDisposable
    {
        private double _previousOpen, _previousClose;
        private bool _seeded;
        public void Reset() { _previousOpen = _previousClose = 0; _seeded = false; }
        public double Update(in Bar bar)
        {
            var withinRange = CalculationsHelper.IsWithinBarRange(bar.Close, bar.Low, bar.High);
            var previous = _seeded ? _previousClose : bar.Close;
            var high = withinRange ? bar.High : Math.Max(previous, bar.Close);
            var low = withinRange ? bar.Low : Math.Min(previous, bar.Close);
            var value = _seeded ? WilderSwingIndex.Compute(bar.Open, high, low, bar.Close, _previousOpen, _previousClose, limitMove) : 0;
            _previousOpen = bar.Open; _previousClose = bar.Close; _seeded = true; return value;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedTimePriceState : IMultiOutputState, IDisposable
    {
        private readonly int _length;
        private readonly Streaming.RollingWindowMax _high;
        private readonly Streaming.RollingWindowMin _low;
        private double _previousHigh, _previousLow;
        private int _lastRise = -1, _lastFall = -1, _index;
        internal GeneratedTimePriceState(int length, int count)
        {
            _length = Math.Max(1, length); var capacity = Math.Min(_length, Math.Max(1, count));
            _high = new(capacity); _low = new(capacity);
        }
        public void Reset() { _high.Reset(); _low.Reset(); _previousHigh = _previousLow = 0; _lastRise = _lastFall = -1; _index = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (bar.High > _previousHigh) _lastRise = _index;
            if (bar.Low < _previousLow) _lastFall = _index;
            var risingAge = _index - _lastRise; var fallingAge = _index - _lastFall;
            outputs[0] = Math.Min(risingAge, _length) / (double)_length - .5;
            outputs[1] = Math.Min(fallingAge, _length) / (double)_length - .5;
            _previousHigh = _high.Add(bar.High, out _); _previousLow = _low.Add(bar.Low, out _); _index++;
        }
        public void Dispose() { _high.Dispose(); _low.Dispose(); }
    }

    private sealed class GeneratedTickLineState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.IMovingAverageSmoother _mean = Streaming.MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, length));
        private readonly Streaming.IMovingAverageSmoother _signal = Streaming.MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, 5);
        private readonly PooledRingBuffer<double> _totals = new(Math.Min(5, Math.Max(1, count)));
        private double _total, _previousMean;
        public void Reset() { _mean.Reset(); _signal.Reset(); _totals.Clear(); _total = _previousMean = 0; }
        public double Update(in Bar bar)
        {
            var mean = _mean.Next(bar.Close, true);
            _total += bar.Close > _previousMean ? 1 : bar.Close < _previousMean ? -1 : 0;
            var previous = _totals.Count == 5 ? _totals[0] : 0;
            var value = _signal.Next(RoundedPercentageChange.Of(_total, previous), true);
            _previousMean = mean; _totals.TryAdd(_total, out _); return value;
        }
        public void Dispose() { _mean.Dispose(); _signal.Dispose(); _totals.Dispose(); }
    }

    private sealed class GeneratedTetherLineState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly GeneratedDonchianMiddleState _middle = new(Math.Max(1, length), count);
        private double _previousClose;
        private bool _seeded;
        public void Reset() { _middle.Reset(); _previousClose = 0; _seeded = false; }
        public double Update(in Bar bar)
        {
            var withinRange = CalculationsHelper.IsWithinBarRange(bar.Close, bar.Low, bar.High);
            var previous = _seeded ? _previousClose : bar.Close;
            var high = withinRange ? bar.High : Math.Max(previous, bar.Close);
            var low = withinRange ? bar.Low : Math.Min(previous, bar.Close);
            var rangeBar = new Bar(bar.Time, bar.Open, high, low, bar.Close, bar.Volume);
            var value = _middle.Update(in rangeBar);
            _previousClose = bar.Close; _seeded = true; return value;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedTfsPairState(int fast, int slow, int signal, int count, bool percentage) : IMultiOutputState, IDisposable
    {
        private readonly StrengthAverage _fast = new(MovingAvgType.SimpleMovingAverage, fast, count);
        private readonly StrengthAverage _slow = new(MovingAvgType.SimpleMovingAverage, slow, count);
        private readonly StrengthAverage _signal = new(MovingAvgType.SimpleMovingAverage, signal, count);
        private bool _invalid;
        public void Reset() { _fast.Reset(); _slow.Reset(); _signal.Reset(); _invalid = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var fastValue = _fast.Next(new StrengthValue(bar.Close), true).Mantissa;
            var slowValue = _slow.Next(new StrengthValue(bar.Close), true).Mantissa;
            var line = percentage ? RoundedPercentageChange.Of(fastValue, slowValue) : fastValue - slowValue;
            _invalid |= double.IsNaN(line) || double.IsInfinity(line);
            var signalValue = _invalid ? double.NaN : _signal.Next(new StrengthValue(line), true).Mantissa;
            outputs[0] = line; outputs[1] = signalValue; outputs[2] = line - signalValue;
        }
        public void Dispose() { _fast.Dispose(); _slow.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedTopsBottomsState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly UnroundedMovingAverage _mean;
        private MacZWindow.Number _previousMean, _previousRise, _previousFall;
        private int _riseStreak, _fallStreak, _seen;
        private bool _previousUp, _previousDown;
        internal GeneratedTopsBottomsState(int length)
        { _length = Math.Max(1, length); _mean = new(MovingAvgType.ExponentialMovingAverage, _length); }
        private void Advance(MacZWindow.Number value, ref MacZWindow.Number prior, ref int streak)
        {
            streak = (value - prior).Sign == 0 ? (streak < _length ? streak + 1 : _length) : 1;
            prior = value;
        }
        public double Update(in Bar bar)
        {
            var mean = _mean.Next(MacZWindow.Number.Of(bar.Close), true);
            var direction = (mean - _previousMean).Sign;
            Advance(direction > 0 ? mean : default, ref _previousRise, ref _riseStreak);
            Advance(direction < 0 ? mean : default, ref _previousFall, ref _fallStreak);
            var partial = _seen < _length - 1;
            var up = mean.Sign != 0 && (partial || _riseStreak == _length);
            var down = mean.Sign != 0 && (partial || _fallStreak == _length);
            var value = _previousUp && !up ? 1d : _previousDown && !down ? -1d : 0;
            _previousMean = mean; _previousUp = up; _previousDown = down; if (_seen < _length) _seen++;
            return value;
        }
        public void Reset() { _mean.Reset(); _previousMean = _previousRise = _previousFall = default; _riseStreak = _fallStreak = _seen = 0; _previousUp = _previousDown = false; }
        public void Dispose() { Reset(); _mean.Dispose(); }
    }

    private sealed class GeneratedTrendDirectionForceState(int length, int lookback) : IIndicatorState, IDisposable
    {
        private readonly ExactTrendForceWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, lookback);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTrendAnalysisIndexState(int length, int width) : IMultiOutputState, IDisposable
    {
        private readonly TrendAnalysisIndexWindow _window = new(MovingAvgType.SimpleMovingAverage, length, width);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Line; outputs[1] = point.SignalLine; }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTrendAnalysisIndicatorState(int length, int width) : IMultiOutputState, IDisposable
    {
        private readonly TrendAnalysisIndicatorWindow _window = new(MovingAvgType.SimpleMovingAverage, length, width);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.Close, true); outputs[0] = point.Line; outputs[1] = point.SignalLine; }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTrendDetectionState : IMultiOutputState, IDisposable
    {
        private readonly int _short, _long;
        private readonly PooledRingBuffer<double> _prices;
        private readonly PooledRingBuffer<RocBankValue> _momenta;
        private ExactMeanAccumulator _direction, _shortAbsolute, _longAbsolute;
        internal GeneratedTrendDetectionState(int shortLength, int longLength, int count)
        {
            _short = Math.Max(1, shortLength); _long = Math.Max(_short, longLength);
            _prices = new(Math.Min(_short, Math.Max(1, count))); _momenta = new(Math.Min(Math.Max(_short, _long), Math.Max(1, count)));
        }
        public void Update(in Bar bar, Span<double> outputs)
        { var point = Next(bar.Close, true); outputs[0] = point.Line; outputs[1] = point.Direction; }
        private static RocBankValue Absolute(RocBankValue value) => new(Math.Abs(value.Mantissa), value.UpperShift);
        internal (double Line, double Direction) Next(double price, bool commit)
        {
            var change = new ExactMeanAccumulator();
            if (_prices.Count == _short) { change.Add(price); change.Add(_prices[0], -1); }
            var momentum = RocBankValue.Round(change);
            var direction = _direction; var shortAbsolute = _shortAbsolute; var longAbsolute = _longAbsolute;
            if (_momenta.Count >= _short)
            {
                var old = _momenta[_momenta.Count - _short]; old.AddTo(ref direction, -1); Absolute(old).AddTo(ref shortAbsolute, -1);
            }
            if (_momenta.Count >= _long) Absolute(_momenta[_momenta.Count - _long]).AddTo(ref longAbsolute, -1);
            momentum.AddTo(ref direction); Absolute(momentum).AddTo(ref shortAbsolute); Absolute(momentum).AddTo(ref longAbsolute);
            var line = shortAbsolute; line.Subtract(longAbsolute);
            // Add the magnitude of the exact direction total without publishing it:
            // an overflowing direction can still cancel the long absolute total.
            var negativeMagnitude = direction;
            if (direction.Sign >= 0) { negativeMagnitude = default; negativeMagnitude.Subtract(direction); }
            line.Subtract(negativeMagnitude);
            if (commit)
            {
                _prices.TryAdd(price, out _); _momenta.TryAdd(momentum, out _);
                _direction = direction; _shortAbsolute = shortAbsolute; _longAbsolute = longAbsolute;
            }
            return (line.Mean(1), direction.Mean(1));
        }
        public void Reset() { _prices.Clear(); _momenta.Clear(); _direction = default; _shortAbsolute = default; _longAbsolute = default; }
        public void Dispose() { _prices.Dispose(); _momenta.Dispose(); }
    }

    private sealed class GeneratedTrenderState(int length, double factor) : IMultiOutputState, IDisposable
    {
        private readonly TrenderWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, factor);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        { var point = _window.Next(bar.High, bar.Low, bar.Close, true); outputs[0] = point.TrendUp; outputs[1] = point.TrendDn; outputs[2] = point.Trender; }
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedTrendForceHistogramState : IIndicatorState, IDisposable
    {
        private readonly Streaming.RollingWindowMax _high;
        private readonly Streaming.RollingWindowMin _low;
        private double _previousHigh, _previousLow, _a, _b, _c, _d;
        private ExactMeanAccumulator _sum;
        private int _index;
        internal GeneratedTrendForceHistogramState(int length, int count)
        { var capacity = Math.Min(Math.Max(2, length), Math.Max(1, count)); _high = new(capacity); _low = new(capacity); }
        public double Update(in Bar bar)
        {
            var a = bar.Close > _previousHigh ? 1d : 0d;
            var b = bar.Close < _previousLow ? 1d : 0d;
            var c = a == 1d ? _c + 1 : b - _b == 1d ? 0 : _c; // NOSONAR: S1244 - These values are exact binary flags (0 or 1), not approximate measurements.
            var d = b == 1d ? _d + 1 : a - _a == 1d ? 0 : _d; // NOSONAR: S1244 - These values are exact binary flags (0 or 1), not approximate measurements.
            var average = (c + d) / 2; _sum.Add(average);
            var result = average - _sum.Mean(_index + 1);
            _previousHigh = _high.Add(bar.Close, out _); _previousLow = _low.Add(bar.Close, out _);
            _a = a; _b = b; _c = c; _d = d; _index++;
            return result;
        }
        public void Reset() { _high.Reset(); _low.Reset(); _previousHigh = _previousLow = _a = _b = _c = _d = 0; _sum = default; _index = 0; }
        public void Dispose() { _high.Dispose(); _low.Dispose(); }
    }

    private sealed class GeneratedTrendImpulseState : IIndicatorState, IDisposable
    {
        private readonly Streaming.RollingWindowMax _high;
        private readonly Streaming.RollingWindowMin _low;
        private readonly Streaming.IMovingAverageSmoother _signal;
        private double _previousHigh, _previousLow, _previous;
        private bool _seeded;
        internal GeneratedTrendImpulseState(int length, int smoothing, int count)
        {
            var capacity = Math.Min(Math.Max(2, length), Math.Max(1, count)); _high = new(capacity); _low = new(capacity);
            _signal = Streaming.MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, smoothing));
        }
        public double Update(in Bar bar)
        {
            var previous = _seeded ? _previous : bar.Close;
            var breakout = bar.Close > _previousHigh || bar.Close < _previousLow ? 1d : 0d;
            var value = (breakout * bar.Close) + ((1 - breakout) * previous);
            var result = _signal.Next(value, true);
            _previousHigh = _high.Add(bar.Close, out _); _previousLow = _low.Add(bar.Close, out _); _previous = value; _seeded = true;
            return result;
        }
        public void Reset() { _high.Reset(); _low.Reset(); _signal.Reset(); _previousHigh = _previousLow = _previous = 0; _seeded = false; }
        public void Dispose() { _high.Dispose(); _low.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedTrendPersistenceState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly PooledRingBuffer<sbyte> _votes;
        private readonly Streaming.IMovingAverageSmoother _mean = Streaming.MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, 5);
        private double _previous, _previous2;
        private long _balance;
        internal GeneratedTrendPersistenceState(int length, int count)
        { _length = Math.Max(1, length); _votes = new(Math.Min(_length, Math.Max(1, count))); }
        public double Update(in Bar bar)
        {
            var mean = _mean.Next(bar.Close, true);
            var difference = (_previous - _previous2) / .01;
            var vote = (sbyte)((difference > 1 ? 1 : 0) - (difference < -1 ? 1 : 0));
            if (_votes.TryAdd(vote, out var expired)) _balance -= expired;
            _balance += vote;
            var result = Math.Abs(100d * _balance / _length);
            _previous2 = _previous; _previous = mean;
            return result;
        }
        public void Reset() { _mean.Reset(); _votes.Clear(); _previous = _previous2 = 0; _balance = 0; }
        public void Dispose() { _mean.Dispose(); _votes.Dispose(); }
    }

    private sealed class GeneratedTrendExhaustionState : IMultiOutputState, IDisposable
    {
        private readonly int _length;
        private readonly double _coefficient;
        private readonly Streaming.RollingWindowMax _high;
        private readonly SignalMean _signal;
        private double _rises, _highs, _previous, _previousClose, _previousHigh;
        internal GeneratedTrendExhaustionState(int length, int count)
        {
            _length = Math.Max(1, length); _coefficient = 2d / unchecked(_length + 1);
            _high = new(Math.Min(Math.Max(2, _length), Math.Max(1, count)));
            _signal = new(_length, count);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            _rises += bar.Close > _previousClose ? 1 : 0;
            _highs += bar.High > _previousHigh ? 1 : 0;
            var ratio = _rises != 0 ? _highs / _rises : 0;
            var value = _previous + (_coefficient * (ratio - _previous));
            var signal = value;
            if (_length != 1)
            { signal = _signal.Next(value); }
            outputs[0] = value; outputs[1] = signal;
            _previous = value; _previousClose = bar.Close; _previousHigh = _high.Add(bar.High, out _);
        }
        public void Reset() { _high.Reset(); _signal.Reset(); _rises = _highs = _previous = _previousClose = _previousHigh = 0; }
        public void Dispose() { _high.Dispose(); _signal.Dispose(); }

        private sealed class SignalMean(int period, int count) : IDisposable
        {
            private readonly double[] _window = period > 1 && period <= count ? new double[period] : [];
            private int _seen, _next;
            private double _sum, _roundoff;
            private bool _exactRequired;

            public void Reset()
            {
                Array.Clear(_window, 0, _window.Length);
                _seen = _next = 0;
                _sum = _roundoff = 0;
                _exactRequired = false;
            }

            internal double Next(double value)
            {
                if (_window.Length == 0) return period == 1 ? value : 0;

                double expired = _window[_next];
                _window[_next] = value;
                if (++_next == period) _next = 0;
                double previous = _sum;
                _sum += value;
                _roundoff = MeanRoundoff.AfterAddition(_roundoff, _sum);
                _exactRequired |= ExactMeanAccumulator.SevereCancellation(previous, value, _sum);
                if (_seen >= period)
                {
                    previous = _sum;
                    _sum -= expired;
                    _roundoff = MeanRoundoff.AfterAddition(_roundoff, _sum);
                    _exactRequired |= ExactMeanAccumulator.SevereCancellation(previous, -expired, _sum);
                    if (Math.Abs(_sum) <= 1e-4 * Math.Max(Math.Abs(expired), Math.Abs(value)))
                        Rebuild(false);
                }
                _seen++;
                double mean = _seen >= period ? _sum / period : 0;
                if (_seen >= period && (_exactRequired || MeanRoundoff.RequiresExact(_sum, period, _roundoff)))
                {
                    var exact = new ExactMeanAccumulator();
                    int index = _next;
                    for (int j = 0; j < period; j++)
                    {
                        exact.Add(_window[index]);
                        if (++index == period) index = 0;
                    }
                    mean = exact.Mean(period);
                }
                if (_seen % period == 0) Rebuild(true);
                return mean;
            }

            private void Rebuild(bool periodic)
            {
                _sum = _roundoff = 0;
                // Cancellation rebuilds preserve the flag; only periodic rebuilds reset it.
                if (periodic) _exactRequired = false;
                int index = _next;
                for (int j = 0; j < period; j++)
                {
                    double previous = _sum, value = _window[index];
                    _sum += value;
                    _roundoff = MeanRoundoff.AfterAddition(_roundoff, _sum);
                    if (periodic) _exactRequired |= ExactMeanAccumulator.SevereCancellation(previous, value, _sum);
                    if (++index == period) index = 0;
                }
            }

            public void Dispose() => Reset();
        }
    }

    private sealed class GeneratedTrueRangeAdjustedState(int length, double multiplier) : IIndicatorState, IDisposable
    {
        private readonly TrueRangeAdjustedWindow _window = new(length, multiplier);
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true).Line;
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedTrimeanState : IMultiOutputState, IDisposable
    {
        private readonly int _capacity;
        private RollingOrderStatistic? _linear;
        private readonly PooledRingBuffer<double>? _prices;
        private OrderStatisticTree _tree = new();
        internal GeneratedTrimeanState(int length, int count)
        {
            length = Math.Max(1, length); _capacity = Math.Min(length, Math.Max(1, count));
            // Preserve the original backend: equal signed-zero keys retain their first tree value.
            if (length <= RollingWindowSettings.SmallWindowThreshold) _linear = new(_capacity);
            else _prices = new(_capacity);
        }
        private double Percentile(double percentile)
        {
            if (_linear is not null) return _linear.PercentileNearestRank(percentile);
            var rank = Math.Min(Math.Max((int)Math.Ceiling(percentile / 100 * _tree.Count), 1), _tree.Count);
            return _tree.SelectByRank(rank);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (_linear is not null) _linear.Add(bar.Close);
            else { if (_prices!.TryAdd(bar.Close, out var expired)) _tree.Remove(expired); _tree.Insert(bar.Close); }
            var first = Percentile(25); var median = Percentile(50); var third = Percentile(75);
            outputs[0] = PriceMean.Of(first, median, median, third); outputs[1] = first; outputs[2] = median; outputs[3] = third;
        }
        public void Reset()
        { if (_linear is not null) { _linear.Dispose(); _linear = new(_capacity); } else { _prices!.Clear(); _tree = new(); } }
        public void Dispose() { _linear?.Dispose(); _prices?.Dispose(); _tree = new(); }
    }

    private sealed class GeneratedTargetReturnState : IIndicatorState, IDisposable
    {
        private readonly struct Fraction
        {
            internal readonly System.Numerics.BigInteger N;
            private readonly System.Numerics.BigInteger _d;
            internal System.Numerics.BigInteger D => _d.IsZero ? System.Numerics.BigInteger.One : _d;
            internal Fraction(System.Numerics.BigInteger numerator, System.Numerics.BigInteger denominator)
            {
                if (denominator.IsZero) throw new DivideByZeroException();
                if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
                var gcd = System.Numerics.BigInteger.GreatestCommonDivisor(System.Numerics.BigInteger.Abs(numerator), denominator);
                N = numerator / gcd; _d = denominator / gcd;
            }
            public static Fraction operator +(Fraction a, Fraction b)
            {
                var common = System.Numerics.BigInteger.GreatestCommonDivisor(a.D, b.D);
                return new Fraction(a.N * (b.D / common) + b.N * (a.D / common), a.D * (b.D / common));
            }
            public static Fraction operator -(Fraction a, Fraction b) => a + new Fraction(-b.N, b.D);
            internal Fraction Square() => new(N * N, D * D);
            internal Fraction Times(int n) => new(N * n, D);
        }
        private readonly int _length;
        private readonly PooledRingBuffer<double> _prices;
        private readonly PooledRingBuffer<(Fraction Up, Fraction Down)> _returns;
        private readonly Fraction _benchmark;
        private readonly Fraction? _beta;
        private readonly bool _potential;
        private readonly (Fraction Up, Fraction Down) _padding;
        private Fraction _up, _down;

        internal GeneratedTargetReturnState(int length, double benchmark, bool potential, int count, double? beta = null)
        {
            length = Math.Max(1, length); _length = length;
            if (MathHelper.IsValueNullOrInfinity(benchmark)) throw new ArgumentOutOfRangeException(nameof(benchmark));
            var target = Math.Pow(1 + benchmark, length / 360d) - 1;
            if (MathHelper.IsValueNullOrInfinity(target)) throw new ArgumentOutOfRangeException(nameof(benchmark), "The period benchmark must be finite and real.");
            _benchmark = new Fraction(ExactVarianceWindow.Units(target), System.Numerics.BigInteger.One << 1074);
            if (beta.HasValue)
            {
                if (MathHelper.IsValueNullOrInfinity(beta.Value)) throw new ArgumentOutOfRangeException(nameof(beta));
                _beta = new Fraction(ExactVarianceWindow.Units(beta.Value), System.Numerics.BigInteger.One << 1074);
            }
            _potential = potential; var capacity = Math.Min(length, Math.Max(1, count)); _prices = new(capacity); _returns = new(capacity);
            _padding = Contributions(default); Reset();
        }
        private (Fraction Up, Fraction Down) Contributions(Fraction value)
        {
            if (_beta.HasValue) return (value, default);
            var difference = value - _benchmark;
            return difference.N.Sign > 0 ? (difference, default) :
                (default, _potential ? difference.Square() : new Fraction(-difference.N, difference.D));
        }
        public double Update(in Bar bar) => Next(bar.Close, true);
        internal double Next(double price, bool commit)
        {
            var previous = _prices.Count == _length ? _prices[0] : 0;
            var relative = previous == 0 ? default : new Fraction(ExactVarianceWindow.Units(price) - ExactVarianceWindow.Units(previous), ExactVarianceWindow.Units(previous));
            var current = Contributions(relative);
            var expired = _returns.Count == _length ? _returns[0] : _padding;
            var up = _up + current.Up - expired.Up; var down = _down + current.Down - expired.Down;
            var result = down.N.IsZero ? 0 : _potential
                ? ExactPopulationDeviation.RootRatio((up.N * up.N * down.D) << 2148, up.D * up.D * _length * down.N)
                : ExactMeanAccumulator.UnitRatio((up.N * down.D) << 1074, up.D * down.N);
            if (_beta is { } beta)
            {
                var count = Math.Min(_returns.Count + 1, _length);
                var excess = new Fraction(up.N, up.D * count) - _benchmark;
                result = beta.N.IsZero ? 0 : ExactMeanAccumulator.UnitRatio((excess.N * beta.D * beta.N.Sign) << 1074, excess.D * System.Numerics.BigInteger.Abs(beta.N));
            }
            if (commit) { _prices.TryAdd(price, out _); _returns.TryAdd(current, out _); _up = up; _down = down; }
            return result;
        }
        public void Reset()
        { _prices.Clear(); _returns.Clear(); _up = _padding.Up.Times(_length); _down = _padding.Down.Times(_length); }
        public void Dispose() { _prices.Dispose(); _returns.Dispose(); }
    }

    private sealed class GeneratedTtmScalperState : IIndicatorState, IDisposable
    {
        private double _previous, _previous2, _previous3, _last;
        private bool _sell;
        public double Update(in Bar bar)
        {
            var sell = _previous < bar.Close && (_previous2 < _previous || _previous3 < _previous);
            var buy = _previous > bar.Close && (_previous2 > _previous || _previous3 > _previous);
            if (sell && !_sell) _last = bar.High;
            else if (buy && _sell) _last = bar.Low;
            _sell = sell ? true : buy ? false : _sell;
            _previous3 = _previous2; _previous2 = _previous; _previous = bar.Close;
            return _last;
        }
        public void Reset() { _previous = _previous2 = _previous3 = _last = 0; _sell = false; }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedTurboScalerState : IMultiOutputState, IDisposable
    {
        private readonly MacZWindow.Number _alpha;
        private readonly UnroundedMovingAverage _first, _second;
        private readonly Range _lineRange, _triggerRange;
        internal GeneratedTurboScalerState(int length, double alpha)
        {
            _alpha = MacZWindow.Number.Of(alpha);
            _first = new(MovingAvgType.SimpleMovingAverage, length); _second = new(MovingAvgType.SimpleMovingAverage, length);
            _lineRange = new(length); _triggerRange = new(length);
        }
        private MacZWindow.Number Blend(MacZWindow.Number value, MacZWindow.Number mean) => mean + _alpha * (value - mean);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = MacZWindow.Number.Of(bar.Close); var first = _first.Next(value, true); var second = _second.Next(first, true);
            outputs[0] = _lineRange.Next(value, Blend(value, first), true).Publish();
            outputs[1] = _triggerRange.Next(first, Blend(first, second), true).Publish();
        }
        public void Reset() { _first.Reset(); _second.Reset(); _lineRange.Reset(); _triggerRange.Reset(); }
        public void Dispose() { Reset(); _first.Dispose(); _second.Dispose(); }
        private sealed class Range
        {
            private readonly int _length; private long _index;
            private readonly LinkedList<(long Index, MacZWindow.Number Value)> _highs = new(), _lows = new();
            internal Range(int length) => _length = Math.Max(1, length);
            private MacZWindow.Number Extreme(LinkedList<(long Index, MacZWindow.Number Value)> deque, MacZWindow.Number value, bool maximum, bool final)
            {
                var expiry = _index - _length + 1; var first = deque.First;
                while (first is not null && first.Value.Index < expiry) first = first.Next;
                var result = first is null || (maximum ? (value - first.Value.Value).Sign > 0 : (value - first.Value.Value).Sign < 0) ? value : first.Value.Value;
                if (final)
                {
                    while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
                    while (deque.Last is { } last && (maximum ? (last.Value.Value - value).Sign <= 0 : (last.Value.Value - value).Sign >= 0)) deque.RemoveLast();
                    deque.AddLast((_index, value));
                }
                return result;
            }
            internal MacZWindow.Number Next(MacZWindow.Number value, MacZWindow.Number blend, bool final)
            {
                var high = Extreme(_highs, blend, true, final); var low = Extreme(_lows, blend, false, final); var width = high - low;
                var result = width.Sign == 0 ? default : (value - low).Divide(width);
                if (final) _index++; return result;
            }
            internal void Reset() { _index = 0; _highs.Clear(); _lows.Clear(); }
        }
    }

    private sealed class GeneratedUberTrendState : IIndicatorState, IDisposable
    {
        private readonly int _length;
        private readonly Queue<(MacZWindow.Number Up, MacZWindow.Number Down, MacZWindow.Number UpVolume, MacZWindow.Number DownVolume)> _history = new();
        private MacZWindow.Number _up, _down, _upVolume, _downVolume, _previousPrice;
        private bool _started;
        internal GeneratedUberTrendState(int length) => _length = Math.Max(1, length);
        public double Update(in Bar bar) => Next(bar.Close, bar.Volume, true);
        private double Next(double price, double volume, bool final)
        {
            Streaming.StreamingInputValidation.Finite(price, nameof(price)); Streaming.StreamingInputValidation.Finite(volume, nameof(volume));
            var current = MacZWindow.Number.Of(price); var change = _started ? current - _previousPrice : default;
            var advance = change.Sign > 0 ? change : default; var decline = change.Sign < 0 ? default(MacZWindow.Number) - change : default;
            var full = _history.Count == _length; var old = full ? _history.Peek() : default;
            var up = _up + advance - old.Up; var down = _down + decline - old.Down;
            var upContribution = change.Sign > 0 && up.Sign != 0 ? MacZWindow.Number.Of(volume).Divide(up) : default;
            var downContribution = change.Sign < 0 && down.Sign != 0 ? MacZWindow.Number.Of(volume).Divide(down) : default;
            var upVolume = _upVolume + upContribution - old.UpVolume; var downVolume = _downVolume + downContribution - old.DownVolume;
            var numerator = up * downVolume; var denominator = down * upVolume;
            // Preserve the nested quotient's defined zero-denominator cases before cross multiplication.
            var value = down.Sign == 0 || upVolume.Sign == 0 || downVolume.Sign == 0 ? MacZWindow.Number.Of(-1)
                : (numerator + denominator).Sign == 0 ? default : (numerator - denominator).Divide(numerator + denominator);
            if (final)
            {
                if (full) _history.Dequeue(); _history.Enqueue((advance, decline, upContribution, downContribution));
                _up = up; _down = down; _upVolume = upVolume; _downVolume = downVolume; _previousPrice = current; _started = true;
            }
            return value.Publish();
        }
        public void Reset() { _history.Clear(); _up = _down = _upVolume = _downVolume = _previousPrice = default; _started = false; }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedVaradiState(int length) : IIndicatorState, IDisposable
    {
        private readonly VaradiWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true).Value;
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedUhlState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.SimpleMovingAverageSmoother? _average = length <= count ? new(length) : null;
        private readonly Streaming.RollingStandardDeviation? _deviation = length <= count ? new(length) : null;
        private readonly double[] _lag = length <= count ? new double[length] : [];
        private int _next;
        private double _previousCma, _previousCts;
        private bool _hasPrevious;
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = bar.Close;
            double sma = _average?.Next(value, true) ?? 0;
            double deviation = _deviation?.Next(value, true) ?? 0;
            double previousDeviation = _lag.Length == 0 ? 0 : _lag[_next];
            double variance = previousDeviation * previousDeviation;
            double previousCma = _hasPrevious ? _previousCma : value;
            double previousCts = _hasPrevious ? _previousCts : value;
            double cmaDistance = MathHelper.Pow(sma - previousCma, 2);
            double ctsDistance = MathHelper.Pow(value - previousCts, 2);
            double ka = variance < cmaDistance && cmaDistance != 0 ? 1 - (variance / cmaDistance) : 0;
            double kb = variance < ctsDistance && ctsDistance != 0 ? 1 - (variance / ctsDistance) : 0;
            _previousCma = (ka * sma) + ((1 - ka) * previousCma);
            _previousCts = (kb * value) + ((1 - kb) * previousCts);
            if (_lag.Length != 0)
            {
                _lag[_next] = deviation;
                if (++_next == length) _next = 0;
            }
            _hasPrevious = true;
            outputs[0] = _previousCts; outputs[1] = _previousCma;
        }
        public void Reset()
        {
            _average?.Reset(); _deviation?.Reset();
            Array.Clear(_lag, 0, _lag.Length);
            _next = 0; _previousCma = _previousCts = 0; _hasPrevious = false;
        }
        public void Dispose() { _average?.Dispose(); _deviation?.Dispose(); }
    }

    private sealed class GeneratedUltimateAverageState : IIndicatorState, IDisposable
    {
        // The facade's obsolete Length property does not configure this calculation.
        private readonly UltimateAverageWindow _window = new(MovingAvgType.SimpleMovingAverage, 5, 50, 1);
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, bar.Volume, true);
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedUltimateBandsState(int minimum, int maximum, double multiplier) : IMultiOutputState, IDisposable
    {
        private readonly UltimateAverageWindow _average = new(MovingAvgType.SimpleMovingAverage, minimum, maximum, 1);
        private readonly UltimateBandWindow _bands = new(minimum, multiplier);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var center = _average.NextPoint(bar.Close, bar.High, bar.Low, bar.Volume, true);
            var point = _bands.Next(bar.Close, center, true);
            outputs[0] = point.Upper; outputs[1] = point.Middle; outputs[2] = point.Lower;
        }
        public void Reset() { _average.Reset(); _bands.Reset(); }
        public void Dispose() { _average.Dispose(); _bands.Reset(); }
    }

    private sealed class GeneratedVidyaAliasState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly ChandeMomentumWindow _momentum = new(Math.Min(Math.Max(1, length), Math.Max(1, count)));
        private readonly double _alpha = 2d / (Math.Max(1, length) + 1d);
        private double _previous;
        private bool _started;
        public double Update(in Bar bar)
        {
            double momentum = Math.Abs(_momentum.Next(bar.Close, true) / 100);
            _previous = VidyaBlend.Compute(_started ? _previous : bar.Close, bar.Close, _alpha * momentum);
            _started = true;
            return _previous;
        }
        public void Reset() { _momentum.Reset(); _previous = 0; _started = false; }
        public void Dispose() => _momentum.Dispose();
    }

    private sealed class GeneratedVmaState(int length) : IIndicatorState, IDisposable
    {
        private readonly Streaming.ExactVariableMovingAverageEngine _window = new(length);
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVariableBandsState(int length, double multiplier) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.ExactVariableMovingAverageEngine _line = new(length), _range = new(length);
        private readonly VariableAverageExpression _multiplier = VariableAverageExpression.Constant(UltimatePowerWeights.Fraction.Of(multiplier));
        private UltimatePowerWeights.Fraction _previousPrice;
        private bool _started;
        public void Update(in Bar bar, Span<double> outputs)
        {
            VariableBandWindow.Validate(bar.Close, bar.High, bar.Low);
            var price = UltimatePowerWeights.Fraction.Of(bar.Close);
            var trueRange = VariableBandWindow.TrueRange(bar.High, bar.Low, _started ? _previousPrice : price);
            var line = _line.NextExpression(price, true);
            var range = _range.NextExpression(trueRange, true);
            var width = _multiplier * range;
            outputs[0] = (line + width).Publish();
            outputs[1] = line.Publish();
            outputs[2] = (line - width).Publish();
            _previousPrice = price; _started = true;
        }
        public void Reset() { _line.Reset(); _range.Reset(); _previousPrice = default; _started = false; }
        public void Dispose() { _line.Dispose(); _range.Dispose(); _previousPrice = default; _started = false; }
    }

    private sealed class GeneratedWaddahState : IMultiOutputState, IDisposable
    {
        private readonly Ema _fast, _slow, _lagFast, _lagSlow;
        private readonly MacZWindow.Number _sensitivity;
        private readonly int _length;
        private readonly Queue<double> _history = new();
        private System.Numerics.BigInteger _sum, _squares;
        private MacZWindow.Number _p1, _p2, _p3;
        public GeneratedWaddahState(int fast, int slow, double sensitivity)
        {
            Streaming.StreamingInputValidation.Finite(sensitivity, nameof(sensitivity));
            _sensitivity = MacZWindow.Number.Of(sensitivity); _length = Math.Max(1, fast);
            _fast = new(fast); _slow = new(slow); _lagFast = new(fast); _lagSlow = new(slow);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double price = bar.Close;
            Streaming.StreamingInputValidation.Finite(price, nameof(price));
            var p = MacZWindow.Number.Of(price); var delta = p - _p1; var lagDelta = _p2 - _p3;
            // Exact linear filters of differences equal the difference of separately lagged MACDs.
            var t1 = (_fast.Next(delta, true) - _slow.Next(delta, true)) * _sensitivity;
            var t2 = (_lagFast.Next(lagDelta, true) - _lagSlow.Next(lagDelta, true)) * _sensitivity;
            var units = ExactVarianceWindow.Units(price); var sum = _sum + units; var squares = _squares + units * units;
            if (_history.Count == _length) { var old = ExactVarianceWindow.Units(_history.Peek()); sum -= old; squares -= old * old; }
            var moment = _history.Count < _length - 1 ? System.Numerics.BigInteger.Zero : _length * squares - sum * sum;
            var denominator = (System.Numerics.BigInteger)_length * _length;
            var width = ExactPopulationDeviation.RootRatio(16 * moment, denominator);
            var up = t1.Sign > 0 ? t1 : default; var down = t1.Sign < 0 ? t1.Times(-1) : default;
            if (_history.Count == _length) _history.Dequeue();
            _history.Enqueue(price); _sum = sum; _squares = squares;
            _p3 = _p2; _p2 = _p1; _p1 = p;
            outputs[0] = t1.Publish(); outputs[1] = t2.Publish(); outputs[2] = width;
            outputs[3] = up.Publish(); outputs[4] = down.Publish();
        }
        public void Reset()
        {
            _fast.Reset(); _slow.Reset(); _lagFast.Reset(); _lagSlow.Reset(); _history.Clear(); _sum = _squares = default;
            _p1 = _p2 = _p3 = default;
        }
        public void Dispose() => Reset();

        private sealed class Ema
        {
            private readonly int _length; private long _count; private MacZWindow.Number _sum, _previous;
            internal Ema(int length) => _length = Math.Max(1, length);
            internal MacZWindow.Number Next(MacZWindow.Number value, bool final)
            {
                var sum = _sum; MacZWindow.Number result;
                if (_count < _length) { sum += value; result = sum.Divide(_count + 1); }
                else result = (_previous.Times(_length - 1L) + value.Times(2)).Divide(_length + 1L);
                if (final) { _sum = sum; _previous = result; _count++; }
                return result;
            }
            internal void Reset() { _count = 0; _sum = _previous = default; }
        }
    }

    private sealed class GeneratedWaveTrendState(int channel) : IMultiOutputState, IDisposable
    {
        private readonly WaveTrendWindow _window = new(MovingAvgType.ExponentialMovingAverage, channel, 21, 4);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var price = WaveTrendWindow.Price(bar.Open, bar.High, bar.Low, bar.Close);
            var point = _window.Next(price, true);
            outputs[0] = point.Line; outputs[1] = point.SignalLine;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVolatilityAverageState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly VolatilityAverageWindow.Average _mean = new(MovingAvgType.SimpleMovingAverage, 10);
        private readonly VolatilityAverageWindow.Average _scores = new(MovingAvgType.SimpleMovingAverage, 3);
        private readonly VolatilityAverageWindow.Average _output = new(MovingAvgType.SimpleMovingAverage, 3);
        private readonly Queue<PeriodicChannelWindow.Number> _moments = new();
        private readonly PeriodicChannelWindow.Number[] _history = new PeriodicChannelWindow.Number[Math.Min(length, count)];
        private PeriodicChannelWindow.Number _sum, _squares;
        private int _next, _stored;
        public double Update(in Bar bar)
        {
            Streaming.StreamingInputValidation.Finite(bar.Close, "price");
            var value = PeriodicChannelWindow.Number.Of(bar.Close);
            var mean = _mean.Next(VolatilityAverageWindow.Radical.Of(value), true).Rational();
            bool full = _moments.Count == 10;
            var expired = full ? _moments.Peek() : default;
            int size = full ? 10 : _moments.Count + 1;
            var sum = _sum + value - expired;
            var squares = _squares + value * value - expired * expired;
            var variance = (squares.Times(size) - sum * sum).Divide((long)size * size);
            if (full) _moments.Dequeue();
            _moments.Enqueue(value); _sum = sum; _squares = squares;
            var score = VolatilityAverageWindow.Radical.QuotientRoot((value - mean).Times(100), variance);
            int period = VolatilityAverageWindow.Period(_scores.Next(score, true), length, 10);
            var weighted = value.Times(period);
            int index = _next;
            for (int lag = 1; lag < period && lag <= _stored; lag++)
            {
                if (--index < 0) index = _history.Length - 1;
                weighted += _history[index].Times(period - lag);
            }
            var average = weighted.Divide((long)period * (period + 1L) / 2);
            var line = _output.Next(VolatilityAverageWindow.Radical.Of(average), true).Rational();
            if (_history.Length != 0)
            {
                _history[_next] = value;
                if (++_next == _history.Length) _next = 0;
                if (_stored < _history.Length) _stored++;
            }
            return line.Publish();
        }
        public void Reset()
        {
            _mean.Reset(); _scores.Reset(); _output.Reset(); _moments.Clear();
            Array.Clear(_history, 0, _history.Length); _sum = _squares = default; _next = _stored = 0;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedZweigState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly sbyte[] _votes = new sbyte[Math.Min(length, count)];
        private readonly Streaming.IMovingAverageSmoother _average = Streaming.MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, length);
        private int _next, _stored, _advances, _declines;
        private double _previous;
        public double Update(in Bar bar)
        {
            sbyte vote = bar.Close > _previous ? (sbyte)1 : bar.Close < _previous ? (sbyte)-1 : (sbyte)0;
            if (_votes.Length != 0)
            {
                if (_stored == _votes.Length)
                {
                    if (_votes[_next] > 0) _advances--;
                    else if (_votes[_next] < 0) _declines--;
                }
                else _stored++;
                _votes[_next] = vote;
                if (++_next == _votes.Length) _next = 0;
            }
            if (vote > 0) _advances++;
            else if (vote < 0) _declines++;
            // Binary flags and their totals are exact in binary64 at every supported period.
            int total = _advances + _declines;
            double ratio = total != 0 ? (double)_advances / total : 0;
            double result = _average.Next(ratio, true);
            _previous = bar.Close;
            return result;
        }
        public void Reset()
        {
            Array.Clear(_votes, 0, _votes.Length); _next = _stored = _advances = _declines = 0;
            _previous = 0; _average.Reset();
        }
        public void Dispose() => _average.Dispose();
    }

    private sealed class GeneratedZeroLagCycleState(int length) : IMultiOutputState, IDisposable
    {
        private readonly ZeroLagCycleWindow _window = new(length);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.Filter;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVostroState(int length1, int length2, double level, int count) : IIndicatorState, IDisposable
    {
        private readonly PooledRingBuffer<double> _prices = new(Math.Min(length1, Math.Max(1, count)));
        private readonly PooledRingBuffer<double> _ranges = new(Math.Min(length1, Math.Max(1, count)));
        private readonly double[] _weightedHistory = new double[Math.Min(length2, count)];
        private readonly long _denominator = (long)length2 * (length2 + 1L) / 2;
        private ExactMeanAccumulator _sum, _numerator;
        private int _stored, _next;
        private double _previousLow, _previousHigh, _priceSum, _rangeSum;
        public double Update(in Bar bar)
        {
            // Preserve WmaState's configured weights and zero-padded startup denominator.
            _numerator.Subtract(_sum); _numerator.Add(bar.Close, length2);
            double average = _numerator.Mean(_denominator);
            _sum.Add(bar.Close);
            if (_stored == length2) _sum.Add(_weightedHistory[_next], -1);
            if (_weightedHistory.Length != 0)
            {
                _weightedHistory[_next] = bar.Close;
                if (++_next == _weightedHistory.Length) _next = 0;
                if (_stored < _weightedHistory.Length) _stored++;
            }
            // Match the verified Vostro arm's accumulation order and overflow persistence.
            _priceSum += bar.Close;
            _rangeSum += bar.High - bar.Low;
            if (_prices.TryAdd(bar.Close, out var removedPrice)) _priceSum -= removedPrice;
            if (_ranges.TryAdd(bar.High - bar.Low, out var removedRange)) _rangeSum -= removedRange;
            double center = _priceSum * .2;
            double scale = _rangeSum * .04;
            double low = scale != 0 ? (bar.Low - center) / scale : 0;
            double high = scale != 0 ? (bar.High - center) / scale : 0;
            double candidate = high > level && bar.High > average ? 90 : low < -level && bar.Low < average ? -90 : 0;
            double result = (high > level && _previousHigh > level) || (low < -level && _previousLow < -level) ? 0 : candidate;
            _previousLow = low; _previousHigh = high;
            return result;
        }
        public void Reset()
        {
            _prices.Clear(); _ranges.Clear(); Array.Clear(_weightedHistory, 0, _weightedHistory.Length);
            _sum = _numerator = default; _stored = _next = 0; _previousLow = _previousHigh = _priceSum = _rangeSum = 0;
        }
        public void Dispose() { _prices.Dispose(); _ranges.Dispose(); }
    }

    private sealed class GeneratedWilliamsFractalState(int length, int count, bool upper) : IIndicatorState, IDisposable
    {
        private readonly double[] _history = new double[(int)Math.Min((long)length + 8, count)];
        private int _index, _next;
        public double Update(in Bar bar)
        {
            double current = upper ? bar.High : bar.Low;
            int older = Math.Min(6, _index - length);
            double result = 0;
            if (older >= 2)
            {
                Span<double> neighborhood = stackalloc double[9];
                for (int j = 0; j < older + 3; j++)
                {
                    int offset = length + older - j;
                    if (offset == 0) neighborhood[j] = current;
                    else
                    {
                        int slot = _next - offset;
                        if (slot < 0) slot += _history.Length;
                        neighborhood[j] = _history[slot];
                    }
                }
                result = WilliamsFractalPattern.IsFractal(neighborhood.Slice(0, older + 3), upper) ? 1 : 0;
            }
            if (_history.Length != 0)
            {
                _history[_next] = current;
                if (++_next == _history.Length) _next = 0;
            }
            _index++;
            return result;
        }
        public void Reset() { Array.Clear(_history, 0, _history.Length); _index = _next = 0; }
        public void Dispose() { }
    }

    private sealed class GeneratedDailyPivotState(bool standard) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.DailyPivotLevels _daily = new(standard);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var levels = _daily.Next(bar.Time, bar.Open, bar.High, bar.Low, bar.Close, true);
            levels.AsSpan().CopyTo(outputs);
        }
        public void Reset() => _daily.Reset();
        public void Dispose() { }
    }

    private sealed class GeneratedWoodiePivotState : IMultiOutputState, IDisposable
    {
        private readonly Streaming.DailyPivotLevels _daily = new(false, woodie: true);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var levels = _daily.Next(bar.Time, bar.Open, bar.High, bar.Low, bar.Close, true);
            levels.AsSpan().CopyTo(outputs);
        }
        public void Reset() => _daily.Reset();
        public void Dispose() { }
    }

    private sealed class GeneratedAdaptiveCyberState(int length, double alpha) : IMultiOutputState, IDisposable
    {
        private readonly AdaptiveCyberWindow _window = new(length, alpha);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Cycle;
            outputs[1] = value.Period;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedAdaptiveGravityState(int length) : IIndicatorState, IDisposable
    {
        private readonly AdaptiveGravityWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedSmoothedAdaptiveMomentumState(int first, int second, MovingAvgType average) : IMultiOutputState, IDisposable
    {
        private readonly SmoothedAdaptiveMomentumWindow _window = new(first, second, average);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedAdaptiveRsiV1State(double fraction, bool fisher) : IMultiOutputState, IDisposable
    {
        private readonly AdaptiveRsiV1Window _window = new(fraction, fisher);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Value;
            if (!fisher) outputs[1] = value.Average;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedAdaptiveRangeV1State(double fraction, bool commodity, double constant) : IMultiOutputState, IDisposable
    {
        private readonly AdaptiveRangeV1Window _window = new(fraction, commodity, constant);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var price = commodity ? CommodityIndexWindow.TypicalPrice(bar.High, bar.Low, bar.Close) : bar.Close;
            var value = _window.Next(price, bar.High, bar.Low, bar.Close, true);
            outputs[0] = value.Value;
            outputs[1] = value.Average;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedRoofAutocorrelationState(int length, int smooth) : IIndicatorState, IDisposable
    {
        private readonly RoofAutocorrelationWindow _window = new(length, smooth);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedAutocorrelationSpectrumState(int upper, int lower, int firstLag) : IIndicatorState, IDisposable
    {
        private readonly AutocorrelationSpectrumWindow _window = new(upper, lower, firstLag);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedAdaptiveBandPassState(int upper, int lower, int firstLag, double bandwidth) : IMultiOutputState, IDisposable
    {
        private readonly AdaptiveBandPassWindow _window = new(upper, lower, firstLag, bandwidth);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Value;
            outputs[1] = value.Trigger;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedZeroCrossingCycleState(int length, double bandwidth) : IIndicatorState, IDisposable
    {
        private readonly ZeroCrossingCycleWindow _window = new(length, bandwidth);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Value;
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedAdaptiveRsiV2State(int upper, int lower, int firstLag, bool fisher) : IMultiOutputState, IDisposable
    {
        private readonly AdaptiveRsiV2Window _window = new(upper, lower, firstLag, MovingAvgType.ExponentialMovingAverage);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            if (fisher)
            {
                var transformed = 2 * (value.Value - .5);
                var amplified = MathHelper.MinOrMax(1.5 * transformed, .999, -.999);
                outputs[0] = .5 * Math.Log((1 + amplified) / (1 - amplified));
            }
            else { outputs[0] = value.Value; outputs[1] = value.Average; }
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedAdaptiveRangeV2State(int upper, int lower, int firstLag, bool commodity, bool inverseFisher) : IMultiOutputState, IDisposable
    {
        private readonly AdaptiveRangeV2Window _window = new(upper, lower, firstLag, MovingAvgType.ExponentialMovingAverage, commodity);
        private double _previousInverse;
        public void Reset() { _window.Reset(); _previousInverse = 0; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            if (inverseFisher)
            {
                var inverse = Math.Tanh(6 * (value.Value - .5));
                outputs[0] = inverse;
                outputs[1] = .9 * _previousInverse;
                _previousInverse = inverse;
            }
            else { outputs[0] = value.Value; outputs[1] = value.Average; }
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedEvenBetterSineState(int high, int low) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.EvenBetterSineWaveKernel _window = new(high, low);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => outputs[0] = _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedReverseEmaState(double alpha) : IMultiOutputState, IDisposable
    {
        private readonly ReverseEmaWindow _window = new(alpha);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => outputs[0] = _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedReverseEmaPairState(double trend, double cycle) : IMultiOutputState, IDisposable
    {
        private readonly ReverseEmaWindow _trend = new(trend), _cycle = new(cycle);
        public void Reset() { _trend.Reset(); _cycle.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var trendValue = _trend.Next(bar.Close, true);
            outputs[0] = _cycle.Next(bar.Close, true);
            outputs[1] = trendValue;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedUniversalOscillatorState(int length) : IMultiOutputState, IDisposable
    {
        private readonly UniversalOscillatorWindow _window = new(MovingAvgType.ExponentialMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedUniversalTradingState(int length, int rms, double parameter, bool snake) : IMultiOutputState, IDisposable
    {
        private readonly UniversalTradingWindow _window = new(MovingAvgType.EhlersHannMovingAverage, length, rms, parameter, snake);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = snake ? value.Upper : value.Line;
            outputs[1] = snake ? value.Line : value.Upper;
            outputs[2] = value.Lower;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTruncatedBandState(int length, int cutoff, double bandwidth) : IMultiOutputState, IDisposable
    {
        private readonly TruncatedBandPassWindow _window = new(length, cutoff, bandwidth);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs) => outputs[0] = _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedVossState(int length, double predict, double bandwidth) : IMultiOutputState, IDisposable
    {
        private readonly VossPredictiveWindow _window = new(length, predict, bandwidth);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, true);
            outputs[0] = value.Voss;
            outputs[1] = value.Filter;
        }
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedAmDetectorState(int envelope, int smooth) : IMultiOutputState, IDisposable
    {
        private readonly AmDetectorWindow _window = new(MovingAvgType.SimpleMovingAverage, envelope, smooth);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Open, bar.Close, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedEhlersKaufmanState(int length) : IIndicatorState, IDisposable
    {
        private readonly EhlersKaufmanWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedSimpleCycleIndicatorState(double alpha) : IIndicatorState, IDisposable
    {
        private readonly SimpleCycleIndicatorWindow _window = new(alpha);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedEhlersDistanceState(int length) : IIndicatorState, IDisposable
    {
        private readonly EhlersDistanceFilterWindow _window = new(length, 5);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedAverageErrorState(int length) : IIndicatorState, IDisposable
    {
        private readonly AverageErrorWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedDistanceCoefficientState(int length) : IIndicatorState, IDisposable
    {
        private readonly DistanceCoefficientWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedTrendflexState(int length, bool reflex) : IIndicatorState, IDisposable
    {
        private readonly TrendflexWindow _window = new(length, reflex);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedDeviationScaledState(int fast, int slow, bool fisher) : IIndicatorState, IDisposable
    {
        private readonly DeviationScaledWindow _window = new(fast, slow);
        private double _previous;
        public void Reset() { _window.Reset(); _previous = 0; }
        public double Update(in Bar bar)
        {
            var value = _window.Next(bar.Close, true, out _);
            if (!fisher) return value;
            if (Math.Abs(value) < 2) _previous = FisherArithmetic.Transform(value / 2);
            return _previous;
        }
        public void Dispose() => Reset();
    }

    private sealed class GeneratedDeviationSuperState(int length) : IIndicatorState, IDisposable
    {
        private readonly DeviationSuperSmootherWindow _window = new(MovingAvgType.EhlersHannMovingAverage, length, 50);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Reset();
    }

    private sealed class GeneratedErgodicCandleState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly ErgodicCandleWindow _window = new(MovingAvgType.ExponentialMovingAverage, 32, length, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, bar.Open, bar.High, bar.Low, true);
            outputs[0] = point.Eco; outputs[1] = point.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedErgodicStrengthPairState(int[] first, int[] second, int signal, int count) : IMultiOutputState, IDisposable
    {
        private readonly StrengthWindow _first = new(MovingAvgType.ExponentialMovingAverage, first, count);
        private readonly StrengthWindow _second = new(MovingAvgType.ExponentialMovingAverage, second, count);
        private readonly StrengthAverage _signal = new(MovingAvgType.ExponentialMovingAverage, signal, count);
        public void Reset() { _first.Reset(); _second.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs[0] = _first.Next(bar.Close, true);
            var value = _second.Next(bar.Close, true);
            outputs[1] = value; outputs[2] = _signal.Next(new StrengthValue(value), true).Mantissa;
        }
        public void Dispose() { _first.Dispose(); _second.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedAdaptiveErgodicCandleState(int smooth, int stochastic, int signal, int count) : IMultiOutputState, IDisposable
    {
        private readonly AdaptiveCandleWindow _window = new(MovingAvgType.ExponentialMovingAverage, smooth, stochastic, signal, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Open, bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Eco.Publish(); outputs[1] = point.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedErgodicSelectionState(int length, int smooth, double pointValue) : IMultiOutputState, IDisposable
    {
        private readonly ErgodicSelectionWindow _window = new(MovingAvgType.WildersSmoothingMethod, length, smooth, pointValue);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Line; outputs[1] = point.SignalLine;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedMeanErrorState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly ExactStandardErrorWindow? _window = Math.Max(1, length) <= count ? new(length, false) : null;
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window?.Next(bar.Close, true) ?? 0;
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedErgodicDeviationState(int mean, int first, int second, int signal) : IMultiOutputState, IDisposable
    {
        private readonly ResidualAverageWindow _window = new(MovingAvgType.ExponentialMovingAverage, mean, MovingAvgType.ExponentialMovingAverage, first, second, signal);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Value;
            outputs[1] = point.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedHistoricalRankState(int length, int annualLength) : IMultiOutputState, IDisposable
    {
        private readonly int _length = Math.Max(1, length), _annual = Math.Max(1, annualLength);
        private readonly Streaming.RollingWindowSum _logs = new(Math.Max(1, length));
        private readonly Streaming.RollingWindowSum _residuals = new(Math.Max(1, length));
        private readonly Streaming.EmaState _signal = new(length);
        private RollingOrderStatistic _order = new(Math.Max(1, annualLength));
        private double _previous;

        public void Update(in Bar bar, Span<double> outputs)
        {
            var log = StableLogRatio.OfSameSign(bar.Close, _previous);
            var sum = _logs.Add(log, out var count);
            var residual = log - sum / count;
            var squares = _residuals.Add(residual * residual, out _);
            var volatility = _length > 1 ? Math.Sqrt(Math.Max(0, squares / (_length - 1))) * Math.Sqrt(_annual) : 0;
            _order.Add(volatility);
            var rank = (double)_order.CountLessThan(VolatilityRank.StrictBoundary(volatility)) / _annual * 100;
            outputs[0] = rank;
            outputs[1] = _signal.GetNext(rank, true);
            _previous = bar.Close;
        }

        public void Reset()
        {
            _logs.Reset(); _residuals.Reset(); _signal.Reset(); _previous = 0;
            _order.Dispose(); _order = new(_annual);
        }
        public void Dispose() { _logs.Dispose(); _residuals.Dispose(); _order.Dispose(); }
    }

    private sealed class GeneratedGarmanKlassState(int length, int count) : IMultiOutputState, IDisposable
    {
        private readonly OhlcVolatilityWindow _window = new(OhlcVolatilityWindow.Estimator.GarmanKlass, Math.Max(1, length), count);
        private readonly StrengthAverage _signal = new(MovingAvgType.WeightedMovingAverage, 7, count);
        public void Reset() { _window.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(in bar);
            outputs[0] = value;
            outputs[1] = _signal.Next(new StrengthValue(value), true).Mantissa;
        }
        public void Dispose() { _window.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedOhlcVolatilityState(OhlcVolatilityWindow.Estimator estimator, int length, int count) : IIndicatorState, IDisposable
    {
        private readonly OhlcVolatilityWindow _window = new(estimator, length, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(in bar);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTypicalVolatilityState(int length, int count) : IIndicatorState, IDisposable
    {
        private readonly ExactTypicalVolatilityWindow? _window = length <= count ? new(length) : null;
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window?.Next(bar.High, bar.Low, bar.Close, true) ?? 0;
        public void Dispose() => _window?.Dispose();
    }

    // AnnualizationFactor is ignored by the legacy residual-volatility facade.
    private sealed class GeneratedResidualVolatilityState(int length) : IMultiOutputState, IDisposable
    {
        private readonly ResidualVolatilityWindow _window = new(MovingAvgType.SimpleMovingAverage, length);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Deviation;
            outputs[1] = point.Variance;
            outputs[2] = point.SignalLine;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedClosedDistanceState(int length) : IIndicatorState, IDisposable
    {
        private readonly ClosedFormDistanceWindow _window = new(MovingAvgType.ExponentialMovingAverage, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Value;
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedVolatilityWaveState(int length) : IIndicatorState, IDisposable
    {
        private readonly VolatilityWaveWindow _window = new(MovingAvgType.WeightedMovingAverage, length, 2.5);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVolatilityRatioState(int length) : IIndicatorState
    {
        private readonly VolatilityRatioWindow _window = new(length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true);
    }

    private sealed class GeneratedVolatilitySwitchState(int length) : IIndicatorState, IDisposable
    {
        private readonly VolatilitySwitchWindow _window = new(MovingAvgType.WeightedMovingAverage, length);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Value(bar.Close, true);
        public void Dispose() { _window.Reset(); _window.Dispose(); }
    }

    private sealed class GeneratedCloseVolatilityState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.RollingStandardDeviation? _deviation = period <= count ? new(period) : null;
        private double _previous;
        public void Reset() { _deviation?.Reset(); _previous = 0; }
        public double Update(in Bar bar)
        {
            // Include the startup zero, then retain the batch's translated two-pass deviation.
            var result = (_deviation?.Next(StableLogRatio.OfSameSign(bar.Close, _previous), true) ?? 0) * Math.Sqrt(252);
            _previous = bar.Close;
            return result;
        }
        public void Dispose() => _deviation?.Dispose();
    }

    private sealed class GeneratedDownsideDeviationState(int period, int count) : IIndicatorState, IDisposable
    {
        // A full return window needs period preceding prices plus the current price.
        private readonly ExactDownsideWindow? _window = period < count ? new(period) : null;
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window?.Next(bar.Close, true) ?? 0;
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedHistoricalVolatilityState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.RollingStandardDeviation? _deviation = period <= count ? new(period) : null;
        private double _previous;
        private int _seen;
        public void Reset() { _deviation?.Reset(); _previous = 0; _seen = 0; }
        public double Update(in Bar bar)
        {
            double deviation = _deviation?.Next(StableLogRatio.OfSameSign(bar.Close, _previous), true) ?? 0;
            _previous = bar.Close;
            if (_seen < period) { _seen++; return 0; }
            return 100 * deviation * MathHelper.Sqrt(365);
        }
        public void Dispose() => _deviation?.Dispose();
    }

    // These formulas publish zero until a complete period is available. Avoid
    // renting a window that cannot fill during this owned finite execution.
    private sealed class GeneratedVarianceState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly ExactVarianceWindow? _window = Math.Max(1, period) <= count ? new(period) : null;
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window?.Next(bar.Close, true) ?? 0;
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedCoefficientState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly ExactCoefficientWindow? _window = Math.Max(1, period) <= count ? new(period) : null;
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window?.Next(bar.Close, true) ?? 0;
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedStandardErrorState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly ExactStandardErrorWindow? _window = Math.Max(1, period) <= count ? new(period, regression: true) : null;
        public void Reset() => _window?.Reset();
        public double Update(in Bar bar) => _window?.Next(bar.Close, true) ?? 0;
        public void Dispose() => _window?.Dispose();
    }

    private sealed class GeneratedChaikinVolatilityState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly ChaikinVolatilityWindow _window = new(MovingAvgType.ExponentialMovingAverage, period, 12, count);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedDrawdownState(int period, int count) : IIndicatorState, IDisposable
    {
        // No window can evict an observation beyond this owned finite input.
        private readonly DrawdownWindow _window = new(Math.Min(Math.Max(1, period), Math.Max(1, count)));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChoppinessState(int period) : IIndicatorState
    {
        private readonly ChoppinessWindow _window = new(period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true);
    }

    private sealed class GeneratedVwapState : IIndicatorState
    {
        private ExactVolumeMean _mean;
        public void Reset() => _mean = default;
        public double Update(in Bar bar)
        {
            _mean.AddTypical(bar.High, bar.Low, bar.Close, bar.Volume);
            return _mean.Value();
        }
    }

    private sealed class GeneratedAroonLegState(int period, bool maximum) : IIndicatorState
    {
        // These facades include both endpoints and blank the first period bars.
        private readonly WindowExtremeDeque _window = new((long)period + 1, maximum);
        private long _index;
        public void Reset() { _window.Reset(); _index = 0; }
        public double Update(in Bar bar)
        {
            _window.Add(maximum ? bar.High : bar.Low);
            double value = _index < period ? 0 : 100.0 * (period - (_index - _window.Index)) / period;
            _index++;
            return value;
        }
    }

    private sealed class GeneratedMassState(int averagePeriod, int sumPeriod, int count) : IMultiOutputState, IDisposable
    {
        private readonly MassIndexWindow _window = new(MovingAvgType.ExponentialMovingAverage, averagePeriod, averagePeriod, sumPeriod, 9, count);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.High, bar.Low, true);
            outputs[0] = value.Value;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedChaikinOscillatorState(int fastPeriod, int slowPeriod) : IIndicatorState, IDisposable
    {
        private readonly MoneyFlowAverageWindow _window = new(MovingAvgType.ExponentialMovingAverage, fastPeriod, slowPeriod);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, bar.Volume, true).Signal;
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedVolumeRatioState(int period, int count) : IIndicatorState
    {
        private readonly GeneratedRatioState _ratio = new(period, count);
        public void Reset() => _ratio.Reset();
        public double Update(in Bar bar) => _ratio.Update(new Bar(default, 0, 0, 0, bar.Volume, 0));
    }

    private sealed class GeneratedVolumeTrendState(int period) : IMultiOutputState, IDisposable
    {
        private readonly PriceVolumeTrendWindow _window = new(MovingAvgType.ExponentialMovingAverage, period);
        public void Reset() => _window.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _window.Next(bar.Close, bar.Volume, true);
            outputs[0] = value.Line;
            outputs[1] = value.Signal;
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedMoneyFlowState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly Streaming.RollingMoneyFlowIndex _window = new(Math.Min(Math.Max(1, period), Math.Max(1, count)));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(
            Streaming.RollingMoneyFlowIndex.TypicalPrice(bar.High, bar.Low, bar.Close), bar.Volume, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedForceState(int period) : IIndicatorState, IDisposable
    {
        private readonly ForceWindow _window = new(MovingAvgType.ExponentialMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Volume, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedObvState(int period) : IMultiOutputState
    {
        private readonly Streaming.EmaState _signal = new(period);
        private ExactMeanAccumulator _sum;
        private double _previous;
        private bool _invalidSignal;
        public void Reset() { _signal.Reset(); _sum = default; _previous = 0; _invalidSignal = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (bar.Close > _previous) _sum.Add(bar.Volume);
            else if (bar.Close < _previous) _sum.Add(bar.Volume, -1);
            _previous = bar.Close;
            double value = _sum.Mean(1);
            _invalidSignal |= !double.IsFinite(value);
            outputs[0] = value;
            outputs[1] = _invalidSignal ? double.NaN : _signal.GetNext(value, commit: true);
        }
    }

    private sealed class GeneratedDetrendedState : IIndicatorState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother _mean;
        private readonly double[] _lag;
        private readonly int _lookback;
        private int _position, _seen;
        internal GeneratedDetrendedState(int period, int count)
        {
            _lookback = MathHelper.MinOrMax((int)Math.Ceiling(period / 2.0 + 1));
            _lag = new double[Math.Min(_lookback, Math.Max(1, count))];
            _mean = new(period);
        }
        public void Reset() { _mean.Reset(); _position = _seen = 0; }
        public double Update(in Bar bar)
        {
            double previous = _seen >= _lookback ? _lag[_position] : 0;
            _lag[_position] = bar.Close;
            if (++_position == _lag.Length) _position = 0;
            if (_seen < _lookback) _seen++;
            return previous - _mean.Next(bar.Close, true);
        }
        public void Dispose() => _mean.Dispose();
    }

    private sealed class GeneratedEmaGapState(int fastPeriod, int slowPeriod, bool percentage = false) : IIndicatorState
    {
        private readonly Streaming.EmaState _fast = new(fastPeriod);
        private readonly Streaming.EmaState _slow = new(slowPeriod);
        public void Reset() { _fast.Reset(); _slow.Reset(); }
        public double Update(in Bar bar)
        {
            double fast = _fast.GetNext(bar.Close, commit: true);
            double slow = _slow.GetNext(bar.Close, commit: true);
            return percentage ? RoundedPercentageChange.Of(fast, slow) : fast - slow;
        }
    }

    private sealed class GeneratedEmaGapSignalState(int fastPeriod, int slowPeriod, int signalPeriod, bool percentage = false) : IMultiOutputState
    {
        private readonly GeneratedEmaGapState _line = new(fastPeriod, slowPeriod, percentage);
        private readonly Streaming.EmaState _signal = new(signalPeriod);
        private bool _invalidSignal;
        public void Reset() { _line.Reset(); _signal.Reset(); _invalidSignal = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = _line.Update(bar);
            _invalidSignal |= double.IsInfinity(value);
            double signal = _invalidSignal ? double.NaN : _signal.GetNext(value, commit: true);
            outputs[0] = value;
            outputs[1] = signal;
            outputs[2] = value - signal;
        }
    }

    private sealed class GeneratedChandeState(int period, int count, int signalLength = 3) : IMultiOutputState, IDisposable
    {
        private readonly ChandeMomentumWindow _window = new(Math.Min(Math.Max(1, period), Math.Max(1, count)));
        private readonly Streaming.EmaState _signal = new(signalLength);
        public void Reset() { _window.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = _window.Next(bar.Close, true);
            outputs[0] = value;
            outputs[1] = _signal.GetNext(value, commit: true);
        }
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedTrixState(int period) : IIndicatorState, IDisposable
    {
        private readonly TrixWindow _window = new(MovingAvgType.ExponentialMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true).Publish();
        public void Dispose() => _window.Dispose();
    }

    private sealed class GeneratedMomentumState(int period, int count) : IMultiOutputState
    {
        private readonly GeneratedRatioState _ratio = new(period, count, momentum: true);
        private readonly WmaValueState _signal = new(period, count);
        private bool _invalidSignal;
        public void Reset() { _ratio.Reset(); _signal.Reset(); _invalidSignal = false; }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = _ratio.Update(bar);
            _invalidSignal |= !double.IsFinite(value);
            outputs[0] = value;
            // FiniteSignalInput invalidates the entire dependent suffix.
            outputs[1] = _invalidSignal ? double.NaN
                : _signal.Update(new Bar(default, 0, 0, 0, value, 0));
        }
    }

    private sealed class GeneratedRsiState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly PriceRsiWindow _line = new(MovingAvgType.WildersSmoothingMethod, period, count);
        private readonly StrengthAverage _signal = new(MovingAvgType.WildersSmoothingMethod, 3, count);
        public void Reset() { _line.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            double value = _line.Next(bar.Close, true);
            double signal = _signal.Next(new StrengthValue(value), true).Mantissa;
            outputs[0] = value;
            outputs[1] = signal;
            outputs[2] = value - signal;
        }
        public void Dispose() { _line.Dispose(); _signal.Dispose(); }
    }

    private sealed class GeneratedAtrState(int period, int count) : IIndicatorState, IDisposable
    {
        private readonly Helpers.KeltnerWindow _window = new(MovingAvgType.WildersSmoothingMethod, 1, period,
            MovingAvgType.WildersSmoothingMethod, Math.Max(1, count));
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.High, bar.Low, bar.Close, true).Atr.Publish();
        public void Dispose() => _window.Dispose();
    }

    private sealed class WilderValueState(int period) : IIndicatorState
    {
        private readonly Streaming.WilderState _state = new(period);
        public void Reset() => _state.Reset();
        public double Update(in Bar bar) => _state.GetNext(bar.Close, commit: true);
    }

    private sealed class ExtrapolatedEmaValueState(int period, bool triple) : IIndicatorState
    {
        private readonly Streaming.EmaState _first = new(period);
        private readonly Streaming.EmaState _second = new(period);
        private readonly Streaming.EmaState? _third = triple ? new(period) : null;
        public void Reset() { _first.Reset(); _second.Reset(); _third?.Reset(); }
        public double Update(in Bar bar)
        {
            double first = _first.GetNext(bar.Close, commit: true);
            double second = _second.GetNext(first, commit: true);
            return _third is null ? ExponentialExtrapolation.Double(first, second)
                : ExponentialExtrapolation.Triple(first, second, _third.GetNext(second, commit: true));
        }
    }

    private sealed class GeneratedTriangularState : IIndicatorState, IDisposable
    {
        private readonly Streaming.RoundedSimpleMovingAverageSmoother _first;
        private readonly Streaming.RoundedSimpleMovingAverageSmoother _second;
        internal GeneratedTriangularState(int period)
        {
            _first = new(period);
            try { _second = new(period); }
            catch { _first.Dispose(); throw; }
        }
        public void Reset() { _first.Reset(); _second.Reset(); }
        public double Update(in Bar bar) => _second.Next(_first.Next(bar.Close, true), true);
        public void Dispose() { _first.Dispose(); _second.Dispose(); }
    }

    private sealed class HullValueState(int period) : IIndicatorState, IDisposable
    {
        private readonly Helpers.HullWindow _window = new(MovingAvgType.WeightedMovingAverage, period);
        public void Reset() => _window.Reset();
        public double Update(in Bar bar) => _window.Next(bar.Close, true);
        public void Dispose() => _window.Dispose();
    }

    private sealed class AdlValueState(int period, int count) : IMultiOutputState, IDisposable
    {
        private readonly MoneyFlowAccumulationWindow _line = new();
        private readonly RocBankAverage _signal = new(MovingAvgType.ExponentialMovingAverage, period, count);
        public void Reset() { _line.Reset(); _signal.Reset(); }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var value = _line.Next(bar.High, bar.Low, bar.Close, bar.Volume, commit: true);
            outputs[0] = value.Publish();
            outputs[1] = _signal.Next(value, true).Publish();
        }
        public void Dispose() => _signal.Dispose();
    }

    private sealed class PrefixPriceExtremeState(bool maximum) : IIndicatorState
    {
        private bool _started;
        private double _best;
        public void Reset() { _started = false; _best = 0; }
        public double Update(in Bar bar)
        {
            double value = maximum ? bar.High : bar.Low;
            if (!_started || (maximum ? value > _best : value < _best)) _best = value;
            _started = true;
            return _best;
        }
    }

    private sealed class PriceExtremeState(int period, int count, bool maximum) : IIndicatorState
    {
        // Earliest equal candidates must survive, including opposite signed zeros,
        // matching TrendCore's strict comparison in an oldest-to-newest scan.
        private readonly ExtremeDeque _window = new(Math.Min(period, Math.Max(1, count)), maximum);
        private long _index;
        public void Reset() { _window.Reset(); _index = 0; }
        public double Update(in Bar bar)
        {
            _window.Add(_index, maximum ? bar.High : bar.Low, _index - period + 1);
            _index++;
            return _window.Value;
        }
    }

    private sealed class TrueRangeValueState : IIndicatorState
    {
        private bool _started;
        private double _previousClose;
        public void Reset() { _started = false; _previousClose = 0; }
        public double Update(in Bar bar)
        {
            // Keep VolatilityCore.TrueRange's first-bar rule and operation order.
            var value = _started
                ? Math.Max(bar.High - bar.Low, Math.Max(Math.Abs(bar.High - _previousClose), Math.Abs(bar.Low - _previousClose)))
                : bar.High - bar.Low;
            _started = true;
            _previousClose = bar.Close;
            return value;
        }
    }
}
#endif
