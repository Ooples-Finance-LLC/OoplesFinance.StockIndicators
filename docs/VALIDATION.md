# V2 Indicator Validation Status

**Last Updated**: 2025-01-26
**Total Indicators**: 716
**Validated**: 0
**Needs Review**: 0
**Has Variants**: TBD (research needed)

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
| SimpleMovingAverage | :white_check_mark: | - | - | - | :construction: | |
| ExponentialMovingAverage | :white_check_mark: | - | - | :question: | :construction: | Wilder vs Standard EMA |
| WeightedMovingAverage | :white_check_mark: | - | - | - | :construction: | |
| TriangularMovingAverage | :white_check_mark: | - | - | - | - | |
| DoubleExponentialMovingAverage | :white_check_mark: | - | - | - | - | |
| TripleExponentialMovingAverage | :white_check_mark: | - | - | - | - | |
| HullMovingAverage | :white_check_mark: | - | - | - | - | |
| KaufmanAdaptiveMovingAverage | :white_check_mark: | - | - | - | - | |
| VariableIndexDynamicAverage | :white_check_mark: | - | - | - | - | |
| Tema | :white_check_mark: | - | - | - | - | |

---

## Oscillators (Bounded 0-100)

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| RelativeStrengthIndex | :white_check_mark: | - | - | :question: | :construction: | Wilder vs Cutler smoothing |
| StochasticOscillator | :white_check_mark: | - | - | - | - | |
| MoneyFlowIndex | :white_check_mark: | - | - | - | - | |
| CommodityChannelIndex | :white_check_mark: | - | - | - | - | |
| UltimateOscillator | :white_check_mark: | - | - | - | - | |
| ChaikinMoneyFlow | :white_check_mark: | - | - | - | - | |
| AccumulationDistributionOscillator | :white_check_mark: | - | - | - | - | |
| AroonOscillator | :white_check_mark: | - | - | - | - | |
| BalanceOfPower | :white_check_mark: | - | - | - | - | |
| ConnorsRsi | :white_check_mark: | - | - | - | - | |

---

## Oscillators (Bounded -100 to 100)

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| WilliamsR | :white_check_mark: | - | - | - | - | |
| PercentagePriceOscillator | :white_check_mark: | - | - | - | - | |
| PercentageVolumeOscillator | :white_check_mark: | - | - | - | - | |
| StochasticMomentumIndex | :white_check_mark: | - | - | - | - | |
| TrueStrengthIndex | :white_check_mark: | - | - | - | - | |

---

## Volatility (Non-Negative)

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| AverageTrueRange | :white_check_mark: | - | - | :question: | :construction: | Wilder vs SMA smoothing |
| StandardDeviation | :white_check_mark: | - | - | - | - | |
| BollingerBands | :white_check_mark: | - | - | - | - | |
| KeltnerChannels | :white_check_mark: | - | - | - | - | |
| AverageTrueRangePercent | :white_check_mark: | - | - | - | - | |
| Variance | :white_check_mark: | - | - | - | - | |

---

## Trend Indicators

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| Macd | :white_check_mark: | - | - | :question: | :construction: | EMA variant affects values |
| MovingAverageConvergenceDivergence | :white_check_mark: | - | - | - | - | |
| AverageDirectionalIndex | :white_check_mark: | - | - | - | - | |
| ParabolicSar | :white_check_mark: | - | - | - | - | |
| SuperTrend | :white_check_mark: | - | - | - | - | |
| Adx | :white_check_mark: | - | - | - | - | |
| DirectionalMovementIndex | :white_check_mark: | - | - | - | - | |

---

## Volume Indicators

| Indicator | Property | Reference | Edge | Variants | Status | Notes |
|-----------|:--------:|:---------:|:----:|:--------:|:------:|-------|
| OnBalanceVolume | :white_check_mark: | - | - | - | - | |
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

## Known Variants (Research Needed)

The following indicators are known to have multiple legitimate formula variants:

| Indicator | Variants | Our Implementation | Reference |
|-----------|----------|-------------------|-----------|
| RSI | Wilder (original), Cutler | TBD | Wilder 1978 |
| EMA | Wilder (2/(n+1)), Standard | TBD | - |
| ATR | Wilder smoothing, SMA | TBD | Wilder 1978 |
| MACD | Different EMA periods | TBD | Appel |
| Stochastic | Fast, Slow, Full | TBD | Lane |

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
