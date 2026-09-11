# Migration Guide: v1.x to v2.0

This guide helps you migrate from OoplesFinance.StockIndicators v1.x to v2.0.

## Overview of Changes

### Why We Made Breaking Changes

Version 2.0 introduces a new fluent builder API that provides:
- **Zero-allocation hot paths** using `ArrayPool<T>` for indicator buffers
- **SIMD-optimized calculations** for math operations
- **Unified batch/streaming support** through `IndicatorDataSource`
- **Signal system** for trading alerts with group conditions
- **Notification adapters** (Email, SMS, Webhook, Telegram, Discord)
- **Auto-trading integration** (Console, Alpaca)
- **Lazy evaluation** for better memory efficiency

### Performance Improvements

| Operation | v1.x | v2.0 | Improvement |
|-----------|------|------|-------------|
| Buffer allocations | Multiple per indicator | Zero (ArrayPool) | ~100% reduction |
| Math operations | Scalar | SIMD/loop-unrolled | 2-4x faster |
| Multi-indicator chains | Compute all | Lazy evaluation | Compute only needed |

## Quick Start: Minimum Changes

If you just want to get your existing code working quickly, the old `Calculate*` methods still work. However, they will show deprecation warnings in future versions.

```csharp
// v1.x code - still works in v2.0
var stockData = new StockData(opens, highs, lows, closes, volumes, dates);
var result = stockData.CalculateSimpleMovingAverage(20);
var smaValues = result.OutputValues1.TakeLast(100).ToList();
```

## Full Migration

### Step 1: Create an IndicatorDataSource

```csharp
// v1.x
var stockData = new StockData(opens, highs, lows, closes, volumes, dates);

// v2.0
var stockData = new StockData(opens, highs, lows, closes, volumes, dates);
var source = IndicatorDataSource.FromBatch(stockData);
```

### Step 2: Use the StockIndicatorBuilder

```csharp
// v1.x - Chain Calculate* calls
var result = stockData
    .CalculateSimpleMovingAverage(20)
    .CalculateRelativeStrengthIndex(14);

// v2.0 - Use builder pattern
var builder = new StockIndicatorBuilder(source)
    .ConfigureIndicators(indicators =>
    {
        var sma = indicators.Sma(20);
        var rsi = indicators.Rsi(14);
    });

using var runtime = builder.Build();
```

### Step 3: Access Results

```csharp
// v1.x
var smaValues = result.OutputValues1;

// v2.0 - Get typed buffer
var smaBuffer = runtime.GetSeries(smaHandle);
var smaSpan = smaBuffer.AsSpan(); // Zero-allocation access
// Or convert to list when needed:
var smaValues = smaBuffer.ToList();
```

### Step 4: Add Signals (New in v2.0)

```csharp
// Declare handles at outer scope for cross-lambda access
SeriesHandle rsi = default;

var builder = new StockIndicatorBuilder(source)
    .ConfigureIndicators(indicators =>
    {
        rsi = indicators.Rsi(14);
    })
    .ConfigureSignals(signals =>
    {
        var overbought = signals.When(rsi).CrossesAbove(70).Emit("overbought");
        var oversold = signals.When(rsi).CrossesBelow(30).Emit("oversold");
    })
    .ConfigureNotifications(notify =>
    {
        notify.Console();
        notify.Email(new EmailOptions { To = "alerts@example.com" });
    });
```

## API Mapping

### Moving Averages

| v1.x Method | v2.0 Equivalent |
|-------------|-----------------|
| `CalculateSimpleMovingAverage(20)` | `indicators.Sma(20)` |
| `CalculateExponentialMovingAverage(20)` | `indicators.Ema(20)` |
| `CalculateWeightedMovingAverage(20)` | `indicators.Wma(20)` |
| `CalculateHullMovingAverage(20)` | `indicators.Hma(20)` |

### Oscillators

| v1.x Method | v2.0 Equivalent |
|-------------|-----------------|
| `CalculateRelativeStrengthIndex(14)` | `indicators.Rsi(14)` |
| `CalculateCommodityChannelIndex(20)` | `indicators.Cci(20)` |
| `CalculateWilliamsR(14)` | `indicators.WilliamsR(14)` |

### Multi-Output Indicators

```csharp
// v1.x - Access multiple outputs via OutputValues1, OutputValues2, etc.
var bb = stockData.CalculateBollingerBands(20, 2);
var upper = bb.OutputValues1;
var middle = bb.OutputValues2;
var lower = bb.OutputValues3;

// v2.0 - Get typed series
var bb = indicators.BollingerBands(20, 2);
var upperBuffer = runtime.GetSeries(bb.Upper);
var middleBuffer = runtime.GetSeries(bb.Middle);
var lowerBuffer = runtime.GetSeries(bb.Lower);
```

### Generic Indicator Access

For any indicator not explicitly listed in the catalog:

```csharp
// v2.0 - Access any of 750+ indicators by name
var handle = indicators.Calculate(
    IndicatorName.AdaptiveMovingAverage,
    parameters: new object[] { 10, 2.0 });
```

## Breaking Changes

### Custom input: one mechanism for every streaming indicator

Every streaming state used to take custom input through its own selector constructor -
`new RsiState(14, bar => ...)` - and 82 of them had none, so some indicators could not take custom
values at all. Those per-state selector constructors are removed. `CustomInputState` wraps any state
instead, and ready-made `InputSeries` presets are named after the input names they replace.

```csharp
// Before
var rsi = new RelativeStrengthIndexState(14, 3, bar => (bar.High + bar.Low) / 2);

// After - a preset
var rsi = new CustomInputState(new RelativeStrengthIndexState(14), InputSeries.MedianPrice);

// After - any function of the bar
var rsi = new CustomInputState(new RelativeStrengthIndexState(14), bar => (bar.High + bar.Low) / 2);

// After - another indicator's output (streaming chaining)
var rsi = new CustomInputState(new RelativeStrengthIndexState(14), InputSeries.Of(new MidpointState(14)));
```

The same presets work in batch, where `UseInput` chains a series:

```csharp
var rsi = stockData.UseInput(InputSeries.MedianPrice).CalculateRelativeStrengthIndex(length: 14);
```

| Input | Preset |
|---|---|
| close, adjusted close, open, high, low, volume | `InputSeries.Close`, `.AdjustedClose`, `.Open`, `.High`, `.Low`, `.Volume` |
| median, typical, full typical, weighted close, average price | `InputSeries.MedianPrice`, `.TypicalPrice`, `.FullTypicalPrice`, `.WeightedClose`, `.AveragePrice` |
| midpoint, midprice over *n* bars | `InputSeries.Midpoint(n)`, `InputSeries.Midprice(n)` |
| any function of the bar | `InputSeries.Of(bar => ...)` |
| another indicator's output | `InputSeries.Of(state)` |

A custom series changes more than the close: when its value lies outside the bar's range, the
indicator's high and low come from the series itself (the max and min of its previous and current
value). Batch applies the same rule to a chained series, so the two engines give the same numbers.

### InputName is removed

Callers pass values, not a name for them. `InputName` is gone from every indicator constructor, every
`Calculate*` method, `StockData`, and the streaming options. An indicator built with no input named reads
exactly what it read by default before; to compute it on something else, pass the series.

```csharp
// Streaming - default input: just drop the argument
var cci = new CommodityChannelIndexState(InputName.TypicalPrice, MovingAvgType.SimpleMovingAverage, 20); // before
var cci = new CommodityChannelIndexState(MovingAvgType.SimpleMovingAverage, 20);                         // after

// Streaming - a different input: wrap the state
var cci = new CustomInputState(new CommodityChannelIndexState(), InputSeries.MedianPrice);

// Batch - default input: just drop the argument
var ao = data.CalculateAwesomeOscillator(MovingAvgType.SimpleMovingAverage, InputName.MedianPrice); // before
var ao = data.CalculateAwesomeOscillator(MovingAvgType.SimpleMovingAverage);                         // after

// Batch - a different input: chain it
var ao = data.UseInput(InputSeries.TypicalPrice).CalculateAwesomeOscillator();
```

| Removed | Replacement |
|---|---|
| `InputName` parameter on a streaming state | drop it for the default; `new CustomInputState(state, InputSeries.X)` otherwise |
| `inputName` parameter on a `Calculate*` method | drop it for the default; `data.UseInput(InputSeries.X).CalculateY()` otherwise |
| `new StockData(tickers, InputName.X)` and `StockData.InputName` | `new StockData(tickers).UseInput(InputSeries.X)` |
| `StreamingOptions.InputName`, `IndicatorSubscriptionOptions.InputName` | a `CustomInputState` per indicator |
| `VolumeFlowIndicatorSpecOptions(inputName, ...)` | nothing - it was never read |
| the `InputName` enum, `StreamingInputSelector`, `GetInputValuesList(InputName, StockData)` | now internal; name a series with `InputSeries` |

**This can change results, in the direction of correctness.** `StockData`'s input name was stored and then
ignored by about 650 indicators: `new StockData(tickers, InputName.MedianPrice).CalculateRsi()` computed an
RSI of the close (#182). `new StockData(tickers).UseInput(InputSeries.MedianPrice).CalculateRsi()` really
computes it on the median price. The same holds for the two streaming options, which fed that same ignored
value, and for `VolumeFlowIndicatorSpecOptions`, whose input name no calculation ever read.

### Removed

- None - all v1.x methods are still available (will be deprecated in v3.0)

### Changed

- `IndicatorBuffer<T>` is now the primary container for indicator values (replaces raw lists)
- Results are accessed via `runtime.GetSeries(handle)` instead of `OutputValues*` properties

### Corrected values

Every streaming state is now held to computing, on every bar, every value its batch twin computes
(`StreamingBatchValueParityTests`, all 769 states). The hand-written parity specs had covered a subset,
often only a band indicator's middle band, and 50 indicators disagreed between engines. Each was fixed to
the indicator's published definition, so **some batch values change**:

- **Moving averages no longer leak into the next calculation.** `GetMovingAverageList` left its result on
  `CustomValuesList`, so whatever an indicator calculated next ran on the average rather than the price.
- **Bollinger Bands** use the population standard deviation of the prices, as Bollinger defines them. The
  batch bands were about 55% too wide at steady state and far wider during warmup. %B, Width, the Bayesian
  Oscillator, BB-ATR, the Narrow Sideways Channel and Waddah Attar Explosion's bands follow.
- **Composite indicators read the prices in every component** instead of the previous component's output:
  Pring Special K, Technical Ratings, Technical Rank, Trading Made More Simpler Oscillator, Waddah Attar
  Explosion, Woodie CCI, Ultimate Momentum Indicator, Ultimate Moving Average and its bands, QMA-SMA
  Difference, Hurst Cycle Channel, R2 Adaptive Regression and T-Step LSMA.
- **A stochastic of a derived series is taken over that series' own range**, not the bars' highs and lows:
  Schaff Trend Cycle, Strength of Movement, and Technical Ratings' Stochastic RSI.
- **Corrected Moving Average, 1LC LSMA and the Linear Regression Line** use the true variance and standard
  deviation of the source.
- **`MovingAvgType` fast paths compute the indicator of the same name** for the Variable Moving Average
  (now LazyBear's formula, which also corrects the VMA indicator itself), VIDYA, McNicholl, Ehlers' Noise
  Elimination Technology and the Zero-Lag TEMA.
- **Z Distance from VWAP and MAC-Z VWAP** compute LazyBear's `calc_zvwap`.
- **Sortino Ratio** sums its downside window exactly, so a window with no downside is 0 instead of a
  rounding residue that inflated the ratio to around 1e7.
- **Gopalakrishnan Range Index** publishes its `Signal` series, which was always empty.
- **Window calculations no longer drift over a long series** (`StreamingLongRunStabilityTests`). After
  100,000 bars near 100,000 and 100,000 near 10, the weighted moving average was 1e-6 off and the standard
  deviation channel 3e-4 off in both engines, carrying rounding from values long out of the window. The
  simple and weighted moving averages now rebuild their running sums from the window every `length` bars,
  and window sums taken as the difference of two prefix sums (about 120 batch indicators, among them CMO,
  MFI and Vortex) keep each prefix as a compensated pair.
  The linear regression counts x from the window's first bar instead of the series' (its `Intercept` is
  still reported at bar 0), slides in O(1) with the same periodic rebuild, and fits the bars there are
  while its window fills, rather than dividing by the full length as though the missing points sat at the
  origin; Chande Forecast, the standard deviation channel, Inertia and Projection Bands follow. Correlation is taken from each value's distance to the window mean, so a
  window with one side constant correlates at 0 rather than a rounding residue of either sign; the
  Periodic Channel sums that sign. Otherwise values change only in their last digits.
- **`IncludeCustomValues = false` hides a result without changing any.** It used to empty the one list
  that both the caller and the next calculation read, so a chain ran on the close, 176 indicators threw
  and the Accelerator, Derivative and McClellan oscillators computed other values
  (`IncludeCustomValuesTests`). Every indicator now computes with the option off exactly what it computes
  with it on, and only `CustomValuesList` is empty. `Clear()` gives the data a new, empty series rather
  than emptying the list in place, so a list you kept from an earlier result survives it; setting
  `CustomValuesList` to null now reads back as an empty list.
- **`IncludeOutputValues = false` and `RoundingDigits` change only what is published**, the same way
  (`IncludeOutputValuesTests`, `RoundingDigitsTests`). With output values off, 56 indicators threw reading a
  component's named series from the emptied dictionary; it is now replaced rather than cleared in place.
  With rounding on, 187 indicators computed on rounded values - a component's result, or an intermediate
  series handed on as input - so their final digits were the rounding of a different computation. Every
  indicator now computes on unrounded values and rounds only what it publishes.
- **Four indicators take each component of the price, as they are defined.** Each component call publishes
  its result for the next one, and these called the next component without handing back the caller's series;
  their streaming states copied the chain. CCT StochRSI took every RSI after the first of the RSI before it,
  the Fast and Slow RSI Oscillator took its kurtosis term of the RSI, and the Sector Rotation Model took its
  second rate of change of the first. Connors RSI ranked a 100-bar rate of change of the RSI; it ranks the
  one-bar rate of change of the price over 100 bars, as Connors defines it, and the Stochastic Connors RSI and
  Quasi White Noise built on it follow.
- **Adaptive Ehlers windows take a cycle within float noise of an integer as that integer** before
  rounding up to whole bars. A dominant cycle of exactly 29 in exact arithmetic could arrive as
  29.000000000000004 and average over 30 bars, and which side it fell depended on summation order. Values
  move only on bars where the cycle sat within a relative 1e-9 of an integer.

Streaming-only corrections (batch unchanged): the first bar's true range in the ATR channels, Stoller
channels, dynamic support/resistance, Bollinger Fibonacci ratios, Hurst cycle channel, trend trader bands,
VMA bands, Trender and the volume positive/negative indicator; the Time Price Indicator's band offset; the
defaults of the Ergodic Mean Deviation Indicator (signal length 5) and Quadratic Least Squares MA (length
50); VIDYA's seed; and the Trend Analysis Index, Trender and Vervoort Smoothed Oscillator deviations. The
first bar's true range in the Grover Llorens Cycle Oscillator and the Ultimate Trader Oscillator is also
High - Low now, not the whole high.

A preview (`isFinal: false`) of a bar now publishes what that bar publishes once final
(`StreamingPreviewTests`, every state). Nine did not: ALMA, Interquartile Range Bands and Trimean left the
forming bar out of their window; Alligator, Gator and the Ehlers Fractal Adaptive Moving Average read their
displaced value a bar late; and Connors RSI, with the Stochastic Connors RSI and Quasi White Noise built on
it, ranked the forming value against a value the commit evicts. The autocorrelation periodogram behind the
adaptive Ehlers indicators divided the previous bar's powers by the forming bar's maximum.

`BollingerBandsState` takes an optional `maType`, and `CalculateVolatilityIndexDynamicAverageIndicator` is
the batch twin of the streaming state of the same name.

**`MovingAvgType` means the indicator of that name.** `GetMovingAverageList` smoothed through span fast
paths meant to reproduce the indicator of the same name, and 120 of 162 did not (a different formula, a
different warmup, or a numerical blow-up). Every type is now computed by its indicator unless its fast path
is verified to match it (`MovingAverageFastPathTests` holds every type to its indicator). Batch indicators
that smooth with one of the 118 unverified types, whether by default or through a `maType` you pass, give
the indicator's values. Routing also corrected the indicators it exposed:

- **Kaufman's Adaptive Moving Average** passes the price through until its efficiency window fills, then
  recurses from it, as TA-Lib and Pine seed it. It was seeded at 0 in both engines, crawled up from zero, and
  on a flat market was still converging thousands of bars later.
- **Linear Regression** fits the bars there are during its warmup rather than the full length, as though
  the missing points sat at the origin. Projection Bands, Bandwidth and Oscillator follow.
- Asking `GetMovingAverageList` for the dynamically adjustable, adaptive, Ehlers adaptive Laguerre or middle
  high-low average without a fast length now uses the indicator's own default instead of 0.

### New Dependencies (net461 only)

- `System.Net.Http` 4.3.4
- `System.Text.Json` 6.0.11
- `System.Memory` 4.5.5
- `System.Buffers` 4.5.1

## FAQ

### Q: Do I have to migrate everything at once?

No. You can use the old `Calculate*` methods alongside the new builder API. They share the same underlying calculations.

### Q: Will my existing code break?

No. All v1.x methods remain functional. You'll see deprecation warnings starting in a future release.

### Q: How do I get the best performance?

1. Use `IndicatorBuffer<double>.AsSpan()` for zero-allocation access
2. Dispose `IndicatorRuntime` when done to return buffers to the pool
3. Use `ConfigureSignals` to enable lazy evaluation - only computed indicators are materialized

### Q: Can I still use LINQ on results?

Yes. `IndicatorBuffer<T>` implements `IReadOnlyList<T>` and works with all LINQ methods. However, for best performance, use `AsSpan()` when possible.

### Q: How do I handle streaming data?

```csharp
// Create streaming source
var source = IndicatorDataSource.FromStreaming(streamSource);

var builder = new StockIndicatorBuilder(source)
    .ConfigureIndicators(i => i.Sma(20))
    .ConfigureSignals(s => s.When(sma).CrossesAbove(100).Emit());

using var runtime = builder.Build();
// Runtime will process streaming updates automatically
```

## Getting Help

- [GitHub Issues](https://github.com/ooples/OoplesFinance.StockIndicators/issues)
- [Full Documentation](https://github.com/ooples/OoplesFinance.StockIndicators/wiki)
