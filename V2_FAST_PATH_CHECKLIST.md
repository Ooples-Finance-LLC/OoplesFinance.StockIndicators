# V2 Fast Path Implementation Checklist

This checklist tracks progress on implementing the v2 Builder fast path for all 773 indicator Calculate methods.

## Architecture Overview

The v2 fast path has three layers:

1. **Core Methods** (`src/Core/*.cs`) - Span-based implementations (**615 methods**)
2. **ComputeFast Wrappers** (`src/Builder/Compute/IndicatorCompute.cs`) - Buffer wrappers (**610 methods**)
3. **TryComputeFast Dispatch** - Routes spec options to fast path (**607 INDICATORS WIRED**)

### Current State (Updated 2026-01-24)

| Layer | Implemented | Notes |
|-------|-------------|-------|
| Core Methods | **615** | Span-based implementations (Osc:287 MA:193 Trend:71 Vol:35 Volume:29) |
| ComputeFast Wrappers | **610** | Buffer wrappers in IndicatorCompute.cs |
| SpecOptions Classes | **607** | Typed indicator options |
| TryComputeFast Dispatch | **607** | Routed to fast path methods |
| IndicatorName Total | 773 | Target for 100% coverage |

### Progress Summary

- **Batches 1-24**: Original 409 ComputeFast methods wired
- **Batch 25**: Added 40 new ComputeFast methods for unwired Core methods
- **Batch 26**: Added 36 new ComputeFast methods for additional Core methods
- **Batch 27**: Added 12 new multi-input ComputeFast methods (DeMarker, Vortex, Klinger, etc.)
- **Batch 28**: Added 15 final Core method wrappers (Reverse Engineering RSI, PPO MA, etc.)
- **Batch 29**: Added TripleHullMovingAverage, AdaptiveAutonomousRecursiveMovingAverage Core methods
- **Batch 30**: Added GDEMA, Ehlers FIR/IIR Filter, VolumeAdjustedMovingAverage, AverageDayRange
- **Batches 31-32**: Added Ehlers CenterofGravity, Reflex, Trendflex, StochasticCyberCycle Core methods
- **Batch 33**: Added Pivot Points (Floor, Camarilla, Woodie, Fibonacci, Demark), Channels (Price, Donchian, Linear)
- **Batch 34**: Added ThreeHma, AlphaDecreasingEMA, AdaptiveAutonomousRecursiveTrailingStop, AdaptiveTrailingStop, AtrTrailingStops
- **Multi-Output Support**: MACD (Line/Signal/Histogram), BollingerBands (Upper/Middle/Lower), Stochastic (K/D)
- **Total**: 607 SpecOptions, 607 dispatch routes

### Coverage Analysis

| Metric | Count | Percentage |
|--------|-------|------------|
| Core Methods | **615** | **79.6%** of 773 |
| SpecOptions/Dispatch | **607** | **78.5%** of 773 |
| Remaining indicators | **158** | **20.4%** |

### Notes on Coverage Calculation

- Core methods have abbreviations (e.g., ExponentialMovingAverage in Core → EmaSpecOptions)
- 79 Core method names don't have exact SpecOptions match due to aliasing
- All Core methods with aliases are already wired through abbreviated SpecOptions

### Implementation Challenges for Remaining Indicators

**MovingAvgType Dependency**: ~80% of remaining indicators depend on `MovingAvgType` parameter which allows 162+ MA variants. These cannot be easily converted to simple Core methods because:
1. The Calculate method allows dynamic MA selection (e.g., SMA, EMA, DEMA, etc.)
2. A Core method would need to support all 162 MA types internally, or
3. Accept a delegate/function pointer for the MA calculation

**Complex Dependencies**: Many remaining indicators call other Calculate methods internally:
- EhlersAdaptive* indicators depend on EhlersAutoCorrelationPeriodogram
- Efficient* indicators depend on KaufmanAdaptiveMovingAverage
- Most RSI/Stochastic variants depend on the base implementations

**Multi-Output Indicators**: Some indicators return multiple output series requiring multiple Core methods or array outputs.

### Remaining Work for 100% Coverage

~176 indicators need Core method implementations:
- _1LCLeastSquaresMovingAverage
- _3HMA
- _4MovingAverageConvergenceDivergence
- AdaptiveErgodicCandlestickOscillator
- ChandelierExit
- ... (full list: 263 indicators)

Each Core method implementation requires:
1. Understanding the algorithm from Calculations
2. Converting List-based code to Span-based
3. Handling dependencies on other Core methods
4. Creating SpecOptions and dispatch routing
5. Testing

## What's Already Complete

### MovingAvgType Fast Path (COMPLETE)
All 162 MovingAvgType values now use Core methods in `GetMovingAverageList`:
- [x] Single-input Core methods (144 types)
- [x] Complex single-input Core methods (7 types)
- [x] Multi-input Core methods (11 types)

## What Needs To Be Done

### Phase 1: Create SpecOptions Classes
Each indicator that should use fast path needs a dedicated `*SpecOptions` class.

**High Priority (commonly used) - COMPLETED:**
- [x] WmaSpecOptions
- [x] DemaSpecOptions
- [x] TemaSpecOptions
- [x] HmaSpecOptions
- [x] KamaSpecOptions
- [x] CciSpecOptions
- [x] WilliamsRSpecOptions
- [x] RocSpecOptions
- [x] MomentumSpecOptions
- [x] PpoSpecOptions
- [x] TsiSpecOptions
- [x] AroonSpecOptions
- [x] ObvSpecOptions

**Additional SpecOptions Created:**
- [x] TmaSpecOptions (Triangular MA)
- [x] WwmaSpecOptions (Welles Wilder MA)
- [x] LinRegSpecOptions (Linear Regression)
- [x] ZlemaSpecOptions (Zero-Lag EMA)
- [x] CmoSpecOptions (Chande Momentum Oscillator)
- [x] ApoSpecOptions (Absolute Price Oscillator)
- [x] UltimateOscillatorSpecOptions
- [x] StochRsiSpecOptions
- [x] DpoSpecOptions (Detrended Price Oscillator)
- [x] TrixSpecOptions
- [x] MassIndexSpecOptions
- [x] AdlSpecOptions (Accumulation/Distribution Line)
- [x] CmfSpecOptions (Chaikin Money Flow)
- [x] ForceIndexSpecOptions

**Volume Indicators (Batch 2):**
- [x] VrocSpecOptions (Volume ROC)
- [x] NviSpecOptions (Negative Volume Index)
- [x] PviSpecOptions (Positive Volume Index)
- [x] PvtSpecOptions (Price Volume Trend)
- [x] ChaikinOscillatorSpecOptions
- [x] EmvSpecOptions (Ease of Movement)
- [x] KvoSpecOptions (Klinger Volume Oscillator)
- [x] MfiSpecOptions (Money Flow Index)

**Volatility Indicators (Batch 2):**
- [x] StdDevSpecOptions (Standard Deviation)
- [x] HistoricalVolatilitySpecOptions
- [x] ChaikinVolatilitySpecOptions
- [x] UlcerIndexSpecOptions
- [x] NatrSpecOptions (Normalized ATR)
- [x] TrueRangeSpecOptions

**Price/Trend Indicators (Batch 2):**
- [x] DonchianChannelSpecOptions
- [x] HighestHighSpecOptions
- [x] LowestLowSpecOptions
- [x] PercentageChangeSpecOptions
- [x] LinRegSlopeSpecOptions
- [x] RSquaredSpecOptions
- [x] VhfSpecOptions (Vertical Horizontal Filter)

**Additional Oscillators (Batch 2):**
- [x] AwesomeOscillatorSpecOptions
- [x] AcceleratorOscillatorSpecOptions
- [x] StochasticKSpecOptions (Fast K)
- [x] FisherTransformSpecOptions
- [x] ConnorsRsiSpecOptions
- [x] PmoSpecOptions (Price Momentum Oscillator)
- [x] KstSpecOptions (Know Sure Thing)
- [x] PercentRankSpecOptions
- [x] ChoppinessIndexSpecOptions

**Moving Averages (Batch 3):**
- [x] SmmaSpecOptions (Smoothed MA)
- [x] McGinleyDynamicSpecOptions
- [x] T3SpecOptions
- [x] VidyaSpecOptions (Variable Index Dynamic Average)
- [x] VmaSpecOptions (Variable MA)
- [x] AlmaSpecOptions (Arnaud Legoux MA)
- [x] LsmaSpecOptions (Least Squares MA)
- [x] FramaSpecOptions (Fractal Adaptive MA)
- [x] AmaSpecOptions (Adaptive MA)
- [x] JmaSpecOptions (Jurik MA)
- [x] SuperSmootherSpecOptions
- [x] ButterworthFilterSpecOptions

**MACD Variants (Batch 3):**
- [x] MacdLineSpecOptions
- [x] MacdSignalSpecOptions
- [x] MacdHistogramSpecOptions

**Trend Indicators (Batch 3):**
- [x] ParabolicSarSpecOptions
- [x] SuperTrendSpecOptions
- [x] ChandelierExitLongSpecOptions
- [x] ChandelierExitShortSpecOptions

**Volume/Power Indicators (Batch 3):**
- [x] BalanceOfPowerSpecOptions
- [x] RviSpecOptions (Relative Vigor Index) - SpecOptions only, no ComputeFast
- [x] PvoSpecOptions (Percentage Volume Oscillator)

**More Oscillators (Batch 3):**
- [x] CoppockCurveSpecOptions
- [x] ChandeForecastOscillatorSpecOptions
- [x] BullPowerSpecOptions
- [x] BearPowerSpecOptions
- [x] PfeSpecOptions (Polarized Fractal Efficiency) - SpecOptions only, no ComputeFast
- [x] StcSpecOptions (Schaff Trend Cycle) - SpecOptions only, no ComputeFast
- [x] PzoSpecOptions (Price Zone Oscillator) - SpecOptions only, no ComputeFast
- [x] ElderForceIndexSpecOptions
- [x] PgoSpecOptions (Pretty Good Oscillator) - SpecOptions only, no ComputeFast
- [x] RelativeVolatilityIndexSpecOptions
- [x] QstickSpecOptions
- [x] SpecialKSpecOptions

**Vortex and Trend Indicators (Batch 3):**
- [x] VortexPositiveSpecOptions
- [x] VortexNegativeSpecOptions
- [x] TrendIntensityIndexSpecOptions
- [x] AbsoluteStrengthIndexSpecOptions
- [x] RelativeMomentumIndexSpecOptions
- [x] IntradayMomentumIndexSpecOptions

**Volume Weighted MAs (Batch 3):**
- [x] VwmaSpecOptions (Volume Weighted MA)
- [x] VwapSpecOptions (Volume Weighted Average Price)

**Complex Oscillators (Batch 3):**
- [x] ElliottWaveOscillatorSpecOptions
- [x] GatorOscillatorSpecOptions

**Ichimoku (Batch 3):**
- [x] IchimokuTenkanSenSpecOptions
- [x] IchimokuKijunSenSpecOptions

**Additional Oscillators (Batch 3 - wired separately):**
- [x] PfeSpecOptions (Polarized Fractal Efficiency) - WIRED
- [x] StcSpecOptions (Schaff Trend Cycle) - WIRED
- [x] PzoSpecOptions (Price Zone Oscillator) - WIRED
- [x] PgoSpecOptions (Pretty Good Oscillator) - WIRED
- [x] RviSpecOptions (Relative Vigor Index) - WIRED

**Batch 4 - Price indicators:**
- [x] TypicalPriceSpecOptions
- [x] MedianPriceSpecOptions
- [x] WeightedCloseSpecOptions
- [x] AveragePriceSpecOptions
- [x] MidpointSpecOptions
- [x] MidpriceSpecOptions

**Batch 4 - Statistical indicators:**
- [x] VarianceSpecOptions
- [x] CoefficientOfVariationSpecOptions
- [x] StandardErrorSpecOptions

**Batch 4 - Aroon components:**
- [x] AroonUpSpecOptions
- [x] AroonDownSpecOptions

**Batch 4 - More oscillators:**
- [x] DemarkerSpecOptions
- [x] SmoothedRocSpecOptions
- [x] DerivativeOscillatorSpecOptions
- [x] FractalChaosOscillatorSpecOptions
- [x] DisparityIndexSpecOptions
- [x] DynamicMomentumIndexSpecOptions

**Batch 4 - More MAs:**
- [x] SineWmaSpecOptions
- [x] HammingMaSpecOptions
- [x] GeoMaSpecOptions
- [x] RegularizedEmaSpecOptions
- [x] ModifiedMaSpecOptions
- [x] EndPointMovingAverageSpecOptions
- [x] CubicWmaSpecOptions
- [x] NaturalMaSpecOptions

**Batch 4 - Volume indicators:**
- [x] TradeVolumeIndexSpecOptions
- [x] VolumeOscillatorSpecOptions
- [x] VolumeZoneOscillatorSpecOptions
- [x] NetVolumeSpecOptions
- [x] VolumeMomentumSpecOptions
- [x] NormalizedVolumeSpecOptions

**Batch 4 - Stochastic variants:**
- [x] StochasticDSpecOptions
- [x] DoubleSmoothedStochasticSpecOptions
- [x] PremierStochasticSpecOptions

**Batch 4 - Volatility indicators:**
- [x] CloseToCloseVolatilitySpecOptions
- [x] ParkinsonVolatilitySpecOptions
- [x] GarmanKlassVolatilitySpecOptions

**Batch 5 - Price/Range indicators:**
- [x] AdrSpecOptions
- [x] BollingerBandsMiddleSpecOptions
- [x] VpciSpecOptions
- [x] KeltnerChannelMiddleSpecOptions
- [x] TrendDetectionSpecOptions
- [x] PriceChannelMiddleSpecOptions
- [x] SwingIndexSpecOptions
- [x] AccumulativeSwingIndexSpecOptions
- [x] ZigZagSpecOptions
- [x] PivotPointSpecOptions
- [x] RangeSpecOptions
- [x] PriceMomentumSpecOptions

**Batch 5 - Volume indicators:**
- [x] MfiCoreSpecOptions
- [x] TwiggsMoneyFlowSpecOptions
- [x] DemandIndexSpecOptions
- [x] WilliamsADSpecOptions
- [x] CumulativeVolumeIndexSpecOptions
- [x] VolumePriceTrendSpecOptions
- [x] ElderRayBullPowerSpecOptions
- [x] ElderRayBearPowerSpecOptions
- [x] VolumeWeightedRsiSpecOptions

**Batch 5 - Trend/Directional indicators:**
- [x] DirectionalTrendIndexSpecOptions
- [x] LinRegInterceptSpecOptions
- [x] ElderImpulseSystemSpecOptions
- [x] MassThrustSpecOptions

**Batch 5 - Chande indicators:**
- [x] ChandeCompositeMomentumIndexSpecOptions
- [x] ChandeKrollRSquaredIndexSpecOptions
- [x] ChandeTrendScoreSpecOptions
- [x] ChandeMomentumOscillatorAbsoluteSpecOptions

**Batch 5 - Oscillators:**
- [x] ErgodicCandlestickOscillatorSpecOptions
- [x] BayesianOscillatorSpecOptions
- [x] AnchoredMomentumSpecOptions
- [x] ChartmillValueIndicatorSpecOptions
- [x] CenterOfLinearitySpecOptions
- [x] BreakoutRsiSpecOptions
- [x] ChopZoneSpecOptions
- [x] ForecastOscillatorSpecOptions

**Batch 5 - Adaptive indicators:**
- [x] AsymmetricalRsiSpecOptions
- [x] AdaptiveStochasticSpecOptions
- [x] AdaptiveRsiSpecOptions

**Batch 5 - Moving averages:**
- [x] AutoLineSpecOptions
- [x] AutoLineWithDriftSpecOptions
- [x] AutoFilterSpecOptions
- [x] BuffAverageSpecOptions
- [x] BryantAdaptiveMovingAverageSpecOptions
- [x] CompoundRatioMovingAverageSpecOptions
- [x] ConditionalAccumulatorSpecOptions
- [x] AhrensMovingAverageSpecOptions
- [x] AlphaDecreasingEmaSpecOptions
- [x] AdaptiveEmaSpecOptions
- [x] AutonomousRecursiveMaSpecOptions
- [x] AdaptiveLeastSquaresSpecOptions
- [x] AtrFilteredEmaSpecOptions
- [x] MedianMaSpecOptions
- [x] VolumeAdjustedMaSpecOptions
- [x] QuadraticWmaSpecOptions
- [x] ParabolicWmaSpecOptions

**Batch 5 - Volatility indicators:**
- [x] RogersSatchellVolatilitySpecOptions
- [x] YangZhangVolatilitySpecOptions
- [x] DownsideDeviationSpecOptions
- [x] StandardDeviationChannelSpecOptions
- [x] StandardDeviationVolatilitySpecOptions
- [x] VolatilityRatioSpecOptions

**Batch 5 - Bands/Channels:**
- [x] AtrTrailingStopsSpecOptions
- [x] AtrChannelWidthSpecOptions
- [x] AverageTrueRangeChannelSpecOptions
- [x] VolatilityStopSpecOptions
- [x] BollingerBandsPercentBSpecOptions
- [x] BollingerBandsAtrSpecOptions

**Batch 5 - Ratio/Performance:**
- [x] CalmarRatioSpecOptions
- [x] CommoditySelectionIndexSpecOptions

**Batch 5 - Smoothed oscillators:**
- [x] SmoothedWilliamsRSpecOptions
- [x] PriceOscillatorPercentSpecOptions
- [x] NormalizedMacdSpecOptions
- [x] RelativeVigorIndexSignalSpecOptions
- [x] VolumeMomentumOscillatorSpecOptions
- [x] TrendContinuationFactorSpecOptions
- [x] TrendPersistenceRateSpecOptions
- [x] InertiaSpecOptions

**Batch 5 - Price calculations:**
- [x] PercentChangeSpecOptions
- [x] PriceChangeSpecOptions
- [x] MidRangeSpecOptions
- [x] OhlcAverageSpecOptions
- [x] HlcAverageSpecOptions
- [x] DoubleSmoothedMomentaSpecOptions

**Batch 5 - Statistical indicators:**
- [x] HighLowIndexSpecOptions
- [x] MarketFacilitationIndexSpecOptions
- [x] TrendScoreSpecOptions
- [x] MedianValueSpecOptions
- [x] LogReturnsSpecOptions
- [x] SimpleReturnsSpecOptions
- [x] CumulativeSumSpecOptions
- [x] RollingMaxSpecOptions
- [x] RollingMinSpecOptions
- [x] PricePositionSpecOptions
- [x] AtrPercentSpecOptions

**Batch 5 - Trend/Activator indicators:**
- [x] RepulseSpecOptions
- [x] GannHiLoActivatorSpecOptions
- [x] HalfTrendSpecOptions

**Batch 6 - Chande oscillators:**
- [x] ChandeMomentumOscillatorAbsoluteAverageSpecOptions
- [x] ChandeMomentumOscillatorAverageSpecOptions
- [x] ChandeMomentumOscillatorAverageDisparityIndexSpecOptions
- [x] ChandeMomentumOscillatorFilterSpecOptions

**Batch 6 - Stochastic variants:**
- [x] DoubleStochasticOscillatorSpecOptions
- [x] BilateralStochasticOscillatorSpecOptions
- [x] FisherTransformStochasticOscillatorSpecOptions
- [x] StochasticCustomOscillatorSpecOptions
- [x] FastSlowStochasticOscillatorSpecOptions
- [x] DiNapoliPreferredStochasticOscillatorSpecOptions
- [x] DMIStochasticSpecOptions
- [x] CCTStochRelativeStrengthIndexSpecOptions

**Batch 6 - DT/Dynamic oscillators:**
- [x] DTOscillatorSpecOptions
- [x] DynamicMomentumOscillatorSpecOptions

**Batch 6 - Price/Momentum oscillators:**
- [x] ComparePriceMomentumOscillatorSpecOptions
- [x] DailyAveragePriceDeltaSpecOptions
- [x] PriceCycleOscillatorSpecOptions
- [x] PriceVolumeOscillatorSpecOptions
- [x] PercentChangeOscillatorSpecOptions
- [x] DecisionPointPriceMomentumOscillatorSpecOptions

**Batch 6 - Demand/Volume oscillators:**
- [x] DemandOscillatorSpecOptions
- [x] AverageMoneyFlowOscillatorSpecOptions
- [x] VolumeAccumulationOscillatorSpecOptions
- [x] TFSVolumeOscillatorSpecOptions

**Batch 6 - RSI variants:**
- [x] DoubleSmoothedRelativeStrengthIndexSpecOptions
- [x] FastSlowRsiOscillatorSpecOptions

**Batch 6 - DiNapoli/Ergodic/PPO oscillators:**
- [x] DiNapoliPercentagePriceOscillatorSpecOptions
- [x] ErgodicPercentagePriceOscillatorSpecOptions
- [x] ImpulsePercentagePriceOscillatorSpecOptions
- [x] MirroredPercentagePriceOscillatorSpecOptions
- [x] PercentagePriceOscillatorLeaderSpecOptions
- [x] TFSMboPercentagePriceOscillatorSpecOptions

**Batch 6 - Kurtosis/Degree oscillators:**
- [x] FastSlowKurtosisOscillatorSpecOptions
- [x] FastSlowDegreeOscillatorSpecOptions

**Batch 6 - Gann oscillators:**
- [x] GOscillatorSpecOptions
- [x] GannSwingOscillatorSpecOptions
- [x] GannTrendOscillatorSpecOptions

**Batch 6 - Special oscillators:**
- [x] FireflyOscillatorSpecOptions
- [x] KarobeinOscillatorSpecOptions
- [x] GroverLlorensCycleOscillatorSpecOptions
- [x] LindaRaschke310OscillatorSpecOptions
- [x] MidpointOscillatorSpecOptions
- [x] MobilityOscillatorSpecOptions

**Batch 6 - Projection/Regression oscillators:**
- [x] ProjectionOscillatorSpecOptions
- [x] RainbowOscillatorSpecOptions
- [x] RegressionOscillatorSpecOptions
- [x] RexOscillatorSpecOptions

**Batch 6 - Sentiment/Zone oscillators:**
- [x] SentimentZoneOscillatorSpecOptions
- [x] WaveTrendOscillatorSpecOptions
- [x] WamiOscillatorSpecOptions

**Batch 6 - Kase oscillators:**
- [x] KasePeakOscillatorV1SpecOptions
- [x] KasePeakOscillatorV2SpecOptions

**Batch 6 - Mathematical oscillators:**
- [x] VaradiOscillatorSpecOptions
- [x] PrimeNumberOscillatorSpecOptions
- [x] TrigonometricOscillatorSpecOptions
- [x] UltimateTraderOscillatorSpecOptions
- [x] SmoothedDeltaRatioOscillatorSpecOptions
- [x] RobustWeightingOscillatorSpecOptions

**Batch 6 - Detector/Pivot oscillators:**
- [x] PivotDetectorOscillatorSpecOptions
- [x] TickLineMomentumOscillatorSpecOptions
- [x] SupportAndResistanceOscillatorSpecOptions
- [x] TradingMadeMoreSimplerOscillatorSpecOptions
- [x] NthOrderDifferencingOscillatorSpecOptions
- [x] OscOscillatorSpecOptions

**Batch 6 - Ehlers oscillators:**
- [x] EhlersCenterOfGravityOscillatorSpecOptions
- [x] EhlersDecyclerOscillatorV1SpecOptions
- [x] EhlersDecyclerOscillatorV2SpecOptions
- [x] EhlersHilbertOscillatorSpecOptions
- [x] EhlersUniversalOscillatorSpecOptions
- [x] EhlersRecursiveMedianOscillatorSpecOptions
- [x] EhlersStochasticCenterOfGravityOscillatorSpecOptions
- [x] EhlersFisherizedDeviationScaledOscillatorSpecOptions
- [x] EhlersAdaptiveCenterOfGravityOscillatorSpecOptions

**Batch 6 - Vervoort oscillators:**
- [x] VervoortSmoothedOscillatorSpecOptions
- [x] VervoortHeikenAshiCandlestickOscillatorSpecOptions
- [x] VervoortHeikenAshiLongTermCandlestickOscillatorSpecOptions

**Batch 6 - Convergence/Divergence oscillators:**
- [x] RelativeDifferenceOfSquaresOscillatorSpecOptions
- [x] LinearQuadraticConvergenceDivergenceOscillatorSpecOptions
- [x] StationaryExtrapolatedLevelsOscillatorSpecOptions

**Batch 6 - Kaufman/MACD oscillators:**
- [x] KaufmanAdaptiveCorrelationOscillatorSpecOptions
- [x] StochasticMacdOscillatorSpecOptions
- [x] McClellanOscillatorSpecOptions

**Batch 6 - Decision Point/Swenlin oscillators:**
- [x] DecisionPointBreadthSwenlinTradingOscillatorSpecOptions

**Batch 6 - Mass Thrust oscillator:**
- [x] MassThrustOscillatorSpecOptions

**Batch 7 - Moving averages:**
- [x] UltimateMovingAverageSpecOptions
- [x] SymmetricallyWeightedMovingAverageSpecOptions
- [x] SquareRootWeightedMovingAverageSpecOptions
- [x] Spencer15PointMovingAverageSpecOptions
- [x] Spencer21PointMovingAverageSpecOptions
- [x] SlowSmoothedMovingAverageSpecOptions
- [x] RepulsionMovingAverageSpecOptions
- [x] QuickMovingAverageSpecOptions

**Batch 7 - Ehlers MAs:**
- [x] EhlersBetterExponentialMovingAverageSpecOptions
- [x] EhlersDeviationScaledMovingAverageSpecOptions
- [x] EhlersHannMovingAverageSpecOptions
- [x] EhlersTriangleMovingAverageSpecOptions

**Batch 7 - Volume weighted/exponential MAs:**
- [x] ElasticVolumeWeightedMovingAverageV1SpecOptions
- [x] HoltExponentialMovingAverageSpecOptions
- [x] PentupleExponentialMovingAverageSpecOptions
- [x] QuadrupleExponentialMovingAverageSpecOptions

**Batch 7 - Ichimoku components:**
- [x] IchimokuSenkouSpanASpecOptions
- [x] IchimokuSenkouSpanBSpecOptions
- [x] IchimokuChikouSpanSpecOptions

**Batch 7 - Williams fractals:**
- [x] WilliamsFractalUpSpecOptions
- [x] WilliamsFractalDownSpecOptions

**Batch 7 - Alligator components:**
- [x] AlligatorJawSpecOptions
- [x] AlligatorTeethSpecOptions
- [x] AlligatorLipsSpecOptions

**Batch 7 - Ehlers Laguerre:**
- [x] EhlersLaguerreFilterSpecOptions
- [x] EhlersLaguerreRsiSpecOptions
- [x] EhlersZeroLagEmaSpecOptions
- [x] EhlersFramaSpecOptions
- [x] EhlersInverseFisherTransformSpecOptions
- [x] EhlersCyberCycleSpecOptions
- [x] EhlersStochasticSpecOptions
- [x] EhlersAdaptiveLaguerreFilterSpecOptions

**Batch 7 - Trend/Filter indicators:**
- [x] CoralTrendIndicatorSpecOptions
- [x] DampedSineWaveWeightedFilterSpecOptions
- [x] FibonacciWeightedMovingAverageSpecOptions
- [x] GeneralizedDoubleEmaSpecOptions
- [x] GeometricMeanMovingAverageSpecOptions
- [x] HarmonicMeanMovingAverageSpecOptions

**Batch 7 - Ehlers Butterworth filters:**
- [x] Ehlers2PoleButterworthFilterV1SpecOptions
- [x] Ehlers2PoleButterworthFilterV2SpecOptions
- [x] Ehlers3PoleButterworthFilterV1SpecOptions
- [x] Ehlers3PoleButterworthFilterV2SpecOptions

**Batch 7 - Ehlers Super Smoother filters:**
- [x] Ehlers2PoleSuperSmootherFilterV1SpecOptions
- [x] Ehlers2PoleSuperSmootherFilterV2SpecOptions
- [x] Ehlers3PoleSuperSmootherFilterSpecOptions

**Batch 7 - More Ehlers filters:**
- [x] EhlersDecyclerSpecOptions
- [x] EhlersHammingMovingAverageSpecOptions
- [x] EhlersLeadingIndicatorSpecOptions
- [x] EhlersHighPassFilterV1SpecOptions
- [x] EhlersHighPassFilterV2SpecOptions
- [x] DistanceWeightedMovingAverageSpecOptions
- [x] EhlersFilterSpecOptions
- [x] EhlersFirFilterSpecOptions
- [x] EhlersIirFilterSpecOptions

**Batch 7 - Cycle indicators:**
- [x] SimpleCycleSpecOptions
- [x] SimpleLinesSpecOptions
- [x] DoubleExponentialSmoothingSpecOptions
- [x] DetrendedSyntheticPriceSpecOptions

**Batch 7 - Timing/Setup indicators:**
- [x] BelkhayateTimingSpecOptions
- [x] DemarkSetupIndicatorSpecOptions
- [x] PerformanceIndexSpecOptions
- [x] PsychologicalLineSpecOptions

**Batch 7 - Market indicators:**
- [x] MoveTrackerSpecOptions
- [x] MultiLevelIndicatorSpecOptions
- [x] MarketDirectionIndicatorSpecOptions
- [x] MorphedSineWaveSpecOptions

**Batch 7 - Price/Statistical indicators:**
- [x] FullTypicalPriceSpecOptions
- [x] InternalBarStrengthIndicatorSpecOptions
- [x] ZScoreSpecOptions
- [x] FastZScoreSpecOptions
- [x] KurtosisIndicatorSpecOptions

**Batch 7 - Demark indicators:**
- [x] DemarkRangeExpansionIndexSpecOptions
- [x] DemarkPressureRatioV1SpecOptions
- [x] DemarkPressureRatioV2SpecOptions
- [x] DemarkReversalPointsSpecOptions

**Batch 8 - Final (Channel widths, Core methods, RMO):**
- [x] BollingerBandsWidthSpecOptions
- [x] DonchianChannelWidthSpecOptions
- [x] KeltnerChannelWidthSpecOptions
- [x] MassIndexCoreSpecOptions
- [x] RahulMohindarOscillatorSpecOptions
- [x] RviVolatilitySpecOptions
- [x] StandardErrorCoreSpecOptions

### Phase 2: Wire TryComputeFast Dispatch
Add each new SpecOptions to the switch expression in `TryComputeFast`.

### Phase 3: Remaining Categories

| Category | Calculate Methods | ComputeFast Methods | SpecOptions Needed |
|----------|------------------|---------------------|-------------------|
| MovingAverages | 160 | ~50 | ~45 |
| Oscillators | 212 | ~100 | ~95 |
| Ehlers | 115 | ~60 | ~55 |
| PriceChannel | 61 | ~30 | ~25 |
| Volume | 34 | ~20 | ~18 |
| Volatility | 28 | ~20 | ~17 |
| Trend | 24 | ~15 | ~12 |
| Stochastic | 19 | ~10 | ~8 |
| Rsi | 19 | ~10 | ~8 |
| TrailingStop | 13 | ~8 | ~6 |
| Chande | 13 | ~8 | ~6 |
| Macd | 12 | ~8 | ~6 |
| Momentum | 11 | ~6 | ~5 |
| Ratio | 10 | ~6 | ~5 |
| Ppo | 9 | ~6 | ~5 |
| BollingerBands | 9 | ~6 | ~5 |
| PivotPoint | 8 | ~5 | ~4 |
| Inputs | 7 | ~5 | ~4 |
| Demark | 6 | ~4 | ~3 |
| Wilder | 3 | ~3 | ~2 |

## Files To Modify

### Core Files (mostly complete)
- `src/Core/MovingAverageCore.cs` (188 methods)
- `src/Core/OscillatorCore.cs` (221 methods)
- `src/Core/TrendCore.cs` (57 methods)
- `src/Core/VolatilityCore.cs` (35 methods)
- `src/Core/VolumeCore.cs` (29 methods)

### Builder Files (need work)
- `src/Builder/Specs/*.cs` - Create ~400 new SpecOptions classes
- `src/Builder/Compute/IndicatorCompute.cs` - Add dispatch cases

## Testing

- Streaming parity tests verify Core methods match Calculate methods
- All 707 current tests pass
- Each new SpecOptions type should have corresponding tests

## Progress Summary

| Item | Status | Count |
|------|--------|-------|
| Calculate Methods | Total | 773 |
| Core Methods | Implemented | ~530 |
| ComputeFast Methods | Implemented | 410 |
| SpecOptions Classes | Implemented | **414** |
| TryComputeFast Routes | Wired | **409** |
| **Effective Fast Path Coverage** | | **~53%** |

## Next Steps

1. ~~**Create high-priority SpecOptions classes** (WMA, DEMA, CCI, etc.)~~ **DONE**
2. ~~**Add dispatch cases** to TryComputeFast~~ **DONE for all 409 indicators**
3. ~~**Create remaining SpecOptions** for final indicators~~ **DONE - Batch 8 completed**
4. **Fast path coverage now at ~53%** (409/773 Calculate methods)
5. **COMPLETE**: All ComputeFast methods now have SpecOptions and are wired to TryComputeFast
6. **Future**: Consider source generation to auto-create SpecOptions from Calculate signatures for remaining ~364 indicators without ComputeFast

## Notes

- All 410 ComputeFast methods now have SpecOptions classes and are wired to TryComputeFast
- The remaining ~364 indicators (773 - 409 = 364) would need ComputeFast methods before SpecOptions
- Consider using source generation to auto-create SpecOptions from Calculate signatures for future expansion
- **Milestone achieved**: All existing ComputeFast methods are now accessible via the typed fast path API
