# V2 Migration Plan - Complete Breaking Change

**Created**: 2025-01-25
**Status**: In Progress
**Goal**: Complete replacement of v1 with NO v1 dependency

## Executive Summary

V2 is a **complete breaking change** and **full refactor** of v1. V1 was **never supposed to stick around**. All batch computation must be rewritten to use v2-native StatefulIndicators, eliminating all dependencies on v1 Calculate* methods.

---

## Current State Analysis

### V1 Components (TO BE REMOVED)

| Component | Count | Location |
|-----------|-------|----------|
| Calculate* methods | 773 | `src/Calculations/**/*.cs` |
| IndicatorInvoker (reflection) | 1 | `src/Builder/Evaluation/IndicatorInvoker.cs` |
| ApplyIndicator (v1 calls) | 1 | `src/Builder/Evaluation/SeriesEvaluator.cs` |
| ApplyMultiStockIndicator (v1 calls) | 1 | `src/Builder/Evaluation/SeriesEvaluator.cs` |

### V2 Components (ALREADY EXIST)

| Component | Count | Location |
|-----------|-------|----------|
| StatefulIndicator classes | 768 | `src/Streaming/StatefulIndicators*.cs` |
| IndicatorCompute fast path | 100+ | `src/Builder/Compute/IndicatorCompute.cs` |
| IStreamingIndicatorState interface | 1 | `src/Streaming/StreamingIndicatorState.cs` |

### Key Insight

**768 v2-native StatefulIndicator implementations already exist** - they are currently used for streaming mode. The plan is to use these same implementations for batch computation by iterating through historical data.

---

## Phase 1: Rewrite SeriesEvaluator Batch Computation

### Goal
Replace all v1 Calculate* calls with v2 StatefulIndicator-based computation.

### Tasks

#### 1.1 Create StatefulIndicatorFactory
- Create a factory that maps `IndicatorSpec` to the appropriate `IStreamingIndicatorState` implementation
- Handle all indicator parameters and options
- Support all 768 indicator types

**File**: `src/Builder/Evaluation/StatefulIndicatorFactory.cs`

```csharp
internal static class StatefulIndicatorFactory
{
    public static IStreamingIndicatorState Create(IndicatorSpec spec)
    {
        return spec.Options switch
        {
            SmaSpecOptions sma => new SimpleMovingAverageState(sma.Length),
            EmaSpecOptions ema => new ExponentialMovingAverageState(ema.Length),
            RsiSpecOptions rsi => new RelativeStrengthIndexState(rsi.Length),
            // ... all 768 indicators
            _ => throw new NotSupportedException($"Indicator '{spec.Name}' not supported in v2.")
        };
    }
}
```

#### 1.2 Create BatchCompute Helper
- Create a helper that processes all bars through a StatefulIndicator
- Returns double[] of results (same interface as current SeriesEvaluator)

**File**: `src/Builder/Evaluation/BatchCompute.cs`

```csharp
internal static class BatchCompute
{
    public static double[] ComputeAll(StockData data, IStreamingIndicatorState state)
    {
        var count = data.Count;
        var results = new double[count];
        state.Reset();

        for (var i = 0; i < count; i++)
        {
            var bar = new OhlcvBar(
                data.Dates[i],
                data.OpenPrices[i],
                data.HighPrices[i],
                data.LowPrices[i],
                data.ClosePrices[i],
                data.Volumes[i]);

            var result = state.Update(bar, isFinal: true, includeOutputs: false);
            results[i] = result.Value;
        }

        return results;
    }
}
```

#### 1.3 Modify SeriesEvaluator.ResolveIndicator
- Replace `ApplyIndicator` with `BatchCompute` using `StatefulIndicatorFactory`
- Remove all v1 Calculate* method calls

**Before** (v1):
```csharp
var result = ApplyIndicator(baseData, node.Spec);
return ExtractOutput(result, node.Spec);
```

**After** (v2):
```csharp
var state = StatefulIndicatorFactory.Create(node.Spec);
return BatchCompute.ComputeAll(baseData, state);
```

#### 1.4 Modify SeriesEvaluator.ResolveMultiStockIndicator
- Create multi-stock versions of StatefulIndicators
- Remove `ApplyMultiStockIndicator` and all v1 calls

#### 1.5 Delete IndicatorInvoker.cs
- Remove the reflection-based v1 fallback completely
- All indicators must have v2 StatefulIndicator implementations

---

## Phase 2: Multi-Stock Indicators V2

### Goal
Implement all 6 multi-stock comparison indicators using v2 patterns.

### Multi-Stock Indicators to Convert

1. **RSMKIndicator** - Relative Strength Market Comparison
2. **ComparePriceMomentumOscillator** - Compare momentum vs benchmark
3. **KaufmanStressIndicator** - Market stress relative to benchmark
4. **RelativeNormalizedVolatility** - Volatility comparison
5. **RelativeStrength3DIndicator** - 3D relative strength
6. **SectorRotationModel** - Sector vs market rotation

### Tasks

#### 2.1 Create Multi-Stock StatefulIndicator Base
```csharp
public interface IMultiStockIndicatorState
{
    IndicatorName Name { get; }
    void Reset();
    StreamingIndicatorStateResult Update(OhlcvBar stockBar, OhlcvBar marketBar, bool isFinal, bool includeOutputs);
}
```

#### 2.2 Implement Each Multi-Stock Indicator
Create v2-native implementations for each:
- `RSMKIndicatorState`
- `ComparePriceMomentumOscillatorState`
- `KaufmanStressIndicatorState`
- `RelativeNormalizedVolatilityState`
- `RelativeStrength3DIndicatorState`
- `SectorRotationModelState`

#### 2.3 Update BatchCompute for Multi-Stock
```csharp
public static double[] ComputeMultiStock(
    StockData stockData,
    StockData marketData,
    IMultiStockIndicatorState state)
{
    var count = Math.Min(stockData.Count, marketData.Count);
    var results = new double[count];
    state.Reset();

    for (var i = 0; i < count; i++)
    {
        var stockBar = CreateBar(stockData, i);
        var marketBar = CreateBar(marketData, i);
        var result = state.Update(stockBar, marketBar, isFinal: true, includeOutputs: false);
        results[i] = result.Value;
    }

    return results;
}
```

---

## Phase 3: Signals, Auto-Trading, Backtesting

### Current Status
These features have catalog classes but implementation uses v1:
- `SignalCatalog` - Signal generation
- `AutoTradingCatalog` - Automated trading rules
- `BacktestOptions` - Backtesting configuration

### Tasks

#### 3.1 Verify Signal Generation Works with V2
- Signals consume indicator values from SeriesHandle
- Once SeriesEvaluator uses v2, signals should work automatically

#### 3.2 Implement Auto-Trading Engine
- Create `AutoTradingEngine` that processes signals
- Generate trade entries/exits based on configured rules

#### 3.3 Implement Backtesting Engine
- Create `BacktestEngine` that simulates trades
- Track performance metrics (returns, drawdown, Sharpe ratio, etc.)
- Support position sizing and risk management

---

## Phase 4: Remove V1 Code

### Prerequisites
- All 768 indicators work via v2 batch computation
- All tests pass with v2 implementation
- Performance benchmarks show acceptable results

### Tasks

#### 4.1 Mark V1 as Obsolete
First, mark all v1 methods as obsolete with clear migration guidance:
```csharp
[Obsolete("V1 API is deprecated. Use StockIndicatorBuilder instead.", error: true)]
public static StockData CalculateSimpleMovingAverage(this StockData data, int length = 14)
```

#### 4.2 Remove V1 Files
Delete the following directories/files:
- `src/Calculations/**/*` (all 76 files with 773 methods)
- `src/Builder/Evaluation/IndicatorInvoker.cs`

#### 4.3 Update Public API
- Remove extension methods from `Calculations` class
- Document migration path in README

---

## Phase 5: Testing and Validation

### Tasks

#### 5.1 Numerical Accuracy Tests
- Compare v2 batch results against known-good v1 results
- Ensure all indicators produce identical output

#### 5.2 Performance Benchmarks
- Compare v2 batch performance vs v1
- Optimize StatefulIndicator implementations if needed

#### 5.3 Edge Case Tests
- All 768 indicators with edge cases (empty data, single bar, NaN values)
- Multi-stock indicators with mismatched data lengths

#### 5.4 Integration Tests
- Full builder pipeline tests
- Streaming and batch mode consistency

---

## Implementation Order

| Order | Phase | Description | Priority |
|-------|-------|-------------|----------|
| 1 | 1.1 | StatefulIndicatorFactory | CRITICAL |
| 2 | 1.2 | BatchCompute helper | CRITICAL |
| 3 | 1.3 | Modify ResolveIndicator | CRITICAL |
| 4 | 1.4 | Modify ResolveMultiStockIndicator | CRITICAL |
| 5 | 2.1-2.3 | Multi-stock v2 implementations | HIGH |
| 6 | 5.1 | Numerical accuracy tests | HIGH |
| 7 | 1.5 | Delete IndicatorInvoker | HIGH |
| 8 | 3.1-3.3 | Signals/Trading/Backtesting | MEDIUM |
| 9 | 5.2-5.4 | Performance and integration tests | MEDIUM |
| 10 | 4.1-4.3 | Remove all v1 code | FINAL |

---

## Success Criteria

1. **Zero v1 dependencies**: No code in `src/Builder` calls any v1 Calculate* method
2. **All tests pass**: 971+ tests pass with v2-only implementation
3. **Performance acceptable**: Batch computation not more than 10% slower than v1
4. **API stability**: Public builder API unchanged
5. **Feature complete**: Signals, auto-trading, and backtesting fully functional

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Missing StatefulIndicator | Already have 768/773 - only 5 gap |
| Numerical differences | Extensive accuracy testing against v1 |
| Performance regression | IndicatorCompute fast path already exists |
| Breaking existing users | V2 is explicitly a breaking change |

---

## Notes

- **This plan must be saved to persist across context compactions**
- V2 was always intended to be a complete replacement
- V1 was never supposed to remain in the codebase
- All indicator computation will use StatefulIndicators
