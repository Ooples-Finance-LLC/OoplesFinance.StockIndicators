# V2 Fast Path Implementation Checklist

This checklist tracks progress on implementing the v2 Builder fast path for all 773 indicator Calculate methods.

## Architecture Overview

The v2 fast path has three layers:

1. **Core Methods** (`src/Core/*.cs`) - Span-based implementations (~530 methods)
2. **ComputeFast Wrappers** (`src/Builder/Compute/IndicatorCompute.cs`) - Buffer wrappers (411 methods)
3. **TryComputeFast Dispatch** - Routes spec options to fast path (**243 INDICATORS WIRED**)

### Current State

| Layer | Implemented | Notes |
|-------|-------------|-------|
| Core Methods | ~530 | Most calculations implemented |
| ComputeFast Wrappers | 411 | Most wrappers exist |
| SpecOptions Classes | **248** | 239 new + 9 original |
| TryComputeFast Dispatch | **243** | 234 new routes wired |

### The Gap

- **411 ComputeFast methods exist** but some still use `GenericIndicatorOptions`
- **243 indicators** now route through `TryComputeFast`
- **Missing**: ~168 specific `*SpecOptions` classes and corresponding dispatch cases

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
| ComputeFast Methods | Implemented | 411 |
| SpecOptions Classes | Implemented | **248** |
| TryComputeFast Routes | Wired | **243** |
| **Effective Fast Path Coverage** | | **~31.4%** |

## Next Steps

1. ~~**Create high-priority SpecOptions classes** (WMA, DEMA, CCI, etc.)~~ **DONE**
2. ~~**Add dispatch cases** to TryComputeFast~~ **DONE for 243 indicators**
3. **Continue creating SpecOptions** for remaining ~168 indicators
4. **Focus on categories with most ComputeFast methods**: Ehlers (~60 remaining), more oscillators (~100 remaining)
5. **Consider source generation** to auto-create SpecOptions from Calculate signatures

## Notes

- The ComputeFast methods already exist for 411 indicators
- The bottleneck is creating SpecOptions classes and wiring dispatch
- Consider using source generation to auto-create SpecOptions from Calculate signatures
