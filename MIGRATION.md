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

### Removed

- All v1.x `Calculate*` methods are still available (they will be deprecated in v3.0).
- `TrixResult` is gone. `IndicatorCatalog.Trix()` now returns a plain `SeriesHandle`.
- `AroonOscillatorResult.Up`, `.Down` and `.Oscillator` are gone, as are
  `AlligatorIndexResult.Jaw` and `GatorOscillatorResult.Upper` / `.Lower`. The result types
  survive; the members are renamed to the outputs the indicators actually publish.

#### Why: the removed members never worked

The catalog generator was given a hand-maintained list of which indicators publish more than one
output and under what names. That list had drifted from the calculations in five of its six entries:

| Indicator | The list said | The calculation publishes |
|---|---|---|
| `AroonOscillator` | `Up`, `Down`, `Oscillator` | `Aroon`, `AroonUp`, `AroonDown` |
| `AlligatorIndex` | `Jaw`, `Teeth`, `Lips` | `Lips`, `Teeth`, `Jaws` |
| `GatorOscillator` | `Upper`, `Lower` | `Top`, `Bottom` |
| `Trix` | `Trix`, `Signal` | one output only |
| `PPO` | `Ppo`, `Signal`, `Histogram` | (key never matched `IndicatorName`) |
| `ElderRayIndex` | `BullPower`, `BearPower` | `BullPower`, `BearPower` |

Handles were generated for outputs that do not exist, and resolving a handle for an output an
indicator does not publish fell back to the primary series without raising anything. So
`aroon.Up`, `aroon.Down` and `aroon.Oscillator` were three names for one series, `trix.Trix` and
`trix.Signal` were the same series, and every band of `gator` and `alligator` came back identical.
The names are now read out of the calculations' own `SetOutputValues` calls, so the catalog can no
longer describe an output that is not there.

`PPO` is deliberately still a plain handle: its list key never matched the `IndicatorName` member,
so it has always returned one, and giving it a result type now would be a new API rather than a
repair.

#### Migrating

```csharp
// Before - .Up, .Down and .Oscillator were three names for the oscillator series
var aroon = indicators.AroonOscillator(25);
var osc = runtime.GetSeries(aroon.Oscillator);
var up = runtime.GetSeries(aroon.Up);       // same series as osc

// After - Aroon is the oscillator; AroonUp and AroonDown are the real component series
var aroon = indicators.AroonOscillator(25);
var osc = runtime.GetSeries(aroon.Aroon);
var up = runtime.GetSeries(aroon.AroonUp);
var down = runtime.GetSeries(aroon.AroonDown);
```

```csharp
// Before - .Signal was the same series as .Trix
var trix = indicators.Trix(14);
var line = runtime.GetSeries(trix.Trix);

// After - Trix publishes one series, so the catalog returns one handle
var trix = indicators.Trix(14);
var line = runtime.GetSeries(trix);
```

If you need a Trix signal line, chain a moving average over the Trix handle yourself; the catalog no
longer supplies one that is secretly the Trix series itself.

```csharp
// Before - .Jaw silently resolved to the primary series (Lips)
var alligator = indicators.AlligatorIndex();
var jaw = runtime.GetSeries(alligator.Jaw);

// After - the member is named for the output the indicator publishes
var alligator = indicators.AlligatorIndex();
var jaws = runtime.GetSeries(alligator.Jaws);
```

```csharp
// Before - Upper and Lower both resolved to the primary series
var gator = indicators.GatorOscillator();
var upper = runtime.GetSeries(gator.Upper);
var lower = runtime.GetSeries(gator.Lower);

// After
var gator = indicators.GatorOscillator();
var top = runtime.GetSeries(gator.Top);
var bottom = runtime.GetSeries(gator.Bottom);
```

`ElderRayIndexResult` is unchanged: it is the one entry the old list had right.

### Changed

- `IndicatorBuffer<T>` is now the primary container for indicator values (replaces raw lists)
- Results are accessed via `runtime.GetSeries(handle)` instead of `OutputValues*` properties

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
