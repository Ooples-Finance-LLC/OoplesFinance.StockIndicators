# Public builder comparisons

The kernel results in [README.md](README.md) do **not** establish builder performance or wins against the strongest competitor. The primary public-API suite is `CpuBuilderBenchmarks`; kernel timings cannot substitute for its measurements.

## Exact timed Ooples path

```csharp
var indicator = new JurikAdaptive(20, 0, 10); // selected indicator for each case
using var run = await new StockIndicatorBuilder()
    .ConfigureSource(Bars.From(preparedBars))
    .ConfigureIndicators(indicator)
    .BuildAsync();
return run.BarCount;
```

Indicator construction, source wrapping, builder configuration, input enumeration/validation, graph setup, computation, complete result materialization, and disposal are timed. `BuildAsync` completes every result series before returning. There is no kernel bypass or extra result copy. Prepared native input representations are outside timing for all libraries. Cases use a deterministic fixture and common 1/1024 input grid, 1,000 and 10,000 observations, and period 20. Asin uses its defined [-1,1] domain.

Competitor calls include fresh state where required and owned outputs. No cached calculations are reused between invocations. Outputs retain their normal public shapes; Ooples also publishes presence series for unavailable values. These are user-facing API workloads, not identical instructions, allocations, output shapes, or precision contracts.

## Competitor selection, before measuring

Candidates were checked against the complete [competitor manifest](../../competitor-library-manifest.json) and source entry points. Every included route is reported; the fastest measured eligible native route sets the target. Losing and uncertain results are retained. Conclusions apply only to these package versions and workloads.

| Family | Builder indicator | Native public routes |
|---|---|---|
| Jurik adaptive | `JurikAdaptive(20, 0, 10)` | QuanTAlib `Jma.Calc(TValue)` |
| Scaled true range | `ScaledTrueRange(20)` | QuanTAlib 1.0.0 `Atr.Calc(TBar)` |
| Rolling standard pivots | `RollingPivotLevels(20)` | Skender `GetRollingPivots(20, 0, Standard)` |
| Retrospective strict fractals | `RetrospectiveFractals(20, 20)` | Skender `GetFractal(20, 20, HighLow)` |
| Rickshaw Man | `RickshawManCandle(10, 5)` | TA-Lib `Candles.RickshawMan<double>` |
| Arcsine | `PriceCircularTransform(ArcSine)` | TA-Lib `Functions.Asin<double>` |
| Bullish short body | `BullishShortBodyCandle(20, .25m)` | Trady `BullishShortDay(...).Compute()` and `BullishShortDayByTuple(...).Compute()` |
| SMA | `Sma(20)` | TA-Lib `Functions.Sma<double>`; QuanTAlib `Sma.Calc(TValue)`; Skender `GetSma(20)` on quotes and double tuples; Trady `Sma(20)` and `SimpleMovingAverageByTuple(...).Compute()` |

Versions remain Skender 2.7.3, TA-Lib.NETCore 0.5.0, Trady 3.2.8, and QuanTAlib 1.0.0. The seven families other than SMA each have one direct library match in this inventory; Trady short body has two included output routes. This is not a claim about libraries or versions outside this inventory.

### Exclusions and semantic limits

- Conventional ATR from Skender, TA-Lib, and Trady is smoothed. The pinned QuanTAlib bar route instead returns true range divided by period because of its update-state behavior. This pilot establishes **no conventional ATR win**.
- TA-Lib Short Line uses body/shadow thresholds, unlike Trady's direction and body-percentile test. Trady Short Day and Bearish Short Day have different direction predicates.
- Calendar pivots, pivot trends, and fractal chaos bands have different windows or outputs. Skender's symmetric Fractal overload delegates to the same calculation with identical left/right spans.
- Skender's SMA reusable-result overload adds index synchronization for chaining; both fresh raw-price routes (quotes and double tuples) are included. SMA Analysis computes additional statistics.
- QuanTAlib SMA emits prefix averages before a full window. Other routes have different startup representations. Full-window comparison starts at index 19, but all startup computation and outputs are timed. Native arithmetic and Ooples' exact arithmetic remain unchanged.
- The close-only Asin kernel is diagnostic only. The primary Asin comparison includes the builder's OHLCV validation and domain-presence output.
- Retrospective fractals publish at center bars and require future data. The new builder indicator rejects live sources rather than shifting to confirmation time. Absent pivot/fractal values are zero with explicit presence flags, following existing builder conventions.

## Reproduction

```powershell
$env:COMPARISON_PAIR = 'TaLib.Functions.Sma'
dotnet benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll --filter '*CpuBuilderBenchmarks*' --exporters json --artifacts builder-performance
python scripts/verify-cpu-kernel-performance.py builder-performance TaLib.Functions.Sma --suite builder
```

The manual CPU performance workflow defaults to the builder suite and runs eight indicator families in parallel, with all competitors for each family on the same runner. Before the workflow reaches the default branch, adding the `run-builder-benchmarks` PR label explicitly starts the same builder campaign. Kernel diagnostics remain separately selectable. Regression checks cover complete output placement/presence and compare native routes to existing independently verified contracts. Evidence validation rejects missing methods/sizes, failed/dry measurements, invalid allocations, and substituted kernel reports.

## Measured builder results

[GitHub campaign](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/37805347344) for PR head `ecd3229d`, built from merge revision `85c8918d`. All 56 method/size measurements passed evidence validation. Raw reports, host information, confidence intervals, and allocations are preserved in [builder/](builder/). Competitors within each family ran sequentially on the same runner. Different families are not ranked against each other.

The fastest native route by measured mean at each size sets the comparison target below. Lower means alone do not establish a stable advantage, especially where intervals overlap. Timings are milliseconds.

At 10,000 observations, SMA, Asin, Rickshaw, and scaled true range are clear builder losses in this run. JMA and rolling pivots have overlapping intervals; fractals and short body favor Ooples. The short-body job uses one invocation per iteration because each native call is expensive, so its millisecond-scale builder samples are noisy (11.45 ms on the candle route versus 2.69 ms on the tuple route despite identical builder work). Do not treat its huge mean ratios as precise speedup estimates or use this pathological competitor path as the performance target for other indicators.

| Family | Bars | Fastest measured native route | Ooples builder | Native | Native / builder |
|---|---:|---|---:|---:|---:|
| Jurik adaptive | 1,000 | QuanTAlib.Jma | 0.2142 | 0.2304 | 1.076x |
| Jurik adaptive | 10,000 | QuanTAlib.Jma | 2.5021 | 2.3085 | 0.923x |
| Scaled true range (not ATR) | 1,000 | QuanTAlib.Atr | 0.1338 | 0.0496 | 0.371x |
| Scaled true range (not ATR) | 10,000 | QuanTAlib.Atr | 1.6100 | 0.5056 | 0.314x |
| Rolling pivots | 1,000 | Skender.GetRollingPivots | 0.6365 | 0.5387 | 0.846x |
| Rolling pivots | 10,000 | Skender.GetRollingPivots | 6.1920 | 6.1702 | 0.996x |
| Retrospective fractals | 1,000 | Skender.GetFractal | 0.1525 | 0.5444 | 3.571x |
| Retrospective fractals | 10,000 | Skender.GetFractal | 1.9075 | 5.8716 | 3.078x |
| Rickshaw Man | 1,000 | TaLib.Candles.RickshawMan | 0.2049 | 0.0676 | 0.330x |
| Rickshaw Man | 10,000 | TaLib.Candles.RickshawMan | 2.3357 | 0.6852 | 0.293x |
| Asin | 1,000 | TaLib.Functions.Asin | 0.1639 | 0.0130 | 0.079x |
| Asin | 10,000 | TaLib.Functions.Asin | 2.0363 | 0.1268 | 0.062x |
| Bullish short body | 1,000 | Trady.Candlestick.BullishShortDay | 1.4674 | 613.7884 | 418.282x |
| Bullish short body | 10,000 | Trady.Candlestick.BullishShortDay.Tuple | 2.6944 | 64,211.2380 | 23831.204x |
| SMA | 1,000 | TaLib.Functions.Sma | 0.1331 | 0.0022 | 0.017x |
| SMA | 10,000 | TaLib.Functions.Sma | 1.5583 | 0.0223 | 0.014x |

### All included routes at 10,000 observations

| Native route | Ooples builder ms | Native ms | Builder allocated bytes | Native allocated bytes |
|---|---:|---:|---:|---:|
| QuanTAlib.Jma | 2.5021 | 2.3085 | 3,722,482 | 3,970,572 |
| QuanTAlib.Atr | 1.6100 | 0.5056 | 3,717,642 | 880,774 |
| Skender.GetRollingPivots | 6.1920 | 6.1702 | 5,085,864 | 5,155,881 |
| Skender.GetFractal | 1.9075 | 5.8716 | 4,997,632 | 1,080,548 |
| TaLib.Candles.RickshawMan | 2.3357 | 0.6852 | 3,720,185 | 40,091 |
| TaLib.Functions.Asin | 2.0363 | 0.1268 | 3,798,495 | 80,063 |
| Trady.Candlestick.BullishShortDay | 11.4484 | 65,848.2267 | 3,725,272 | 119,403,624,040 |
| Trady.Candlestick.BullishShortDay.Tuple | 2.6944 | 64,211.2380 | 3,725,360 | 119,394,145,808 |
| Trady.Indicator.SimpleMovingAverage | 1.5322 | 8.7007 | 3,962,066 | 7,637,937 |
| Trady.Indicator.SimpleMovingAverage.Tuple | 1.5344 | 5.1547 | 3,962,313 | 4,839,590 |
| Skender.GetSma | 1.5388 | 0.6179 | 3,962,575 | 840,850 |
| Skender.GetSma.Tuple | 1.6285 | 0.7535 | 3,962,953 | 920,936 |
| TaLib.Functions.Sma | 1.5583 | 0.0223 | 3,961,986 | 80,057 |
| QuanTAlib.Sma | 1.5483 | 0.5022 | 3,962,692 | 2,225,029 |
