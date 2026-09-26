# Migration Guide: v1.x to v2.0

This guide helps you migrate from OoplesFinance.StockIndicators v1.x to v2.0.

## Corrections exposed by shared validation

- Chande Momentum Oscillator now sums exact price changes and rounds only its final ratio,
  preserving its [-100,100] bound for extreme and subnormal finite prices. Its absolute variant
  retains full-window startup suppression and fixes the core's incorrect first eviction.
- VIDYA uses the corrected Chande momentum and an exact convex price blend with one final rounding.
  Its first-price seed remains unchanged; low-order bits may differ from earlier releases.
- Customer averages can now smooth Chande's Signal while its primary remains independent.
  RSI rejects an insufficient single customer average instead of silently applying it only to Signal.

- DEnvelope uses its finite period-one limit (`2 * current - previous`) instead of zero,
  and floors its filtered deviation at zero to prevent inverted bands after sharp moves.
- Nick Rypock Trailing Reverse retains its trend until a threshold crossing. Previously the
  bearish state reset after a bar without a reversal and could prematurely flip the stop.

- Linear Trailing Stop switches to its bearish side below the lower band and retains its side
  inside the channel. The old comparison switched inside the channel and missed downside breaks.
- Motion to Attraction channel weights saturate at one, keeping their attraction toward the
  midpoint a convex blend instead of extrapolating past it during a long run of updates.

- Percentage Trailing Stops interprets `pct` as percentage points: the default 10 sets stops
  ten percent from breakout prices, rather than multiplying the price by ten.

- Volume Accumulation Percent retains negative selling pressure, using the symmetric -100 to 100
  range of volume-weighted close location instead of clipping all negative readings to zero.

- Overshoot Reduction Moving Average computes its regression slope directly, avoiding the rounding
  error from multiplying correlation by one deviation and dividing by another inside its feedback loop.
  Batch, builder, and streaming share its state arithmetic. Validation independently checks each
  recurrence step, since separate floating-point histories can diverge despite satisfying the same equation.

- Extended Recursive Bands normalizes periods below three to three. Its gain `2/(length+1)`
  must not exceed one-half: larger gains invert upper and lower bands after a price move.

- Edge Preserving Filter treats peak ratios within 1e-12 of one as the same peak, preventing
  floating-point noise on flat regression plateaus from spuriously restarting its running average.

- Turbo Scaler publishes its second blended-range position as Trigger. The PriceChange alias now
  publishes acceleration as Signal, consistently with Move Tracker.

- Published-output parity now discovers required-argument constructors and uses each case's configured
  batch parameters. This exposed incorrect secondary routes in Demand Oscillator, Double-Smoothed RSI,
  Dynamic Momentum Index, Kurtosis, McClellan, and Move Tracker; those outputs now follow their formulas.
- Double-Smoothed RSI removes the strength-ratio cap that limited mixed positive readings to 50.
  Its core now computes the same double-smoothed price-range legs as batch/streaming, with a minimum
  range window of two bars and default smoothing periods 5 and 25.
- Dynamic Momentum Index chooses its period from current deviation relative to smoothed deviation,
  using the integer part of the inverse relative volatility. Changing price units no longer changes
  the period. Integer boundaries use a relative 1e-12 rounding tolerance before truncation.
  An unavailable volatility baseline uses the base period; a zero current deviation with
  a positive baseline uses the maximum. Core startup and no-loss behavior follow batch/streaming.

- Fast and Slow Kurtosis Oscillator and its short-name alias now publish the configured signal average.
  Fast/Slow RSI and Fast/Slow Stochastic also publish their signal averages (fixed WMA periods 6 and 9).

- Fear and Greed now publishes its configured smoothed signal instead of repeating the main line.

- The two- and three-pole Ehlers Butterworth and Super Smoother variants use their requested period,
  normalized to at least two bars, without the unrelated angular/exponential coefficient clamps.
  Existing variant-specific angle constants and startup conventions remain. Two-pole Butterworth V2
  uses the symmetric [1, 2, 1] input kernel at lags 0, 1, 2; its former lag-three tap failed to reject
  alternating input at the Nyquist frequency. The corresponding component smoothers follow these changes.
  Declared warmup now allows five periods for two poles and eight for three. Shared validation checks
  a 1000-bar constant tail after declared warmup and enforces the corresponding fixture budget.

- Candle Bull/Bear Power (including their short-name aliases) and Gain/Loss Moving Average now publish
  their configured signal averages. Candle Power also honors its selected average type for the signal.

- Elder Market Thermometer, Ergodic Mean Deviation, and Ergodic True Strength Index V1 now publish
  the configured signal average instead of repeating the primary line.

- Elastic Volume Weighted Moving Average V1/V2 seed from the first price and retain their last value
  when the volume denominator is zero. V1's short core overload now uses the same volume-float formula
  and default multiplier of 20 as its batch implementation, including when selected as a component average.

- Enhanced Index and Ergodic Candlestick Oscillator now publish their configured signal averages
  instead of repeating their primary lines.

- Ehlers Spectrum Derived Filter Bank now uses two-bar bandpass input differences, quadrature scaling
  by period/(2*pi), and normalization against the completed spectrum. Its logarithmic scale uses
  base ten independently of the median period. Spectral periods and high-pass periods normalize to
  at least three bars; zero-power spectra return the minimum period. Existing seven-bar startup and
  median smoothing remain part of the contract. These corrections also change dependent indicators.
- Ehlers Restoring Pull now uses volume*(2*pi/cycle)^2 without an unrelated angular clamp.
  Its signal retains the configured average and minimum-period smoothing length.

- Ehlers Gaussian Filter now uses the requested cutoff period, with a minimum of two bars (Nyquist),
  instead of clamping its angular frequency to [0.01, 0.99]. Equivalent cascaded one-pole sections
  replace the high-order subtraction recurrence for better numerical stability. All four published
  pole-count outputs retain zero-state initialization.

- Ehlers Relative Vigor Index now publishes its configured signal average. Ehlers Instantaneous
  Trendline V2 now publishes its two-bar extrapolation as Signal. Both previously repeated the primary line.

- Ehlers Laguerre RSI computes its lag states relative to the initial price. The common price level
  cancels from the formula; removing it before filtering prevents roundoff from creating false
  strength on constant inputs. Streaming also clamps gamma to the batch/core range [0, 1].

- Fisher Transform maps a zero-width channel to its neutral midpoint instead of its bottom.
  A flat series now stays at zero; reflecting prices reverses the transform's sign. Its one-period
  batch window now agrees with the core and streaming paths.

- FRAMA follows the equal-half windows and startup in [Ehlers' paper](https://www.mesasoftware.com/papers/FRAMA.pdf).
  Periods normalize upward to an even number of at least two; the first full period returns the selected
  price, and zero-range windows retain the last valid dimension. The library continues to use Close
  as its default price input. Batch, scalar core, and streaming results change where they previously differed.

- Ehlers Zero Lag EMA now uses floor((period - 1)/2) as its correction delay without the unrelated
  [2, 530] clamp. The scalar core now smooths the same lag-corrected input instead of using a separate
  adaptive error-correction formula. Short- and very-long-period outputs change.

- Ehlers Spearman Rank now returns the price/time rank correlation in [-1, 1], using average ranks
  for ties. Its incomplete sort and extra affine rescaling previously produced incorrect readings.
  Existing zero padding during warmup is retained; constant windows and period one return zero.

- Ehlers Simple Deriv and Simple Clip now publish their configured moving averages as signals instead
  of repeating the raw four-bar sums.

- Ehlers Hamming, Hann, Triangle, and Simple Window indicators now publish their scaled filter
  differences as `Roc` instead of repeating the filter. The Simple Window derivative uses three
  smoothing passes as specified by its batch formula.
- A one-period Ehlers Hamming average now returns its input rather than NaN from a zero phase denominator.

- Double Smoothed Stochastic and Double Stochastic Oscillator now publish their smoothed signal
  outputs instead of repeating their oscillator lines.

- DecisionPoint Price Momentum Oscillator now publishes its ten-period EMA signal and line-minus-signal
  histogram instead of repeating the oscillator in both secondary outputs.

- Double Smoothed Momenta's signal now returns its configured moving average instead of repeating
  the unsmoothed oscillator.

- Adaptive Least Squares now fits a weighted regression and returns its current-bar endpoint.
  The former expression canceled its slope and returned only a smoothed price; the scalar core
  used a different fixed-window regression. All paths now retain the true-range-based adaptive
  gain, seed from the first price, and update centered weighted moments. Historical outputs change.

- Apirine Slow RSI retains small price-minus-average residuals with a stable recurrence when using
  Wilder smoothing. Ratios after a price shock no longer depend on cancellation in the rounded average.

- Adaptive Ergodic Candlestick Oscillator's signal now uses its configured smoothing length and
  average type instead of repeating the oscillator.

- Accumulative Swing Index's signal now returns its configured moving average instead of repeating
  the cumulative swing index.

- Relative Volume Indicator's demand-price line now holds the price preceding the latest volume
  z-score of at least two, instead of repeating the z-score itself.

- Psychological Line no longer counts the first positive close as an up bar against an invented zero
  predecessor. Flat series start at zero regardless of their price level.

- Statistical Volatility's signal now returns its configured moving average instead of the unsmoothed line.

- Anchored Momentum's signal now returns the rolling mean of momentum, including its partial-window
  initialization, instead of repeating momentum.

- Price Volume Rank's fast and slow signal outputs now return their configured moving averages
  instead of repeating the raw quadrant rank.

- Chande Intraday Momentum Index sums each candle's gain or loss once. Consecutive candles no longer
  accumulate earlier gains/losses repeatedly; mixed-direction window values change.

- Henderson Weighted Moving Average now uses the same clamped kernel half-width in the core, batch,
  and streaming paths. Core results change for short periods and half-widths above 530.

- Empty inputs now return empty results in the affected oscillator, volatility, and support/resistance
  paths instead of indexing a nonexistent first bar.
- Williams fractals normalize the center lag to at least two bars. Shorter requested lags previously
  read future bars or threw; batch, streaming, and builder now use the same minimum.
- Ehlers MESA Predict V2 treats unavailable autoregression history as zero for short `length1` values.
  Streaming also caps the forecast horizon to `length1`, matching batch and builder.
- Ehlers Variable Index Dynamic Average starts from the first observed price. It no longer stays at
  an artificial zero when a constant market gives its adaptation ratio zero dispersion. Warmup values change.
- The nine Ehlers Chebyshev waves derive their input gain from their existing recurrence coefficients,
  removing the permanent constant-price bias caused by rounded gain literals. Historical readings change
  slightly (the old constant-input gains ranged from approximately 0.999976 to 1.000017).
- Standard Deviation Volatility now reaches its existing batch calculation and streaming state through
  the typed V2 API. Its missing fast arm is no longer incorrectly marked verified.

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

- All v1.x `Calculate*` methods are still available (they will be deprecated in v3.0).
- `AlpacaBroker` and `AlpacaMarketDataProvider` have moved out of the
  `OoplesFinance.StockIndicators` package into a new `OoplesFinance.StockIndicators.Trading` package.
  They are the only two types that used the Alpaca SDK. `AutoTradingCatalog.Alpaca(...)` and
  `AlpacaTradeAdapter` stay in the core package - they are named for Alpaca but use no part of the
  SDK, so they need no new package reference.
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

#### Moving to the Trading package

`AlpacaBroker` and `AlpacaMarketDataProvider` now live in their own repository and package,
[OoplesFinance.StockIndicators.Trading](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators.Trading).
Their namespaces are unchanged, so no `using` needs editing - but the types are no longer in the
core package, so a project that uses them needs the new package reference:

```xml
<PackageReference Include="OoplesFinance.StockIndicators" Version="..." />
<PackageReference Include="OoplesFinance.StockIndicators.Trading" Version="..." />
```

The two packages version independently. Trading takes a minimum core version rather than a pin, so
you choose which core version you resolve provided it is at least that floor.

Nothing else needs to change: same types, same members, same namespaces.

**Why:** `Alpaca.Markets` is a broker SDK, and only those two adapters needed it. Carrying it in the
core package meant a project that only wanted to compute an RSI also restored a trading API client
and its transitive dependencies - the nuspec of the published `OoplesFinance.StockIndicators` 1.1.0
package, the last release made before this change, declares `Alpaca.Markets` and
`Alpaca.Markets.Extensions` as hard dependencies for exactly that reason. Type forwarding would have
made the move invisible, but the Trading assembly references the core one, so a forwarder in the
core assembly would be a reference cycle. The move is therefore breaking, and is marked as such
rather than hidden.

### Changed

- `IndicatorBuffer<T>` is now the primary container for indicator values (replaces raw lists)
- Results are accessed via `runtime.GetSeries(handle)` instead of `OutputValues*` properties
- **A typed Builder spec computes its batch indicator.** Each spec ran a fast arm written apart from the
  indicator it names and never compared with it; over half of the comparable arms disagreed. The Builder now
  serves an arm only where `BuilderArmTests` shows it matches, and computes every other spec with its batch
  indicator, so a spec's values are the indicator's values.
- **Every typed spec now names an indicator this library has.** 57 specs computed something no indicator
  computed - the highest high, a rolling variance, Yang-Zhang volatility, a zig zag - and were pinned in
  `BuilderArmTests.AwaitingPromotion` while they waited. All 57 are bound now and that list is empty. Most
  became new indicators, written from their published definitions and held to their arms; a few turned out
  to be indicators the library already had under another name, and bind to those instead: the median moving
  average is the median value, the ATR percent is the normalized average true range, and both average day
  range specs name the one indicator. Each new indicator has a streaming twin held to it bar by bar, with
  one exception below.
- **`ZigZag` has no streaming twin, and cannot have one.** A turning point is only known once the price has
  moved far enough past it, and recognising it rewrites the bars back to the previous turning point. A
  streaming engine has already published those bars. `CalculateZigZag` computes it, and the Builder serves
  it from there; there is no `ZigZagState`.
- **Eight more spec options are `[Obsolete]`** as having no effect, for the same reason as the 66 before
  them - their indicator has no parameter to set: the true range, the range, net volume, the cumulative
  volume index, the demand index and the Ichimoku lagging span read one bar or two and take no length; the
  Keltner channel width always averages exponentially; and the zig zag's option was being passed as a
  deviation percentage rather than a bar count.
- **A spec's options reach that indicator.** 177 options reached no parameter at all. 111 now set the parameter
  they name - the alligator and ichimoku lines, didi, tsi, the fast and slow pairs, the multipliers and band
  widths, the stochastic's %K and %D, the cyber cycle and laguerre alphas - through 118 mappings, since seven
  options set more than one parameter, as their own arms do: the decycler and osc oscillators' length sets both
  a fast and a slow length, and the mass index's two set three. The remaining 66 that their indicator has no
  parameter for are marked `[Obsolete]` as having no effect. `EhlersRoofingFilterSpecOptions` defaults to
  Ehlers' 48-bar high pass and 10-bar smoother, and `DoubleSmoothedMomentaSpecOptions` to the batch
  indicator's 2, 5 and 25, instead of settings that described another formula.
- **Three indicators gained a parameter their spec sets**, each part of the published definition and each
  defaulting to today's behaviour: the stochastic RSI's own stochastic lookback, Inertia's RVI smoothing
  length, and the Gaussian filter's pole count.
- **A typed spec streams what it computes.** The Builder built streaming states from a second table that had
  never been compared with the batch path: 30 specs streamed another indicator, another parameter, or ignored
  the moving-average type the batch honoured. `BuilderStreamingArmTests` now holds all 168 to their batch
  indicator. `AverageTrueRangeState`, `AverageDirectionalIndexState`, `RelativeStrengthIndexState`,
  `TrixState`, `AwesomeOscillatorState` and `AcceleratorOscillatorState` take a `maType`, defaulting to the
  average each hard-coded, so no existing value changes.
- **`ComparePriceMomentumOscillatorSpecOptions` is obsolete.** It compares a stock with a market series, which
  one series cannot supply; use `IndicatorCatalog.ComparePriceMomentumOscillator`, which passes both.

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
High - Low now, not the whole high. Half Trend, the Volatility Ratio and the ATR Filtered Exponential
Moving Average measure it against the bar's own close too, as their batch twins do. Only the last of the
three publishes different values: the other two discard that first range before it reaches a result - the
Volatility Ratio because its window bounds are still zero and their difference gates the ratio, Half Trend
because its average true range reaches only the arrow levels, which feed a signal and nothing either
engine publishes - so those two had agreed with the batch by luck rather than by construction.

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
- Asking `GetMovingAverageList` for the dynamically adjustable, adaptive, Ehlers adaptive Laguerre or middle
  high-low average without a fast length now uses the indicator's own default instead of 0.

**The Accumulative Swing Index is Wilder's.** Both engines ran his numerator backwards (the previous close
less today's) and took K and R from signed moves where he takes their sizes; they now compute
`50 * ((C - Cy) + 0.5 * (C - O) + 0.25 * (Cy - Oy)) / R * K / T` with his three-case R, and the first bar,
which has no previous bar, contributes 0. `CalculateAccumulativeSwingIndex` and `AccumulativeSwingIndexState`
take Wilder's limit move T as `limitMove`; the default of 0 keeps each bar's range in its place, as before.
ASI values and its signal change.

**Four Builder arms computed something other than the indicator they name.** Each served a typed spec
directly, so its values reached callers even while the spec named no indicator of its own:

- **Yang-Zhang volatility** weighed the open-to-close variance by `0.34 / (1 + (n + 1) / (n - 1))`. The
  published weight has 1.34 in that denominator rather than 1, which at a length of 20 makes it 0.1390
  instead of 0.1615 and moves the published volatility by about one per cent on every bar. A length of one
  also divided by zero, and is clamped to two: both variances are taken about a mean drawn from the same
  window, so a single bar has no reading.
- **The simple price zone** took a change out of its running sums one bar early, subtracting a raw price
  that had never been added to them. From bar `length` onwards both sums were wrong for good, and the zone
  left the range it is defined on, reading as high as 120 where it cannot pass 100.
- **The standard error** measured every residual against the fitted line's endpoint rather than against the
  line at each position in the window, so a window lying exactly on a sloped line reported scatter where
  there is none.
- **The geometric mean moving average** multiplied its window rather than summing logarithms, so a long
  window overflowed a double and published infinity: at a price of 1000 that happens by a length of 103.

Their batch and streaming twins are new here and never published the wrong values; these are the arms only.

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

### Formula-contract alias corrections

`MacdLine` and `PercentagePriceOscillator` now return their signal and histogram
series when those outputs are requested. Previously those slots repeated the main
oscillator line. `MacdLine` uses the batch contract's fixed nine-bar signal period;
`PercentagePriceOscillator` honors its configured signal period and average type.

### Formula-contract corrections across shared calculations

- Standard CCI now measures all window deviations from the current window mean. It
  returns zero until that window fills and on a constant window, including after a spike.
  Non-SMA variants retain their existing smoothed-residual definition.
- EMA uses alpha = 2/(period+1) without clamping. Period 1 follows its input exactly;
  periods above 199 now retain their requested smoothing. Batch and streaming agree.
- WMA clears cancellation residue when its window becomes entirely zero, preventing
  false nonzero volatility downstream. Streaming previews do not commit this state.
- RSI, CMO, momentum, ADL, standard deviation, Elliott Wave, and their affected aliases
  now publish the requested signal/histogram instead of repeating the primary line.
- Williams %R returns the batch convention of -100 for a zero range. PricePosition
  uses the stochastic convention of zero. Parabolic core weights are squared distances,
  consistent with the published parabolic weighted average.
- ADX and its dependent indicators start with zero directional movement because the
  first bar has no preceding high or low.
- Ease of Movement uses the change in the high/low midpoint, multiplied by range
  and the volume divisor and divided by volume. The first bar and zero volume return zero.
- Garman–Klass now returns the square root of mean window variance, annualized by
  sqrt(252), after a full window. The previous history-index factor made stationary
  volatility grow with the total input length. Its signal output now publishes the
  configured seven-bar average instead of repeating the volatility series.
- Balance of Power and Mass Index now publish their signal averages in the signal slots.
- Population variance and standard deviation center their mean calculation on a window
  value. Constant windows now have exact zero dispersion, preventing false saturated
  inverse Fisher readings after spikes; small real variations are retained.
- Vortex starts with zero cross-bar movement. Ultimate Oscillator uses the first
  bar's close as its initial reference for buying pressure and true range. Adding
  a constant to all OHLC prices no longer changes either oscillator during startup.
  Technical Ratings uses the same corrected Ultimate Oscillator initialization.
- Relative Vigor Index uses high minus low at every denominator lag; previous bars
  formerly used high minus open. RVI and TSI signal slots now return their signal lines.
- Camarilla publishes zero levels until a prior session exists, removing first-session
  look-ahead. Its first two offsets use exactly 1.1/12 and 1.1/6 of the prior range.
  The `PivotPoint` alias now follows daily Floor Pivot aggregation rather than prior-bar OHLC.
- Stochastic RSI ranges exactly the requested number of RSI observations; it no longer
  includes an extra observation through an intermediate two-bar range. Both aliases
  publish the second smoothing stage in their signal output.
- EMA and Wilder RSI preserve their ratio on unchanged prices for periods above one,
  where gains and losses decay together. This prevents floating-point noise from
  creating false stochastic extrema while retaining sensitivity to actual price moves.
- Relative Momentum Index now publishes its smoothed signal and line-minus-signal
  histogram when those outputs are requested, instead of repeating its main line.
- Stochastic Fast Oscillator and Stochastic Regular now publish their respective
  second and first smoothing stages in the signal slots instead of repeating the main line.
- Gopalakrishnan Range Index normalizes periods below two to two because logarithm
  base one is undefined. Its signal slot now publishes the configured moving average.
- TFS MBO, TFS MBO PPO, and Ergodic PPO now publish their signal and histogram outputs instead
  of returning the primary oscillator for all three slots.
- PPO Leader now honors its signal period in the signal and histogram outputs.
- DiNapoli PPO now returns its zero-seeded, fractional-period signal and histogram
  when requested, rather than repeating the oscillator.
- Trend Detection Index now publishes its signed momentum sum in `TdiDirection`;
  that slot previously repeated the trend-detection value.
- Elder Impulse is bearish only when both its EMA and MACD histogram fall strictly.
  Flat values now produce neutral readings instead of false bearish signals.
- Hampel filtering measures every absolute deviation from the current window median.
  The previous rolling history mixed deviations from different medians and could reject
  valid observations. Its threshold remains `scalingFactor * MAD`, followed by the existing
  zero-seeded exponential smoothing.

ATR Filtered EMA now computes the SMA variance of normalized true range with centered deviations. Constant range windows correctly restore the smoothing gain instead of letting cancellation in raw second moments freeze the average. Batch, builder, and streaming paths share this correction; caller-supplied component averages retain their definitions.

Bilateral Stochastic output selection now returns its requested Bull, Bear, or Signal series; Signal applies the specified average over 20 bars to the stronger direction.

CCT Stoch RSI builder outputs now honor Type2 through Type6, TypeCustom, and Signal, using each output's RSI period, extrema windows, and smoothing instead of returning Type1 for every selection.

Chande Momentum Oscillator Filter now publishes its actual Signal average with the selected component-average type.

Chande Composite Momentum Index Signal now returns the trailing five-bar mean of its volatility-weighted momentum, instead of the primary exponential average.

Conditional Accumulator Signal now returns the selected moving average of the cumulative gap count, honoring its period and average type.

Constance Brown Composite Index now adds RSI momentum (current RSI minus RSI at the momentum lookback) to the smoothed short RSI. Previously it added only the delayed RSI level. Momentum is zero until its lookback exists. Both builder signal outputs now return their requested averages.

Corrected Moving Average now solves its nonnegative smoothing-gain fixed point exactly. The previous tolerance-limited iteration left residual movement where the exact gain is zero and biased nonzero gains; the closed form also removes up to 5,001 iterations per bar.

DeMarker now starts with zero high/low movement because the first bar has no predecessor. A positively priced flat series no longer invents an initial upward extension.

Delta Moving Average now returns the selected Signal or Histogram series with the configured smoothing period and average type, instead of its unsmoothed Delta for every output.

Forecast Oscillator Signal now uses the configured average and period instead of returning the primary line.

Folded RSI Signal now returns its configured moving average instead of the unsmoothed folded-RSI sum.

Autonomous Recursive MA now gates delayed input by its momentum lag, fixing invalid indexing at short smoothing periods and premature zero substitution at long ones. Its component-average core now computes the same autonomous threshold and twice-smoothed output as the indicator, replacing an unrelated adaptive EMA.

Bayesian Oscillator now normalizes binary evidence as p*q / (p*q + (1-p)*(1-q)). Previously the complementary term was added outside the denominator, producing values above 1. All three probability outputs now stay in [0,1]; contradictory certain evidence returns zero when there is no mass to normalize.

- Generated output names now preserve all published series when punctuation or constructor parameters collide. Ehlers Chebyshev Low Pass exposes all nine filter variants; Variable Length Moving Average exposes its adaptive period as `LengthOutput` alongside its primary average. Negative numeric output names use `Minus` where necessary to distinguish them.

- The Ehlers High Pass Filter V2 span core now matches the published two-pole filter, four-bar zero startup, and two weighted smoothing passes (default period 20). Previously it computed an unrelated one-pole filter with default period 48.

- Damped Sine Wave Weighted Filter now uses the full sine cycle in its normalized kernel. The former angular clamp removed its negative lobe. Periods below three now use three, the smallest nondegenerate sampled full-cycle kernel.

- Connors RSI now ranks each return strictly against the preceding full lookback, excluding ties and the current observation, and starts its streak at zero. Its span implementations now agree on warmup and use the fixed rank denominator. Stochastic Connors RSI now routes `Signal` to its second smoothing pass.

- Stochastic Connors RSI now ranges Connors RSI observations directly. It no longer borrows price candles or expands the lookback through synthetic two-bar candles, so changing OHLC ranges while preserving closes cannot change the oscillator.

- DT Oscillator now computes a stochastic of RSI, rather than of smoothed prices, with both published smoothing outputs. Its span core uses the same partial-window arithmetic startup. DiNapoli Preferred Stochastic and Decision Point Breadth Swenlin now return their separate signal smoothing when requested.

- Know Sure Thing (including Kst), Internal Bar Strength, and Percent Change Oscillator now return their own signal smoothing when requested. Formula contracts cover every output of these indicators, Pring Special K, and the Performance Index aliases.

- Internal Bar Strength uses the existing zero-range fallback of zero in every engine. Volume Accumulation Oscillator contributes zero at the candle midpoint; its span core now matches the published moving average of volume times midpoint displacement. Modified/Reflex/Multi-Vote OBV, Modified Price Volume Trend, and Smoothed Williams Accumulation Distribution now route their separate signal outputs correctly.

- Market Meanness Index preserves fractional percentages instead of truncating them to integers, and exposes its configured smoothed output. Vertical Horizontal Filter and Vhf now route their signal smoothing correctly; period one uses a one-observation range. Independent references also cover both Shinohara Intensity Ratios.

- Williams Fractals requires actual left-side observations and both right-side confirming bars for every plateau pattern. The longest plateau now reads the correct sixth older bar at startup. Peak and trough tests share mirrored conditions across batch, builder, core, and streaming engines. Existing delayed publication for periods above two is preserved.

- All Triple Hull / 3HMA aliases now use the same ceiling-derived periods, with minimum one and no arbitrary upper clamp. They previously disagreed at short periods because some paths clamped intermediate periods to [2, 530].

- Repulse, Midpoint Oscillator, Rex Oscillator, Rapid RSI, Really Simple Indicator, and Sigma Spikes now return their configured signal series. Really Simple Indicator also compares its raw value with its signal when generating legacy signals.

- Ehlers AM Detector now returns its second envelope smoothing for `Signal`. Independent formula contracts also cover its amplitude output, Dynamically Adjustable Filter, Dynamically Adjustable Moving Average, the library's damped Double Exponential Smoothing, and DeMark Pressure Ratio V2.

- Enhanced Williams %R now routes its configured signal smoothing; Reverse MACD routes its signal and histogram separately. Formula contracts also cover Normalized MACD, Smoothed Williams %R, and nine additional momentum-window formulas and their aliases.

- Ehlers High Pass V1 and Decycler now derive their coefficient from the requested cutoff without the arbitrary `[0.01, 0.99]` angle clamp. A half-angle identity handles the removable quarter-cycle singularity and long periods. Cutoffs above Nyquist use the limiting response: zero for the high-pass filter and identity for the low-pass decycler. Derived Simple Decycler bands and Decycler Oscillator V1 inherit the corrected filter.

- Polarized Fractal Efficiency and Pfe now use the requested period for the horizontal distance and Euclidean distance for every path segment. The raw ratio is zero during warmup and on zero net movement, reaches ±100 on straight rising/falling paths, and stays within ±100 by the triangle inequality. The core previously omitted horizontal distance from path segments; the other engines hard-coded a 10-bar direct distance. References also cover relative-volatility variants, Inertia, Stiffness, and Surface Roughness.

- JRC Fractal Dimension counts the opening range once in its rolling sum. The extra copy previously remained forever, biasing later values after that bar left the window. Its one-bar short range and separate signal output now agree across the engines.

- Total Power Indicator now returns its separate bull and bear counts, and Trend Exhaustion returns its configured signal smoothing. Independent references cover these outputs, Trend Persistence Rate, and Trend Continuation Factor's exposed output.

- Stochastic Momentum Index and SMI Ergodic now return their configured signal smoothing. Independent references cover those outputs, Premier Stochastic, and the existing single-pass Schaff Trend Cycle variant and aliases. Additional channel references cover Narrow Sideways, Uni Channel, Moving Average Support/Resistance, Headley Acceleration, Smoothed Volatility, Trend Trader, and Support/Resistance.

- Stochastic Momentum Index's one-period batch range now uses the current candle, matching builder and streaming. Schaff Trend Cycle treats a MACD range below 64 machine epsilons of its contributing averages as numerically flat, returning zero instead of amplifying subtraction noise into a full-scale oscillator. A one-period cycle uses a one-observation range in every published route.

- Impulse PPO now publishes its partial-window signal average and histogram; MAC-Z publishes its configured signal and histogram. Independent formulas cover those outputs, all six Linda Raschke series, and MAC-Z VWAP, whose Laguerre filter is checked using independently expanded observation weights.

- Windowed Volume Weighted MA now applies its periodic Bartlett taper by lag within the trailing window. Weights no longer depend on absolute history position or become negative as history grows. The core, builder, and streaming paths use the same taper; period one is volume-weighted identity and zero weight mass returns zero.

- Wave Trend now routes its Signal output to the final four-period exponential smoothing. Independent references also cover WAMI, Volume Weighted RSI, Woodie's exposed fast CCI, and alternate-average CCI configurations.

- Wave Trend's EMA residual now uses price increments rather than subtracting nearly equal price levels. This preserves its geometric decay on flat tails and prevents rounding noise from changing the normalized oscillator. Its arithmetic-mean startup remains the same.

- Moving Average Bands, Trend Continuation Factor, and Woodie CCI now expose all published series through their typed indicators. Their Value members retain UpperBand, TcfPlus, and FastCci respectively. All newly exposed series have independent formula contracts; Woodie's builder now computes SlowCci and Histogram separately.

- Adaptive Price Zone, Narrow Sideways Channel, Trend Trader Bands, Moving Average Envelope, and Uni Channel expose both outer bands and retain their middle Value. Uni Channel respects chained inputs and computes asymmetric percentage or absolute offsets for each band.

- Keltner Channels, Moving Average Support/Resistance, Headley Acceleration Bands, Smoothed Volatility Bands, and Support/Resistance expose their remaining bands or resistance series, with dedicated builder calculations and independent references. Existing Value selections are preserved.

- Moving Average Channel, Fibonacci Bollinger Bands, and Average True Range Channel expose all their published outputs. The ATR channel includes its separate Sma series as well as its rounded bands, with hand-calculated examples covering every output.

- Standard Deviation Channel, STARC, High/Low Bands, and Kirshenbaum Bands expose both outer bands while preserving their middle Value. Kirshenbaum's width has an independent rolling regression-error reference, and exact small examples cover the newly exposed values.

- Bayesian Oscillator, Chartmill Value Indicator, Buff Average, and DEMA 2 Lines expose their remaining probability, candle, and slow-average series. Independent contracts cover all outputs and retain the existing Value selection.

- Daily Average Price Delta, Fibonacci Retrace, and Dynamic Support/Resistance expose every published level. The latter two now use exactly one candle for a one-period range in the batch API, matching builder and streaming rather than silently expanding to two candles.

Efficient Price and Efficient Auto Line now have typed indicators and automatic independent formula validation. Trend Analysis Index honors a one-bar range; its result is zero when `length2` is one. Sell Gravitation, Trend Analysis, and Mass Thrust builder signal outputs now select their smoothed series. Expanded multi-output indicators retain the same default primary in both typed and legacy builder bindings. Vortex Indicator Plus/Minus no longer invent movement before the first bar.

Varadi percentile ranks treat smoothed ratios within `1e-12 * max(1, abs(currentRatio))` above the current ratio as ties. This prevents smoothing roundoff from turning a flat window into a lower percentile after a spike. The tolerance applies to rank comparisons, not to validation of the resulting percentage.

Martin Ratio now uses correctly scaled percentage excess returns and the Ulcer Index of prices. Sortino Ratio measures downside below the compounded benchmark, rather than below the moving mean. These correct formula errors and change historical results. Both retain the existing period-return convention, 360-bar benchmark year, chosen smoothing, and zero result for zero risk. Ulcer Index now honors a one-bar window.

Didi Index exposes Curta, Media, and Longa. Media is the normalized reference line (one when the medium average is nonzero), and Longa is the long average divided by the medium average; previous batch/streaming publication incorrectly returned unnormalized averages. All three lines are now invariant under positive price scaling. The default Value remains Curta.

Momenta RSI no longer caps the up/down strength ratio at one: mixed trends with stronger upward movement can correctly exceed 50. It also honors a one-bar range. A typed Momenta RSI API and independent references cover both its line and signal; Self-Adjusting RSI contracts cover all four outputs.

Range Bands now honors a one-bar range window: its upper and lower bands equal the moving average at period one, instead of retaining a two-bar spread.

Mean Absolute Deviation Bands now uses the rolling mean absolute deviation of prices about their arithmetic mean instead of standard deviation. Its selectable moving average still sets the band center. Mean Absolute Error Bands and Time Series Forecast now divide cumulative absolute error by the number of included observations, including the current bar.

Choppiness Index and McNicholl Moving Average now normalize periods below two to two across typed options, batch, builder/core and streaming paths. Their defining denominators (`log(length)` and `1 - 2/(length+1)`) are singular at one; the former produced nonfinite values and the latter returned zero even for constant prices.

Automatic validation now includes all-minimum-period configurations. Corrections exposed by those cases:
- One-period SMA and rolling volume-weighted means return the input exactly (or zero VWAP for zero volume), preventing numerical residuals from triggering comparisons and z-scores.
- Hull, Slow Smoothed, Zero Low Lag, and Trend Direction Force derived windows now allow one observation. Adaptive Stochastic and Statistical Volatility honor one-observation ranges.
- Bryant's effective smoothing length is bounded below by one, preventing an unstable gain greater than one.
- Falling/Rising Filter, Shapeshifting MA, Narrow Bandpass Filter, and Phase Change Index normalize their period to at least two because their defining recurrences or weights are singular or nonsettling at one.
- Parametric Kalman starts from the first observation, preventing a permanent zero estimate on constant data at period one.
- Reverse MACD carries the previous common average when fast and slow periods are equal, instead of returning zero for all prices.
- Ehlers Kaufman declares at least 4,500 warmup bars: its minimum gain is 0.0645 squared, so short periods do not imply fast convergence from its zero seed.

Explicit average stages now retain built-in and customer averages in declaration order on finite sources. Additional stages require preceding stages to be supplied; omitted slots throw instead of shifting the remaining averages. Live execution currently rejects separately configured stages because its state factory cannot represent them, rather than silently using the primary average for every stage.

G Channels now normalizes periods below two to two. Its half-width contracts by `1 - 2 / length`; period one made that factor negative and inverted the bands even for a constant price.

### Trend force and explosion width corrections

`TrendForceHistogram` now centers its event count on the mean of all observations seen, dividing by the actual sample count. Its first value is zero. `WaddahAttarExplosion` computes its explosion width directly as four population standard deviations; its streaming path uses centered variance, avoiding cancellation when prices are large relative to their spread.

The native `TrendForceHistogram` and `WellesWilderVolatilitySystem` states now honor the batch API's minimum two-bar window for single-price extrema, including a requested period of one. ATR and moving-average periods remain independent of that extrema minimum.

`Vpci.Signal` now uses its configured smoothing period instead of repeating the raw VPCI output. `VolumeAdaptiveBands` exposes both outer bands alongside its existing middle-band primary output.

`VolumeFlowIndicator` now ignores price changes within its volatility cutoff. Previously a fallback counted every nonzero move, making the cutoff coefficient ineffective. `TradeVolumeIndex.Signal` now publishes the configured smoothed line instead of repeating the raw index.

`OnBalanceVolumeDisparityIndicator` and `NegativeVolumeDisparityIndicator` now measure price and volume-index positions in the same Bollinger-band coordinates. The price denominator is the band width (`4 * standard deviation`), correcting the former current-price-dependent upper boundary. Proportional price and volume-index series now give disparity one. The negative-volume disparity builder also publishes its separately smoothed signal.

### Klinger whole-bar trend and output corrections

`KlingerVolumeOscillator` derives trend from changes in high + low + selected price, rather than changes in the selected price alone. Unchanged sums retain the previous trend; the first bar has no trend. It retains the absolute-force form `volume * abs(2 * range / cumulativeRange - 1) * trend * 100`, also used by [Tulip Indicators](https://github.com/TulipCharts/tulipindicators/blob/master/indicators/kvo.c), with zero force when cumulative range is zero. The low-level core now carries the accumulated range across each trend segment. Both `KlingerVolumeOscillator` and `Kvo` publish separate signal and histogram values instead of repeating their oscillator output.

`EhlersUniversalOscillator` compares the current magnitude against the decayed previous peak, keeping its normalized line within [-1, 1]. Its signal and `EhlersTripleDelayLineDetrender`'s second smoothing are now routed as separate outputs. `EhlersSuperPassbandFilter`'s upper/lower outputs now return positive/negative rolling RMS rather than the filter line.

Klinger's EMA variant evaluates the difference of its legs directly, avoiding cancellation from subtracting large rounded volume averages near zero crossings. `EhlersTrendExtraction.Bp` now exposes the unsmoothed band-pass line. `EhlersUniversalTradingFilter` now respects the selected input series and returns rolling RMS for its outer bands.

Ehlers phase calculation now uses the full Fourier-vector quadrant, removes the DC component from complete windows, and uses a relative degeneracy threshold. Small-amplitude signals therefore retain their phase; a zero harmonic returns 90 degrees. Periods below two resolve to two across execution routes, and `Signal` returns the configured moving average of phase.

Rainbow Oscillator now publishes its positive and negative rainbow-width bands through the builder. Firefly Oscillator now publishes its rolling maximum as `Signal`. These secondary outputs previously repeated the primary oscillator.

Ehlers correlation cycle now correlates with a full sine/cosine cycle instead of clamping its angles below one radian, and exposes both `Real` and `Imag`. Correlation trend uses the same ascending-price sign in the core as in other routes. Centered price differences preserve these correlations at large price offsets. Correlation angle resolves the Fourier axes with `atan2`; a zero vector returns 90 degrees, including streaming (previously 900), while retaining the existing monotonic phase hold and wrap convention.

Stochastic Custom, Stochastic MACD, and Turbo Stochastics now publish their distinct signal/histogram formulas in the builder. Turbo options preserve zero and negative adjustments (clamped to the regression window) instead of forcing every adjustment positive. Slow Turbo now produces one trading signal per bar; its previous loop iterated an empty list.

Trader Pressure Index now publishes its separately averaged `Bulls` and `Bears` components. Strength of Movement defines zero-lag fractional movement as zero, preventing division by zero at period one, and its native range window now matches the batch minimum of two observations.

Value Chart, Wilson Relative Price Channel, and Time and Money Channel expose all their published coordinates/bands while preserving their existing primary values. Time and Money uses its documented minimum two-bar half-period lag consistently in the builder, and its six channel outputs now use the price basis and the signed volatility multiplier.

Ultimate Trader normalizes true range and volume over their own lookback windows. Price-bar containment no longer changes either statistic, and there is no extra two-bar envelope. The normalized score is `100 * (bulls - bears) / (bulls + bears)`, with zero for no contribution: purely bullish input reaches +100 instead of -100. Its builder `Signal` now applies the final smoothing stage.

High Low Moving Average and Hurst Cycle Channel expose all their bands, and Hurst Bands has a typed indicator exposing all seven outputs. Hurst Bands includes real zero prices in its displaced mean instead of extrapolating as though the observation were missing. Time and Money uses centered population variance for its simple-average mode so a departed spike does not leave a spurious channel width.

Vortex Bands and Hirashima Sugita RS expose every published band. Vortex keeps its upper band as its primary output and clamps its smoothed absolute-deviation width at zero: an overshooting moving average can otherwise invert the bands even though every input deviation is nonnegative.

Ehlers Autocorrelation and its Periodogram have typed registrations and independent formula contracts. Autocorrelation uses centered covariance across execution routes, preventing values outside its normalized 0–1 range and eliminating spurious correlations/cycles from one-point windows.

Roofing Filter V2 applies its existing DC and Nyquist zeros before its recursive poles. This equivalent transfer function preserves the published gain and coefficient clamps while avoiding a numerical noise floor when alternating input cancels. The independent reference expands the repeated-pole impulse weights and factors the low-pass denominator into complex conjugates.

Adaptive Ehlers V2 signal outputs now apply their configured moving average instead of returning the primary line. Adaptive CCI retains its historical RMS-of-adaptive-residuals definition; its variable windows no longer subtract lifetime prefix sums, preventing spurious nonzero one-bar residuals and cancellation after large observations leave. The adaptive RSI Fisher transform consumes its normalized 0–1 RSI directly; the erroneous extra division by 100 is removed.

DeMark Pressure Ratio V1 now counts gap-down selling with the same negative sign as ordinary selling. Mixed buying and gap-down selling therefore contributes to total pressure rather than cancelling it. The independent SpearmanIndicator contract preserves its historical correlation against sorted price midranks (including ties); EhlersSpearmanRankIndicator uses chronological ranks.

Standard and Dynamic Pivot Points now have typed indicators exposing every published level. Their native streaming states aggregate completed days to match the daily batch contract; levels stay fixed during a day and previews do not finalize a session. Standard pivots retain the library OHLC/4 definition and coincident second/third levels.

Relative Spread Strength retains high/low precision pairs through its moving averages, their spread, and the spread change before RSI. This prevents subtraction rounding from dominating a nearly constant spread. Simple Lines tracks its integer step count against the first price instead of accumulating fractional steps; its strict one-step threshold is preserved.

Kase Dev Stop V2 now offsets the selected price by its signed average-range/deviation distance. The previous misplaced parentheses multiplied price and distance, so stops changed quadratically when price units changed. All four stops and both Kase Indicator lines are exposed by their typed indicators.

Kase Dev Stop V1 now exposes all four outputs through a typed indicator. Multi-Level Indicator exposes its factor and computes the algebraically equivalent difference of opening prices directly, preventing cancellation against unrelated large closing prices.

Kase Dev Stop V1 now retains compensated fast/slow average sums through division, so equal trend means select the documented long stop rather than switching sides due to roundoff. Custom average components retain their supplied values.

Kase Peak Oscillator V1 now routes the `Pk` output separately from `Kpo` and exposes its smoothing period. Kase Peak Oscillator V2 retains compensated volatility-average sums so an expired spike cannot leave a tiny divisor and generate enormous readings. Phase Change Index now routes its smoothed `Signal` independently.

Ehlers Reverse EMA V2 now exposes both trend and cycle outputs. Batch, builder, and streaming compute each from the original input; the cycle is no longer a second filter applied to the trend output.

Early Onset Trend now uses the original Super Smoother in the builder core, matching the defined formula and other routes at short periods. Band Pass V1 and Stochastic Cyber Cycle now route their trigger outputs independently; the latter preserves the two-sample minimum stochastic window at period one.

Ehlers Modified RSI now divides rolling gains by total absolute movement over the same lookback. Previously it divided the gain sum by a single bar, so the input to its smoothing filter was not a normalized RSI ratio. Both `Emrsi` and `Signal` are exposed. Modified Stochastic streaming now preserves the batch two-sample minimum extrema window.

Roofing V1 now applies its constant-price and alternating-price cancellation before recursive filtering, retaining small transients used by normalized oscillators. Modified RSI computes window means from bounded window sums rather than subtracting historical totals, so decaying movements are not lost.

Even Better Sine Wave now cancels alternating prices before recursive filtering and scales its three-sample RMS calculation, preserving normalized readings for decaying tails and extreme price scales. The inverse Fisher RSI and CCI types expose all three custom moving-average stages.

- Ehlers Hurst Coefficient now has typed period options and shared formula validation. Recursive Median Oscillator exposes its three actual periods; its obsolete single `Length` option retains its previous no-op behavior. Empirical Mode Decomposition now exposes `Trend`, `Peak`, and `Valley` through the builder, including its three configurable averaging stages.

- Adaptive Cyber Cycle now exposes its cycle and measured period with shared formula checks. Autocorrelation Reversals clamps its starting lag to at least one across batch and streaming, matching typed options and preventing a read of a future bar. Its builder uses the same corrected roofing and centered-correlation kernels as the underlying autocorrelation indicator.

- Adaptive Laguerre ranks now treat deviations within 32 machine epsilons of the current price/filter scale as ties before normalization. This prevents near-equal deviations on a steady trend from changing the adaptive coefficient because of arithmetic roundoff.

- Periodic Channel exposes all eight series with configurable periods; Super Trend Filter and Prime Number Bands now have typed options and shared formula validation. Prime searches test each candidate against its own divisors, recognize 2 and 3, and exclude 1 and composite squares. Prime Number Bands uses the same minimum two-sample extrema window across batch and streaming.

### StandardDevation dispersion and signal

`StandardDevation.Std` now always measures rolling population standard deviation (divisor N, zero until the window fills). The selected moving average smooths `Signal`; it does not change the weights used for `Std`. Non-SMA results change because V1 mixed a weighted second moment with an unweighted mean, which was not a consistent variance. The calculation uses centered deviations to avoid subtracting large squared prices.

Pivot Point Average now uses full calendar periods consistently in batch, builder, and streaming. Weeks start on Monday; month/year boundaries and the hour containing each minute are respected. Signals advance once per period.

HalfTrend now retains direction and reversal thresholds across bars. Its previously unreachable transition branch kept it on a rising support line; it now confirms both downward and upward reversals. Initial support is the first rolling low, including negative prices.

Parabolic SAR now carries its trend, extreme point, and capped acceleration between bars, clamps against the preceding two bars, and resets acceleration on reversals. `Sar` publishes the current bar's stop, seeded long at the first bar's low. Previously the batch path restarted on every bar and published a next-step projection. V2's typed constructor now exposes `start`, `increment`, and `maximum`; the obsolete `length` remains inert.

Insync Index now compares Bollinger Percent B against 5 and 95 on its published percentage scale. The former 0.05/0.95 thresholds treated most ordinary band positions as overbought.

Insync Index treats component values within `1e-12 * max(1, abs(value), abs(boundary))` as tied when assigning discrete votes. This prevents rounding residuals in flat or settled components from creating five-point score changes; values outside that numerical resolution retain the strict crossing rules.

Confluence now supports period one consistently, evaluates zero-lag terms after their current values exist, and honors all nine custom average stages. Its projections cancel common terms algebraically, avoiding false nonzero spreads. Discrete votes and phase comparisons treat differences within `1e-12 * max(1, abs(value), abs(boundary))` as ties, so rounding noise cannot generate integer votes.

Ehlers Anticipate now matches a complete periodic sine basis instead of clamping phase angles to 0.01–0.99 radians. Its builder honors bandwidth and the configured impulse-response average. Periods one and two return zero because the sampled sine basis cannot resolve phase. Impulse-response windows whose range is at most 1e-12 of their largest absolute value have unresolved phase and return zero. Correlation scores within 1e-12 retain the first phase. These corrections change legacy predictions.

The default quadratic least-squares fit and its forecast now use centered orthogonal coordinates to avoid cancellation of global index moments. Values remain zero until the window is full. Periods one and two consistently use the window mean because a quadratic fit is underdetermined; they no longer depend on floating-point determinant residue.

Technical Rank now weights its long- and medium-term percentage ROC components by 0.30 and 0.15, without multiplying those already-percent outputs by 100 a second time. This prevents premature saturation of the final 0–100 rank. All nine component periods are exposed by its typed options.

ZigZag now redraws a leg when its provisional endpoint extends and holds the last endpoint through the tail, rather than leaving unpainted bars at the first seed value. It starts at the first candle midpoint and initially tracks highs; outside-bar reversals take precedence over endpoint extensions. Reversal distance uses the absolute pivot magnitude so signed prices have a nonnegative percentage threshold. The deviation percentage is now available in typed options. ZigZag remains a repainting batch indicator.

The SHK Schaff Trend Cycle exposes both stochastic-cycle and MACD outputs with typed component periods. Like the single-pass variant, it treats ranges below 64 machine epsilons of their source scale as numerically flat; its two stochastic passes retain their previous reading in such windows. This prevents normalization of cancellation noise into a full-scale signal.

Ehlers Convolution now uses centered Pearson correlation and a stable high-pass pole (its angle is capped at 0.99 radians for short periods). Previously short periods could produce an unstable filter and overflow. Its slope comparison treats differences within 1e-12 times the larger of one and the two compared magnitudes as tied, publishing +1 for ties. Both convolution and slope outputs are exposed in typed options.

Vervoort Smoothed Oscillator now publishes its band position in percentage points: 50 at the band center, rather than 0.005. The prior formula divided by 100 instead of multiplying. Band position retains extended precision through its filter cascade and normalization. Deviation at or below 64 machine epsilons of the window scale is unresolved and returns zero, as does a zero deviation multiplier. Its actual band, range, cascade, smoothing, and deviation parameters are now exposed in typed options; the obsolete inert Length is retained.

Kaufman Adaptive Correlation and Kaufman Adaptive Least Squares now share one price-derived KAMA gain across all regression moments. Previously each transformed series chose a different gain, yielding incompatible covariance and variances. The corrected default path uses centered moments, bounds correlation to [-1, 1] against rounding, and reproduces linear prices in the fitted endpoint. During the first Length bars, deviation and correlation are zero and the fit equals price. Custom component averages and non-KAMA paths retain their existing definitions.

Optimized Trend Tracker now trails both long and short stops and retains direction while the average stays inside the band. The first bar initializes the long direction from its own stops. Previously the short stop never trailed and the output fell to zero inside the band. Stop distance uses the absolute average magnitude, keeping stop ordering valid for signed prices.

Ehlers Sine Wave V1 and V2 now evaluate their Fourier basis across the full cycle. Previously basis angles were clamped to 0.01–0.99 radians, destroying the projection. Both sine and 45-degree lead outputs are now exposed through typed indicators.

Vervoort Modified Bollinger Bands now exposes all four outputs and preserves extended precision through the Heikin-Ashi smoothing cascade and percent-B normalization. A band deviation at or below 64 machine epsilons of the window scale is unresolved and produces zero percent-B, preventing numerical noise from creating spurious outer bands.

The sine-wave Fourier projections treat each component within 64 machine epsilons of the absolute input-window sum as zero, avoiding arbitrary phase flips from a flat history’s cancellation residue.

Self-adjusting Laguerre RSI defines its one-bar adaptive gain as 0.99 when its normalized range sum is positive, and 0.01 when it is zero. Previously log(1) caused undefined gains. The typed indicator now receives the shared discovery and independent formula checks.

Self-adjusting Laguerre RSI retains precision through its all-pass stages. Total stage variation within 64 machine epsilons of the stage scale is unresolved and returns zero, preventing normalized roundoff on settled histories.

Ehlers Homodyne Dominant Cycle now measures angular advance with atan2 rather than treating the tangent of an angle as the angle. Phase Accumulation Dominant Cycle handles axis-aligned phasors, counts the current sample when accumulating a full turn, and resolves 360-degree crossings within a relative 1e-9. Both, along with Dual Differentiator Dominant Cycle, now expose their periods through typed indicators and independent formula checks.

Ehlers Discrete Fourier Transform now computes a full-angle spectrum and selects its dominant cycle from the current period bins. Previously it treated historical maximum powers as period bins. It normalizes all bins against the same current peak, uses a three-decibel weighted centroid, and returns zero for an empty spectrum. Candidate periods are at least three bars to stay below Nyquist.

The DFT retains extended precision through cleanup and projection. A cleaned window no larger than 64 machine epsilons of its input scale is unresolved and returns zero. Dual Differentiator Dominant Cycle similarly treats its cross-product difference as unresolved within a relative 1e-12 of the two product magnitudes, instead of letting cancellation choose opposite period bounds.

Ultimate Momentum now exposes its five active periods and deviation multiplier. Its composite holds the previous value when the change is within 64 machine epsilons of their scale, so its RSI does not normalize arithmetic residue from an otherwise settled blend. The legacy length6 argument remains inert.

Ehlers Fourier Series Analysis now uses stable nonnegative damping poles, treats harmonics at or above Nyquist as unresolved zero, scales each quadrature difference by its own period divided by 2π, and scales the two-bar wave difference by the fundamental period divided by 4π. Previously misplaced multiplication and shared harmonic scaling distorted the derivative outputs, and short-period poles could be unstable. Both outputs and bandwidth are exposed in typed options.

Ultimate Momentum also retains gain/loss precision through RSI smoothing and detects exactly empty gain/loss windows, so removed historical changes cannot leave a spurious RSI denominator.

Its percent-B component also retains centered precision before the composite multiplies it by 200; otherwise a steady ramp could create tiny false reversals that become full-scale RSI changes.

Technical Ratings treats component comparisons within `1e-12 * max(1, abs(left), abs(right))` as ties before assigning votes. This prevents floating-point residuals on equal moving averages or saturated oscillators from changing ratings by an entire vote.

Guppy Count Back Line uses the most recent high/low extreme in the rolling `Length` bars (most recent equal extreme wins; a same-bar high/low tie selects the high). From a high it counts back two successively lower lows; from a low it counts back two successively higher highs, searching at most `Length` earlier actual bars. Until both are available it returns the current close. This corrects reversed low comparisons, shared counters across pivots, and synthetic zero history.

Mobility Oscillator now integrates uniform candle-range probability mass over common equal-width bins of the current rolling window. Zero-range candles are point masses; bins are lower-inclusive (the final bin includes the maximum). The lowest numerically tied maximum-mass bin defines the mode. The close `Length` bars ago determines the signed density deficit relative to that mode; prices outside the window have zero density. Raw output is zero until that close exists or when the window range is zero. Two selected moving averages produce `Mo` and `Signal`. This replaces the inconsistent per-bin windows and fixes the mode index that previously remained at one.

MESA Predict V1 now fits Burg autoregression at order at most `UpperLength - 1` and applies the full Hann window separately to each lag coefficient's history across bars. Previously one within-bar scratch array mixed the lags and yielded the same smoothed coefficient for every lag. Roofing filter angles and Hann angles use their full mathematical values. The fit normalizes its samples before forming products; a filtered window unresolved within 64 machine epsilons of input scale has zero fitted coefficients. `Length1` selects the forecast horizon; legacy `Length3` only computed discarded extra forecasts and is omitted from the typed options. All three outputs are exposed.

Multi-market batch evaluation now advances the benchmark state before the primary state and honors named secondary outputs. RSMK uses the difference of log relative prices across its lookback, returns zero before that lookback exists, and rejects nonpositive or nonfinite primary/benchmark prices before calculation because its logarithmic ratios require positive finite inputs. Kaufman Stress uses midpoint fraction 0.5 for a zero-range market (previously 50) and treats unresolved relative-position differences within 64 machine epsilons as zero. Relative Strength 3D uses numerical ties when assigning its discrete rank, as Technical Ratings does.

Named batch sources are passed into runtime evaluation and original benchmark OHLC bars are retained. Previously the builder silently reused the primary data for named sources, then flattened benchmark OHLC into close-only candles. Multi-market formulas now receive the explicitly registered inputs; chained price inputs retain their intended synthetic-candle representation.

Shared validation's numeric-tolerance `IndicatorValidationRule.Reference` overload now checks startup from bar zero, matching the error-budget overload's default. Customer references must supply the declared initialization values; finite but incorrect startup outputs are validation failures. Explicit post-warmup-only rules remain available through the error-budget overload's `includeWarmup: false`, and provide only that limited evidence.


### Overshoot Reduction formula correction

Overshoot Reduction now divides the smoothed absolute prediction error by its rolling maximum, matching the author's ROMA formula. Previously the raw error was divided by the smoothed maximum, allowing a gain above one and unintended feedback amplification. Values can change when the error-smoothing period exceeds one. Core component evaluation now shares the streaming startup convention; see `docs/V2_FORMULA_VARIANTS.md` for the documented variant.


### EMA and weighted-average rounding

V2 evaluates EMA's arithmetic seed and complete rational update with one final nearest-even rounding. WMA and its linear alias round their exact weighted window sum once, including zero-padded startup. This fixes overflowing intermediate sums, subnormal erasure and cancellation residues for finite inputs. Ordinary results can change in low bits; sign-based derived signals such as Elder Impulse can also change when their histogram differences are near zero. Their contract uses the documented rounded EMA trajectory, not the sign of an infinite-precision market signal. See `docs/V2_FORMULA_VARIANTS.md` for the formulas and error assumptions.

Median/trimean calculations now preserve representable averages at extreme magnitudes. Trimean's core moving-average registry now uses the same nearest-rank quartiles as the public indicator rather than interpolated quartiles. PercentRank rounds its percentage once; last-bit differences from the prior divide-then-multiply expression are expected. SimplifiedWeightedMovingAverage now follows its equivalent rolling WMA formula without unbounded cumulative-sum drift; its core registry follows the same formula and startup convention.


### Window-weight and midpoint corrections

Midpoint and Midprice preserve finite midpoints when adding their extrema would overflow. Legacy Midpoint now honors period one. Symmetric, Ehlers triangle, parabolic, cubed, Quick and Fibonacci averages use exact weighted sums with one final rounding; low-order bits can change, and representable means no longer become infinity from overflowing intermediate products. QuadraticWma and CubicWma follow their corresponding parabolic/cubed formulas. QuickMovingAverage core now follows the public asymmetric triangular-window formula rather than an exponential recurrence. Selected/chained input is preserved on the corrected arms. See `docs/V2_FORMULA_VARIANTS.md` for startup, peak caps and rounding conventions.

SquareRootWeightedMovingAverage now consistently uses rounded sqrt coefficients and exact weighted accumulation on every corrected route, replacing pow/sqrt discrepancies and intermediate overflow. See the coefficient error bound in `docs/V2_FORMULA_VARIANTS.md`.

KAMA now computes exact efficiency distances and uses once-rounded convex gain/price updates; its existing price seed is preserved. QuadraticMovingAverage core now implements the public RMS formula instead of a two-SMA extrapolation. RMS uses the available startup window and rounds the square root of the exact mean of squares once, preventing avoidable overflow and underflow.

- NaturalMovingAverage now consistently uses log-movement square-root-gap weights across core, builder, legacy and native routes. Weighted sums and the final convex price blend round once; low-order bits can change. Nonpositive inputs retain the historical zero-log convention, and zero log travel returns the preceding source price.

- SineWeightedMovingAverage now uses the public lag order for rounded sine coefficients on every route, with exact weighted accumulation and one final rounding. This fixes finite-input overflow and low-order differences caused by the core's reversed coefficients.

- EhlersHannMovingAverage now honors selected/chained inputs and uses exact weighted accumulation across its direct and native smoother routes, preventing intermediate overflow for finite prices. Rounded coefficients and zero-padded startup are preserved; low-order result bits may change.

- ChandeMomentumOscillator (including Cmo) now rounds its bounded ratio from exact price changes and rolling totals, avoiding finite-input overflow and loss of small changes after large moves expire. Signal uses the selected average of this corrected primary trajectory. Both native aliases preserve the configured signal length; customer signal averages remain supported.

Momentum/MomentumOscillator now round the final percentage ratio once, preserving tiny results previously lost by early division. Their SMA Signal uses an exact partial-window mean. Market Facilitation Index now avoids range-subtraction overflow when its final range/volume ratio is representable. Typed runs reject genuinely unrepresentable outputs; Momentum lower-level batch Signal is NaN from the first overflowing primary ratio onward. Startup and zero-denominator conventions are retained.

BalanceOfPower now evaluates its complete OHLC ratio before rounding, preventing avoidable infinity/infinity results. Its SMA signal uses an exact partial-window mean. NormalizedVolume rounds the final volume/window-sum ratio once, preserving tiny denominators and cancellation remainders; this can change legacy rounded-average results. It still returns zero before a full window and for a zero window sum.

CumulativeSum, CumulativeVolumeIndex and On-Balance Volume now retain exact internal totals and round published values once; small contributions survive later cancellation. OBV retains its zero-price seed and now honors the configured signal average/period in both native factory routes. Its original OnBalanceVolumeState(int) constructor remains available alongside an explicit average overload.
