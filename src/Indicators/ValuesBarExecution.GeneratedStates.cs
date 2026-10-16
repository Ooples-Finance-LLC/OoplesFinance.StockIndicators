#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool IsGeneratedState(IIndicator indicator) =>
        indicator is HighestHigh { Length: > 0 } or LowestLow { Length: > 0 } or TrueRange or BalanceOfPower or Wma or WilliamsR or Ema or Adl
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

    private static object CreateGeneratedState(IIndicator indicator, int count, bool volumeFlowCustomRange) => indicator switch
    {
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
        StdDev deviation => new GeneratedOrderedDeviationState(deviation.Length, count),
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
            if (PriceMean.Of(bar.High, bar.Low, bar.Close) != bar.Close) return true;
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
