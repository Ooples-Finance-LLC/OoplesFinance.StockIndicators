# V2 Indicator Validation Status

**Last Updated**: 2025-01-26
**Total Indicators**: 743
**Golden File Validated**: 49 core indicators (see GoldenFileTests.cs)
**Property Tested**: 743 (see IndicatorPropertyTests.cs)
**Variant API**: RSI and ATR support configurable smoothing methods
**Has Variants**: 67 documented (see Known Variants section)

## Validation Summary

- **Phase 1**: Property-Based Tests ✅ COMPLETE - All indicators pass bounds/NaN checks
- **Phase 2**: Tracking System ✅ COMPLETE
- **Phase 3**: Variant Research ✅ COMPLETE
- **Phase 4**: Reference Tests ⏳ IN PROGRESS - ~86 indicators have formula verification
- **Phase 5**: Hybrid Config API ✅ COMPLETE - RSI/ATR with MovingAvgType
- **Phase 6**: Golden File Tests ⏳ IN PROGRESS - 49/743 complete
- **Phase 7**: No Hardcoded Smoothing ✅ COMPLETE

## Golden File Tests (Authoritative Validation)

The following 49 indicators have proper golden file validation with:
- Fixed, reproducible input data
- Reference formula citations (Wilder 1978, Appel, Lane, etc.)
- Hand-calculated expected values

### Moving Averages (12)
| Indicator | Reference | Test File |
|-----------|-----------|-----------|
| SMA(5), SMA(10) | Standard statistical average | GoldenFileTests.cs |
| EMA(5), EMA(12) | Standard EMA k=2/(n+1) | GoldenFileTests.cs |
| WMA(5) | Weighted average formula | GoldenFileTests.cs |
| HMA(9) | Alan Hull - Hull Moving Average | GoldenFileTests.cs |
| DEMA(10) | Patrick Mulloy 1994 | GoldenFileTests.cs |
| TEMA(10) | Patrick Mulloy 1994 | GoldenFileTests.cs |
| TMA(10) | Double-smoothed SMA | GoldenFileTests.cs |
| ZLEMA(10) | Sylvain Vervoort | GoldenFileTests.cs |
| KAMA(10) | Perry Kaufman 1995 | GoldenFileTests.cs |
| VIDYA(10) | Tushar Chande 1994 | GoldenFileTests.cs |

### Oscillators (15)
| Indicator | Reference | Test File |
|-----------|-----------|-----------|
| RSI(14) | Wilder 1978 | GoldenFileTests.cs |
| Stochastic %K | George Lane | GoldenFileTests.cs |
| Williams %R | Larry Williams | GoldenFileTests.cs |
| CCI(20) | Donald Lambert 1980 | GoldenFileTests.cs |
| MFI(14) | Quong & Soudack | GoldenFileTests.cs |
| CMO | Tushar Chande | GoldenFileTests.cs |
| Ultimate Oscillator | Larry Williams 1985 | GoldenFileTests.cs |
| Aroon | Tushar Chande 1995 | GoldenFileTests.cs |
| DPO(14) | Detrended Price Oscillator | GoldenFileTests.cs |
| APO(12,26) | Absolute Price Oscillator | GoldenFileTests.cs |
| PPO(12,26) | Percentage Price Oscillator | GoldenFileTests.cs |
| ROC(10) | Rate of Change | GoldenFileTests.cs |
| Momentum(10) | Ratio-based momentum | GoldenFileTests.cs |
| TRIX(15) | Jack Hutson | GoldenFileTests.cs |
| Choppiness Index | E.W. Dreiss 1993 | GoldenFileTests.cs |

### Trend (6)
| Indicator | Reference | Test File |
|-----------|-----------|-----------|
| MACD(12,26,9) | Gerald Appel | GoldenFileTests.cs |
| ADX(14) | Wilder 1978 | GoldenFileTests.cs |
| Parabolic SAR | Wilder 1978 | GoldenFileTests.cs |
| VHF(14) | Adam White 1991 | GoldenFileTests.cs |
| Donchian Channel | Richard Donchian | GoldenFileTests.cs |
| Keltner Channel | Chester Keltner 1960 | GoldenFileTests.cs |

### Volatility (7)
| Indicator | Reference | Test File |
|-----------|-----------|-----------|
| ATR(14) | Wilder 1978 | GoldenFileTests.cs |
| True Range | Wilder 1978 | GoldenFileTests.cs |
| Bollinger Bands | John Bollinger | GoldenFileTests.cs |
| Standard Deviation | Population StdDev | GoldenFileTests.cs |
| Historical Volatility | Annualized StdDev of returns | GoldenFileTests.cs |
| Ulcer Index | Peter Martin 1987 | GoldenFileTests.cs |
| Chaikin Volatility | Marc Chaikin | GoldenFileTests.cs |

### Volume (9)
| Indicator | Reference | Test File |
|-----------|-----------|-----------|
| OBV | Joe Granville 1963 | GoldenFileTests.cs |
| CMF(20) | Marc Chaikin | GoldenFileTests.cs |
| Force Index | Alexander Elder | GoldenFileTests.cs |
| TSI | William Blau | GoldenFileTests.cs |
| VWAP | Standard VWAP | GoldenFileTests.cs |
| ADL | Chaikin/Granville 1963 | GoldenFileTests.cs |
| PVT | Price Volume Trend | GoldenFileTests.cs |
| NVI | Paul Dysart 1930s | GoldenFileTests.cs |

## Validation Criteria

Each indicator must pass:
1. **Property Tests**: Bounds checking, no NaN/Infinity, statistical sanity
2. **Reference Tests**: Values match authoritative source (papers, TA-Lib, TradingView)
3. **Edge Cases**: Empty data, single bar, constant prices, high volatility

## Validation Status Legend

| Status | Meaning |
|--------|---------|
| :white_check_mark: | Validated - passes all tests with reference verification |
| :warning: | Needs Review - property tests pass but needs reference verification |
| :x: | Failed - known issues to fix |
| :question: | Has Variants - multiple valid formulas exist (document which variant we implement) |
| :construction: | In Progress - currently being validated |
| - | Not Started |

---

## Moving Averages

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| SimpleMovingAverage | :white_check_mark: | :white_check_mark: | - | - | :construction: | Formula verified |
| ExponentialMovingAverage | :white_check_mark: | :white_check_mark: | - | :question: | :construction: | Wilder vs Standard EMA |
| WeightedMovingAverage | :white_check_mark: | :white_check_mark: | - | - | :construction: | Formula verified |
| TriangularMovingAverage | :white_check_mark: | :white_check_mark: | - | - | :construction: | Property verified |
| DoubleExponentialMovingAverage | :white_check_mark: | :white_check_mark: | - | - | :construction: | Property verified |
| TripleExponentialMovingAverage | :white_check_mark: | :white_check_mark: | - | - | :construction: | Property verified |
| HullMovingAverage | :white_check_mark: | :white_check_mark: | - | - | :construction: | Property verified |
| KaufmanAdaptiveMovingAverage | :white_check_mark: | - | - | - | - | |
| VariableIndexDynamicAverage | :white_check_mark: | - | - | - | - | |
| Tema | :white_check_mark: | - | - | - | - | |

---

## Oscillators (Bounded 0-100)

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| RelativeStrengthIndex | :white_check_mark: | :white_check_mark: | - | :question: | :construction: | Wilder vs Cutler smoothing |
| StochasticOscillator | :white_check_mark: | :white_check_mark: | - | - | :construction: | Bounds verified |
| MoneyFlowIndex | :white_check_mark: | :white_check_mark: | - | - | :construction: | Bounds verified |
| CommodityChannelIndex | :white_check_mark: | :white_check_mark: | - | - | :construction: | Property verified |
| UltimateOscillator | :white_check_mark: | - | - | - | - | |
| ChaikinMoneyFlow | :white_check_mark: | :white_check_mark: | - | - | :construction: | Bounds verified |
| AccumulationDistributionOscillator | :white_check_mark: | - | - | - | - | |
| AroonOscillator | :white_check_mark: | - | - | - | - | |
| BalanceOfPower | :white_check_mark: | - | - | - | - | |
| ConnorsRsi | :white_check_mark: | - | - | - | - | |

---

## Oscillators (Bounded -100 to 100)

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| WilliamsR | :white_check_mark: | :white_check_mark: | - | - | :construction: | Bounds verified |
| PercentagePriceOscillator | :white_check_mark: | - | - | - | - | |
| PercentageVolumeOscillator | :white_check_mark: | - | - | - | - | |
| StochasticMomentumIndex | :white_check_mark: | - | - | - | - | |
| TrueStrengthIndex | :white_check_mark: | - | - | - | - | |

---

## Volatility (Non-Negative)

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| AverageTrueRange | :white_check_mark: | :white_check_mark: | - | :question: | :construction: | Wilder vs SMA smoothing |
| StandardDeviation | :white_check_mark: | :white_check_mark: | - | - | :construction: | Formula verified |
| BollingerBands | :white_check_mark: | :white_check_mark: | - | - | :construction: | Formula verified |
| KeltnerChannels | :white_check_mark: | - | - | - | - | |
| AverageTrueRangePercent | :white_check_mark: | - | - | - | - | |
| Variance | :white_check_mark: | - | - | - | - | |

---

## Trend Indicators

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| Macd | :white_check_mark: | :white_check_mark: | - | :question: | :construction: | EMA variant, MACD=EMA12-EMA26 verified |
| MovingAverageConvergenceDivergence | :white_check_mark: | - | - | - | - | |
| AverageDirectionalIndex | :white_check_mark: | :white_check_mark: | - | - | :construction: | Bounds verified |
| ParabolicSar | :white_check_mark: | - | - | - | - | |
| SuperTrend | :white_check_mark: | - | - | - | - | |
| Adx | :white_check_mark: | - | - | - | - | |
| DirectionalMovementIndex | :white_check_mark: | - | - | - | - | |
| RateOfChange | :white_check_mark: | :white_check_mark: | - | - | :construction: | Property verified |
| MomentumOscillator | :white_check_mark: | :white_check_mark: | - | - | :construction: | Property verified |

---

## Volume Indicators

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| OnBalanceVolume | :white_check_mark: | :white_check_mark: | - | - | :construction: | Property verified |
| VolumeWeightedAveragePrice | :white_check_mark: | - | - | - | - | |
| AccumulationDistribution | :white_check_mark: | - | - | - | - | |
| ChaikinOscillator | :white_check_mark: | - | - | - | - | |
| ForceIndex | :white_check_mark: | - | - | - | - | |
| NegativeVolumeIndex | :white_check_mark: | - | - | - | - | |
| PositiveVolumeIndex | :white_check_mark: | - | - | - | - | |

---

## Multi-Stock Comparison

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| RSMKIndicator | :white_check_mark: | - | - | - | - | Stock vs benchmark |
| ComparePriceMomentumOscillator | :white_check_mark: | - | - | - | - | |
| KaufmanStressIndicator | :white_check_mark: | - | - | - | - | |
| RelativeNormalizedVolatility | :white_check_mark: | - | - | - | - | |
| RelativeStrength3DIndicator | :white_check_mark: | - | - | - | - | |
| SectorRotationModel | :white_check_mark: | - | - | - | - | |

---

## Ehlers Indicators

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| EhlersFisherTransform | :white_check_mark: | - | - | - | - | |
| EhlersHurstCoefficient | :white_check_mark: | - | - | - | - | Fixed numerical stability |
| EhlersAdaptiveCyberCycle | :white_check_mark: | - | - | - | - | |
| EhlersInstantaneousTrendline | :white_check_mark: | - | - | - | - | |
| EhlersMesa | :white_check_mark: | - | - | - | - | |
| EhlersMotherOfAdaptiveMovingAverages | :white_check_mark: | - | - | - | - | |

---

## All Other Indicators

*Remaining 650+ indicators to be categorized and validated*

See `IndicatorName.cs` for the complete list of all 716 indicators.

---

## Known Variants (Research Complete)

### Smoothing Method Variants

The library uses configurable smoothing via `MovingAvgType` parameter. All indicators with internal smoothing now support configurable smoothing types (Phase 7 complete).

| Smoothing Type | Formula | Common Usage | Notes |
|----------------|---------|--------------|-------|
| `WildersSmoothingMethod` | `k = 1/n` | RSI, ATR, ADX | Original Wilder smoothing |
| `ExponentialMovingAverage` | `k = 2/(n+1)` | MACD, most EMAs | Standard EMA |
| `SimpleMovingAverage` | `sum/n` | Stochastic %D, Bollinger | Simple average |
| `WeightedMovingAverage` | `weighted sum` | Hull MA components | Linear weights |

**Key Insight**: Wilder smoothing is slower than standard EMA. `Wilder(14)` ≈ `EMA(27)`.

### Core Indicator Variants

| Indicator | Variants | Our Default | Reference |
|-----------|----------|-------------|-----------|
| RSI | Wilder (original), Cutler (SMA) | **Wilder** (configurable) | Wilder 1978 |
| ATR | Wilder, SMA, EMA | **Wilder** (configurable) | Wilder 1978 |
| MACD | Standard EMA, Wilder EMA | **Standard EMA** | Appel |
| ADX | Wilder smoothing | **Wilder** | Wilder 1978 |
| Stochastic | Fast, Slow, Full | **All supported** | Lane |
| Bollinger Bands | SMA (original), EMA variant | **SMA** | Bollinger |

### Explicit V1/V2 Indicator Pairs (22 pairs)

These indicators have explicitly different algorithm versions, typically from different publications or improvements:

| Base Indicator | V1 | V2 | Notes |
|----------------|----|----|-------|
| DemarkPressureRatio | ✓ | ✓ | Different calculation methods |
| Ehlers2PoleButterworthFilter | ✓ | ✓ | Different pole configurations |
| Ehlers2PoleSuperSmootherFilter | ✓ | ✓ | Different smoothing approaches |
| Ehlers3PoleButterworthFilter | ✓ | ✓ | Different pole configurations |
| EhlersAdaptiveCommodityChannelIndex | ✓ | ✓ | Adaptive algorithm variants |
| EhlersAdaptiveRelativeStrengthIndex | ✓ | ✓ | Adaptive algorithm variants |
| EhlersAdaptiveRsiFisherTransform | ✓ | ✓ | Different transformation methods |
| EhlersAdaptiveStochasticIndicator | ✓ | ✓ | Adaptive algorithm variants |
| EhlersBandPassFilter | ✓ | ✓ | Different filter implementations |
| EhlersDecyclerOscillator | ✓ | ✓ | Different decycling methods |
| EhlersHighPassFilter | ✓ | ✓ | Different filter implementations |
| EhlersInstantaneousTrendline | ✓ | ✓ | Different trendline calculations |
| EhlersMesaPredictIndicator | ✓ | ✓ | Different MESA implementations |
| EhlersReverseExponentialMovingAverageIndicator | ✓ | ✓ | Different reverse EMA methods |
| EhlersRoofingFilter | ✓ | ✓ | Different roofing implementations |
| EhlersSignalToNoiseRatio | ✓ | ✓ | Different SNR calculations |
| EhlersSineWaveIndicator | ✓ | ✓ | Different sine wave detections |
| ElasticVolumeWeightedMovingAverage | ✓ | ✓ | Different elasticity calculations |
| ErgodicTrueStrengthIndex | ✓ | ✓ | Different TSI implementations |
| KaseDevStop | ✓ | ✓ | Different deviation calculations |
| KasePeakOscillator | ✓ | ✓ | Different peak oscillator methods |
| RelativeVolatilityIndex | ✓ | ✓ | Different RVI calculations |

### "Modified" Indicator Variants (8 indicators)

These are explicitly modified versions of original algorithms:

| Indicator | Notes |
|-----------|-------|
| EhlersModifiedOptimumEllipticFilter | Modified from original elliptic filter |
| EhlersModifiedRelativeStrengthIndex | Modified RSI with Ehlers improvements |
| EhlersModifiedStochasticIndicator | Modified stochastic with Ehlers improvements |
| ModifiedGannHiloActivator | Modified from standard Gann HiLo |
| ModifiedPriceVolumeTrend | Modified from standard PVT |
| OnBalanceVolumeModified | Modified from standard OBV |
| SharpModifiedMovingAverage | Modified MA with sharpness adjustments |
| VervoortModifiedBollingerBandIndicator | Vervoort's modified Bollinger implementation |

### Fast/Slow Indicator Variants (10 indicators)

These indicators have explicit fast/slow versions with different responsiveness:

| Base Concept | Fast | Slow | Notes |
|--------------|------|------|-------|
| Stochastic | StochasticFastOscillator | FastandSlowStochasticOscillator | Fast = no smoothing, Slow = smoothed |
| Kurtosis | FastandSlowKurtosisOscillator | - | Combined fast/slow output |
| RSI | FastandSlowRelativeStrengthIndexOscillator | ApirineSlowRelativeStrengthIndex | Different smoothing periods |
| ZScore | FastZScore, InverseFisherFastZScore | - | Shorter lookback |
| Degree | FastSlowDegreeOscillator | - | Combined output |
| Smoothed MA | - | SlowSmoothedMovingAverage | Extra smoothing |
| Turbo Stochastic | TurboStochasticsFast | TurboStochasticsSlow | Different turbo calculations |

### Configurable Smoothing Indicators (27 indicators - Phase 7)

These indicators now support configurable `MovingAvgType` parameters:

| Indicator | Configurable Parameters | Default |
|-----------|------------------------|---------|
| RecursiveRelativeStrengthIndex | rsiMaType | Wilder |
| RecursiveDifferenciator | rsiMaType | Wilder |
| RelativeSpreadStrength | rsiMaType | Wilder |
| ImpulseMovingAverageConvergenceDivergence | zlemaMaType | EMA |
| ImpulsePercentagePriceOscillator | zlemaMaType | EMA |
| OnBalanceVolume | signal maType | EMA |
| Trix | triple EMA maType | EMA |
| KasePeakOscillatorV1 | atrMaType, pkMaType, mnMaType | Wilder, SMA, SMA |
| KaseConvergenceDivergence | atrMaType, enginePkMaType, engineMnMaType | Wilder, SMA, SMA |
| InsyncIndex | stochKMaType, stochDMaType | SMA, SMA |
| RahulMohindarOscillator | rMaType, swingMaType, rmoMaType | SMA, EMA, EMA |
| QmaSmaDifference | compareMaType | SMA |
| TechnicalRatings | vwmaMaType, stochMaType, macdMaType | SMA, SMA, EMA |
| VolumeFlowIndicator | signalMaType | EMA |
| VolumePriceConfirmationIndicator | vwmaMaType | SMA |
| WamiOscillator | diffMaType | WMA |
| StiffnessIndicator | signalMaType | EMA |
| TechnicalRank | smaMaType, ppoMaType | SMA, EMA |
| VervoortModifiedBollingerBandIndicator | wmaMaType | WMA |
| VervoortSmoothedOscillator | rainbowMaType, zlrbMaType, temaMaType, wmaMaType | SMA, EMA, TEMA, WMA |
| KurtosisIndicator | slowMaType, fastMaType | EMA, WMA |
| LeastSquaresMovingAverage | smaMaType | SMA |
| LeoMovingAverage | smaMaType | SMA |
| MacZIndicator | wilderMaType | Wilder |
| HirashimaSugitaRS | emaMaType | EMA |
| HullEstimate | wmaMaType, emaMaType | WMA, EMA |
| EmaWaveIndicator | emaMaType, smoothMaType | EMA, SMA |

### Variant Selection (Future - Phase 5)

```csharp
// Simple usage with enum
catalog.Rsi(14, RsiVariant.Wilder);    // Default
catalog.Rsi(14, RsiVariant.Cutler);    // SMA-based

// Advanced usage with config
catalog.Rsi(new RsiOptions
{
    Length = 14,
    Variant = RsiVariant.Cutler,
    OverboughtLevel = 70,
    OversoldLevel = 30
});
```

---

## Validation Process

### Step 1: Property Tests (Automated)
Run `dotnet test --filter "FullyQualifiedName~ValidationTests"`

### Step 2: Reference Verification (Manual/Semi-automated)
1. Identify authoritative source (original paper, TA-Lib, TradingView)
2. Create golden file test with known input/output pairs
3. Verify our implementation matches

### Step 3: Document Variant
If multiple valid formulas exist:
1. Document which variant we implement
2. Consider adding config option for alternate variants
3. Update this table with variant notes

---

## Reference Sources

| Source | Priority | Notes |
|--------|----------|-------|
| Original Papers/Books | Highest | Wilder, Ehlers, Appel, etc. |
| TradingView | High | User-trusted, widely used |
| TA-Lib | Medium | Easier automated comparison |
| Investopedia | Low | General reference only |
