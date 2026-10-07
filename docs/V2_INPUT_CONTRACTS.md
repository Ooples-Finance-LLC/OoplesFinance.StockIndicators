# V2 input contracts

Typed finite and live sources require finite OHLCV fields. Zero and negative values remain valid unless an indicator declares a narrower domain. This permits derived series without imposing stock-price assumptions on every calculation. These checks cannot establish that vendor data is accurate.

Customer indicators can implement `IIndicatorInputDomainContract`:

```csharp
public IndicatorInputDomain InputDomain => IndicatorInputDomain.PositiveClose;
```

`IndicatorInputDomain.For(indicator)` exposes the domain enforced by the typed builder. Absolute Strength Index requires strictly positive finite effective prices: its accumulated relative-price gains and losses must be nonnegative probability masses. Negative prices can otherwise produce a zero denominator and contaminate subsequent outputs. Its legacy calculation validates the entire effective-price series before calculation; its native state rejects invalid bars before preview or commit, preserving subsequent valid updates.

The typed builder checks root domains before advancing any root state. Chained indicators check the derived close they receive. Component-average substitution checks the substituted input. A failed finite build does not return a run.

Live raw-input rejection occurs before state mutation. If a derived input or customer update fails after a dependency advances, the entire run is faulted: it publishes no partial observation and refuses further enumeration. Create a fresh run and replay accepted observations. Arbitrary customer state has no rollback interface, so the library does not pretend it can recover that graph in place.

Typed calculations also check every published output against its startup policy. Infinity is always an error. NaN requires an explicit `IIndicatorStartupContract` and is only allowed within the declared warmup. Violations throw `IndicatorOutputException`, identifying the indicator type, output slot, bar index, offending value and policy. Finite builds return no run on failure and dispose their runtime; live runs publish no partial bar and become faulted. Component results are checked before downstream calculations consume them. These guards detect invalid results; they do not certify that a finite result follows the correct formula or that intermediate arithmetic never overflowed.

Historical snapshots retain their startup bars but now mark `IsWarmedUp` accurately, including priming history and dependency warmup. A snapshot containing an unavailable NaN output is never marked ready. Live runs suppress unready snapshots unless `PublishBeforeWarmup()` is selected. Indexers, snapshots, `Of()` chaining and `Uses()` components all honor a declared `IPrimaryOutputIndicator.PrimaryOutput`; indicators without that declaration retain slot zero as their default.

The shared validator discovers the same domain on customer indicators and generates NaN, positive/negative infinity, zero and negative probes according to its field restrictions. Reports count successful argument rejections. Invalid replay observations are checked for rejection and replaced by explicitly valid recovery bars when comparing formula trajectories. Single-series batch checks do not alone establish direct-state rollback; live graph recovery/fault behavior has separate regression tests.

Every discovered single-series configuration receives zero-price and negative-price fixtures by default. They exercise formula behavior when those prices are admissible, and rejection when the indicator declares a narrower domain. These fixtures also run through paired validation's benchmark variants.

Native paired states use `IMultiSeriesInputDomainContract`. Their validator injects invalid events before accepted updates and compares subsequent outputs with fresh/reset trajectories. RSMK requires positive finite closes on both series because its logarithmic ratio is undefined otherwise. Paired timestamps/alignment are checked separately from numerical domains.

Formula-specific domains are still being rolled out across the library. Direct calls to customer state implementations and legacy `Calculate*` methods are not covered by the typed-builder boundary. A passing finite-field probe is input-boundary evidence, not evidence that all formula-specific singularities or configuration ranges have been specified.

Catalog batch runtimes now check each registered source before evaluation: OHLCV, dates and the effective input column must each contain exactly `Count` observations, and all numeric observations must be finite. The effective input is the nonempty custom column, otherwise `InputValues`. This includes named benchmark sources. Inputs are checked at `Start()`, including mutations made after constructing the source. Do not mutate source columns during a running calculation. This does not yet enforce every indicator-specific domain on catalog routes.

The native streaming engine rejects nonfinite bar fields, trade price/size and quote prices/sizes before subscription fan-out. Direct aggregators apply the same checks before buffering samples, so rejected events cannot move the reorder watermark. Signed finite values remain admissible. Quote midpoint calculations preserve finite large and subnormal values without overflowing the intermediate sum. These are raw-event guarantees; accumulated-volume overflow, indicator-specific domains, callback failures and direct native-state calls still need their own contracts.

Native states using the shared input resolver reject nonfinite OHLCV before calculation, including unused fields, and reject nonfinite custom-selector outputs. Scalar EMA and the rolling population-deviation kernel also reject nonfinite scalar observations before mutation. Direct states using other input paths still require individual enforcement and recovery evidence.
