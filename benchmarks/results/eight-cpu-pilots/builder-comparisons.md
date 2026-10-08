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

Results are pending the new campaign. No builder win is claimed while those measurements are outstanding.
