# Full-library comparisons

Delivered in [PR #255](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/pull/255), targeting `master`. The reflected manifest inventories 421 public API families from
Skender.Stock.Indicators 2.7.3, TALib.NETCore 0.5.0, Trady.Analysis 3.2.8, and
QuanTAlib 1.0.0. There are 401 paired families, 16 unavailable implementations with
verified counterparts (including two explicitly owned definitions), 4 utilities,
and no pending families.

`paired` means full-trajectory correctness and both performance runners exist;
it does not claim that timings have been collected. `pending` makes neither claim.
The production library has no competitor package dependency.

## Shared rollout status (2026-10-16)

The rollout is **incomplete**. The [saved catalog](results/shared-rollout/catalog.json.gz) covers 401 mappings: 269 with recorded native comparisons, 117 with builder-only measurements, and 15 snapshot-only mappings. Of the 269 native-comparison mappings, 245 have both standard sizes in their current catalog rows; 24 lack either 1k or 10k coverage. These are coverage counts, not speedup counts. Two stale KAMA qualification labels now agree with their latest builder-only rows; historical native measurements remain retained. Historical evidence records 102 mappings with clear wins at both 1,000 and 10,000 bars in Full and LatestOnly. The earlier count of 106 also includes 4 mappings with only one recorded size. Neither count proves current-head performance.

All 132 remaining catalog-label gaps have prepared comparisons: 117 builder-only mappings and 15 snapshot mappings. A supplemental audit also identified the 24 partial-size native mappings: Connors fills one through its own candidate, and eight additional fixtures cover the other 23 with 92 missing-size CPU cases. All 23 fixture cases and 100 GPU cases passed. The queue now has 56 preserved stages. The [initial queue audit](results/shared-rollout/prepared-queue-coverage.json) covers its first 46 stages; the [size audit and validation](results/shared-rollout/missing-native-sizes/contracts/summary.json) records the additional coverage and the 29 saved reports checked for reuse. Previously measured native sizes are excluded from the new stages. Five queued stages are archived: pivot columns, Rickshaw, Asin, SMA and Skender RSI/ADX. The two newly cataloged Skender mappings are measured gaps, with no clear latency wins; cross-run worker comparisons remain separate from same-run qualification. Fixture preparation covers these gaps; measurements, result review and qualification remain unfinished.

### Tillson polynomial candidate (2026-10-15)

Shared CPU Tillson publication now evaluates the exact finite-difference Horner polynomial, retaining one final rounding and the existing stage recurrence. This replaces four wide coefficient products with three factor products. All 17 focused unit, 26 comparator and 100 GPU cases passed. The isolated 256-publication allocation test used 178,512 bytes versus 180,264 for the prior expression (1,752 bytes less, about 1%). This small allocation result does not establish end-to-end allocation or latency improvement. Twelve builder-only timing cases will reuse the three compatible saved native baselines; native methods, inputs and package references are unchanged. [Evidence](results/shared-rollout/tillson-horner/contracts/summary.json). Qualification remains unchanged.

### ALMA retained price factors (2026-10-15)

Modern ALMA caches the exact binary decoding of each inserted price. A complete period-20 window now decodes one new price instead of all 20 retained prices; cached factors and validity flags make queue entries larger. Deferred invalid-state errors, eviction, reset, signed zero and all output contracts are preserved. All 21 unit, 16 comparator and 100 GPU cases passed, including zero steady-state per-bar allocation. Eight builder-only cases reuse the unchanged native fixture and compatible saved report. [Evidence](results/shared-rollout/alma-price-cache/contracts/summary.json). This is a work reduction with a storage tradeoff; latency and total allocation remain unmeasured.

### Certified logarithm first term (2026-10-15)

The fixed logarithm series initializes its directed sums from the first endpoints, eliminating two divisions by one per nonzero series evaluation. Subsequent terms and the certified tail bound are unchanged. All 4 independent logarithm unit tests, 10 volatility comparator tests and 100 local GPU cases passed. Eight queued builder-only timing cases reuse the saved compatible native baseline; latency remains unmeasured. [Evidence](results/shared-rollout/log-first-term/contracts/summary.json).

### Connors sorted return index (2026-10-15)

Connors retains a sorted exact-return index alongside its inclusive nullable history. At rank period 100, binary searches require at most 14 comparisons for insertion and eviction instead of the 101-comparison rank scan. Array shifts remain linear and the index adds storage; no latency or total allocation improvement is claimed. Exact ties, excluded returns, startup, reset and all eight outputs are preserved. All 262 focused unit, 10 comparator and 100 GPU cases passed, including zero warmed per-update allocation after reset. Six queued timing cases reuse the existing 10k native baseline and measure the missing 1k native arms once. [Evidence](results/shared-rollout/connors-rank-index/contracts/summary.json).

### Eight-pilot timing gate

The table below summarizes the existing catalog, before the latest worker/history repairs. Ratio = builder mean / fastest saved native mean; **below 1 is faster**. Each native choice is the faster of the recorded serial and parallel paths. These historical measurements must not be presented as timings of `86380f4d` or as results of the new candidates.

| Competitor mapping | 1k Full / Latest | 10k Full / Latest | Recorded clear wins at both sizes and modes |
|---|---:|---:|---|
| `QuanTAlib.Atr` | 0.759 / 0.536 | 0.601 / 0.378 | Yes |
| `QuanTAlib.Jma` | 0.834 / 0.802 | 0.885 / 0.851 | Yes |
| `Skender.GetFractal` | 0.555 / 0.506 | 0.555 / 0.486 | Yes |
| `Skender.GetRollingPivots` | 3.037 / 2.962 | 2.658 / 2.626 | No |
| `TaLib.Candles.RickshawMan` | 3.388 / 3.410 | 3.704 / 3.689 | No |
| `TaLib.Functions.Asin` | 3.295 / 2.540 | 3.330 / 1.308 | No |
| `TaLib.Functions.Sma` | 3.424 / 2.673 | 6.466 / 1.696 | No |
| `Trady.Candlestick.BullishShortDay` | 0.012 / 0.011 | 0.003 / 0.003 | Yes |

### Implemented and verified follow-ups

| Candidate commit | Change | Focused local verification | New latency qualification |
|---|---|---|---|
| `9a66e1bd` | Rolling pivots reuse exact decoded OHLC factors and bounded workers while retaining all 18 outputs. | 28 unit + 24 comparator + 100 GPU cases | Pending |
| `29c34f23` | Rickshaw workers replay the complete longer prior-range window; oversized windows retain the scalar path. | 18 unit + 6 comparator + 100 GPU cases | Pending |
| `332edbfb` | Asin uses exact workers from 512 bars; Full transfers chunked owned history. | 22 unit + 9 comparator + 100 GPU cases | Pending |
| `86380f4d` | Accepted Full SMA workers allocate chunked history and publish it only after validation, fallback replay and cancellation checks succeed. | 55 unit + 4 comparator + 100 GPU cases | Pending |

All listed local cases passed with zero skips. Each candidate passed 71 hosted checks with 7 intentional skips. For the latest SMA commit, [CI](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/38595056679), [competitor correctness](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/38595056541), and [CodeQL](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/38595056698) succeeded. The [local GPU receipt](results/shared-rollout/local-gpu-contracts/receipt.json) records 100 passing cases; 1,317 contract methods remain assigned to hosted CI in the fresh inventory after the direct-native fixture batches. Correctness checks do not establish latency wins.

### Rolling-pivot column storage (2026-10-15)

Identical presence flags and permanently absent levels now share run-owned storage. Every style still exposes all 18 outputs, with exact values and presence, raw-input validation priority, cancellation, and Full/Latest ownership preserved. Storage is never shared between indicator roots or separate runs.

| Style | Distinct arrays before / after | Retained array payload removed at 1k / 10k bars |
|---|---:|---:|
| Standard, Fibonacci, Woodie | 18 / 9 | 72,000 / 720,000 B |
| Camarilla | 18 / 10 | 64,000 / 640,000 B |
| DeMark | 18 / 5 | 104,000 / 1,040,000 B |

These are exact payload reductions from tested buffer ownership, excluding array headers; they are not total allocation measurements or latency wins. The production net10 build passed with zero warnings/errors; 18 focused unit cases, 12 independent comparator cases, and 100 local GPU cases passed with zero skips. Tests cover all styles, serial/workers, mixed roots, extremes, validation order, rebuild isolation, source mutation and disposal. [Source hashes, storage counts and compressed test results](results/shared-rollout/rolling-pivot-shared-cpu/column-storage/summary.json) retain the evidence. Matching-runtime timing remains pending. The later pivot commit `033e31ab`, including this storage change, passed 71 hosted checks with 7 intentional skips.

Pivot publication now also validates each distinct array only once, at its earliest owning output slot: 9 scans for Standard/Fibonacci/Woodie, 10 for Camarilla and 5 for DeMark, instead of 18. All outputs remain published, and tests for all five styles preserve the first overflow slot and the priority of later invalid raw input. The net10 production build passed with zero warnings/errors; 22 unit, 12 comparator and 100 local GPU cases passed with zero skips. [Source-bound validation evidence](results/shared-rollout/rolling-pivot-shared-cpu/unique-validation/summary.json) records these checks. This removes duplicate validation work without claiming a measured latency gain.

Serial and worker pivot execution now writes each defined level and the shared presence flag once, leaving permanently absent columns at their allocated positive zero. Output-column stores per bar fall from 18 to 8 for Standard/Fibonacci/Woodie, 10 for Camarilla and 4 for DeMark. All 18 public columns retain exact values, including signed zero. Expanded regression coverage checks wide, subnormal and signed-zero inputs with one and eight workers on both sides of worker dispatch. The net10 production build and 22 unit, 12 comparator and 100 GPU cases passed (zero skips). [Source-bound store-reduction evidence](results/shared-rollout/rolling-pivot-shared-cpu/unique-stores/summary.json) retains the checks; latency remains unqualified.

### Percentile-candle rank decisions (2026-10-15)

All eight body/shadow percentile candle indicators now stop exact history comparisons when either rank outcome is certain. Previously only a successful rank decision stopped the scan. A zero percentile now skips history comparisons entirely; warmup, candle direction and window updates remain unchanged. For a full 20-bar window at percentile 1, a current length below every prior length now needs one exact comparison instead of 19. This is a derived work reduction, not a measured latency claim.

The net10 production build passed with zero warnings/errors; 19 focused unit cases, 48 comparator cases and 100 local GPU cases passed with zero skips. Independent reference checks cover all eight patterns, ascending/descending lengths, ties, eviction, extreme decimal percentiles, subnormals, wide values, repeated previews, reset, worker boundaries and both history modes. [Source hashes and compressed test results](results/shared-rollout/percentile-candles/decided-ranks/summary.json) retain the evidence. Latest pivot source `033e31ab` also completed 71 hosted checks with 7 intentional skips; percentile-candle commit `4f65f7f4` subsequently passed 71 hosted checks with 7 intentional skips.

### Rolling-percentile equal endpoints (2026-10-15)

Fractional ranks with equal finite interpolation endpoints now return that value directly, avoiding exact weighted arithmetic on repeated values. Fractional zero remains canonical positive zero; integer ranks retain the selected signed-zero bits and existing tie ordering. Warmup averaging, eviction and reset remain unchanged.

The net10 production build passed with zero warnings/errors; 14 focused unit, 14 comparator and 100 local GPU cases passed with zero skips. Bit-exact regression checks cover constant plateaus and transitions through maximum finite values, subnormals, alternating signed zeros, adjacent values, extreme binary fractions, warmup, eviction, reset and worker replay. [Source hashes and compressed test results](results/shared-rollout/rolling-percentile-cpu/equal-endpoints/summary.json) retain the evidence. This reduces arithmetic work without claiming a measured latency gain. The follow-up analysis fixes passed with commit `20d4302a`: 71 hosted checks succeeded with 7 intentional skips.

### Return-beta moment products (2026-10-15)

In All mode, standard and directional moment windows now share one exact square and cross-product for each nonzero return insertion or eviction. This removes the duplicate pair of products without retaining extra history. Up-only and Down-only selections also skip moment conversion for excluded returns; raw return calculation, publication and window eviction still occur. Standard-only beta retains its existing arithmetic.

The net10 production build passed with zero warnings/errors; 35 focused unit, 27 comparator and 100 local GPU cases passed with zero skips. Independent reference checks cover all selections and output slots, wide/subnormal returns, excluded runs evicting included observations, reset, workers, ownership and input-error priority. Existing zero-per-bar-allocation assertions also pass. Hosted analysis found exact-equality and complexity findings in the preceding rolling-percentile change; interpolation is now a separate helper, with its deliberate exact comparison documented. Pivot validation also has explicit braces. Another 36 percentile/pivot unit cases passed after these analysis fixes. [Source hashes and compressed test results](results/shared-rollout/compact-return/shared-products/summary.json) retain the evidence. Product counts describe reduced work, not measured latency. Commit `20d4302a` passed 71 hosted checks with 7 intentional skips, including Sonar analysis.

### Unchanged percentile and mode windows (2026-10-15)

Rolling percentiles now skip two binary searches and sorted-list removal/insertion when the incoming value equals the outgoing nonzero value. Zero replacements retain the original tie ordering. Rolling mode reuses its prior full-window result when replacement leaves frequencies unchanged, including equivalent signed zeros. Both still rotate history for future eviction; mode retains one additional scalar result per state. This covers five QuanTAlib/Trady mappings without changing their public outputs.

The net10 production build passed with zero warnings/errors; 24 focused unit, 22 comparator and 100 local GPU cases passed with zero skips. Independent references cover periodic and constant windows, later eviction, warmup, reset, wide/subnormal values, signed-zero bits, workers, ownership and input rejection. [Source hashes and compressed test results](results/shared-rollout/order-statistics/unchanged-windows/summary.json) retain the evidence. Commit `84a89e99` passed all 71 hosted checks with 7 intentional skips, including Sonar analysis. Fresh latency qualification remains pending.

### Unchanged paired windows (2026-10-15)

Correlation and full pair-statistics builders reuse the preceding values and presence when a full window replaces a pair with an equal pair. Certified integer columns also skip unchanged moment updates. Wide fallback still rotates its state for later eviction. Each worker computes its first published row independently, so reuse never reads another worker's output. No additional retained history is introduced; captured-input validation, cancellation and output-error ordering remain intact.

The net10 production build passed with zero warnings/errors; 20 focused unit, 13 comparator and 100 local GPU cases passed with zero skips. Bit-exact checks cover periodic windows, later eviction, both flat-correlation settings, worker seeds, wide/subnormal and signed-zero inputs, every output, ownership and input-error priority. [Source hashes and compressed test results](results/shared-rollout/pair-statistics/unchanged-windows/summary.json) retain the evidence. This reduces computation without claiming a latency win. Commit `08a6edf9` passed all 71 hosted checks with 7 intentional skips; fresh latency qualification remains pending.

### Compact ALMA weight products (2026-10-15)

Modern Arnaud Legoux windows retain normalized mantissa/exponent factors for Gaussian weights instead of general dyadic arithmetic states. Each weighted term now forms the exact product of two at-most-53-bit mantissas directly in Int128, avoiding general multiplication dispatch and normalization. Exact accumulation and final rounding, weight generation, window ordering, startup, reset and the framework fallback remain unchanged.

The net10 production build passed with zero warnings/errors. All 120 selected unit cases passed, including 18 directly relevant shared ALMA cases; the remaining cases cover legacy ALMA/composition paths. All 16 ALMA comparator cases and 100 local GPU cases passed with zero skips. Independent references cover wide cancellation, subnormals, signed zero, underflowed weights, extreme parameters, both startup modes, reset and worker ownership. Existing zero-per-bar-allocation assertions pass. [Source hashes and compressed test results](results/shared-rollout/alma/compact-products/summary.json) retain the evidence. This reduces stored representation and arithmetic work without claiming measured latency. The combined production changes passed all 71 hosted checks with 7 intentional skips at `d90d37ad`.

### Compact sinc coefficients and history (2026-10-15)

Modern WindowedSinc retains normalized mantissa/exponent factors for both shared coefficients and per-state price history instead of general dyadic arithmetic states. Each weighted term forms an exact Int128 product of two at-most-53-bit mantissas, including negative sinc coefficients. Exact accumulation and final rounding, coefficient generation, oldest-first ordering, newest-price startup extension, lazy history growth and reset remain unchanged.

The net10 production build passed with zero warnings/errors; 15 focused unit, 7 comparator and 100 local GPU cases passed with zero skips. Bit-exact references cover all five window functions, odd/even taps, wide cancellation, subnormals, signed zero, full-width mantissas, startup and reset. Worker boundaries, ownership, overflow/input-error priority, cancellation and unsupported GPU rejection remain covered. Existing zero-per-bar-allocation assertions pass. [Source hashes and compressed test results](results/shared-rollout/windowed-sinc/compact-products/summary.json) retain the evidence. This reduces representation and computation without claiming measured latency. The combined production changes passed all 71 hosted checks with 7 intentional skips at `d90d37ad`.

### Compact exact shared weighted kernels (2026-10-15)

Gaussian, sine and non-affine normalized-convolution windows now use the existing exact Int128 dyadic accumulator on modern runtimes. Decoded coefficient factors replace the larger coefficient-with-limit records, and each weighted term uses an exact product of at most 106 mantissa bits. Ordinary floating-point products no longer require BigInteger merely because they exceed 64 bits. Wider exponent spans still use the exact fallback. Prefix normalization, zero-mass averaging, cached coefficients, lazy history growth, wrap order and reset remain unchanged; the framework path retains its existing arithmetic.

The net10 production build passed with zero warnings/errors; 25 focused unit, 32 comparator and 100 local GPU cases passed with zero skips. New warmed period-20 tests assert zero per-bar managed allocations for Gaussian, sine and a non-affine floating-point convolution. Independent references cover signed/zero-mass kernels, extreme and subnormal values, partial resets, ring growth, worker boundaries, mixed roots, ownership, overflow/input-error priority and cancellation. [Source hashes and compressed test results](results/shared-rollout/weighted-kernels/compact-products/summary.json) retain the evidence. This reduces representation and computation without claiming measured latency. The combined production changes passed all 71 hosted checks with 7 intentional skips at `d90d37ad`.

### Ordered directional true-range endpoints (2026-10-15)

Selected TA-style directional indicators now choose the extended endpoints of ordered high/low candles using exact comparisons, then compute one exact difference for true range. This replaces three differences and two exact maximum comparisons on that path. Inverted candles retain the original formula. One additional previous-close scalar preserves endpoint selection without decoding retained factors; DX's separate current-close range update retains its native recurrence.

The net10 production build passed with zero warnings/errors; 14 focused unit, 23 comparator and 100 local GPU cases passed with zero skips. Independent references cover all selected measures, periods and suppression settings, gaps above/below the range, flat and inverted candles, current-close DX updates, signed zero, subnormals, extended unpublished stages and reset. Builder/native-prefix setup checks cover worker thresholds and both history modes; raw input validation remains covered. [Source hashes and compressed test results](results/shared-rollout/prior-directional/ordered-range/summary.json) retain the evidence. This reduces exact arithmetic work without claiming measured latency. The combined production changes passed all 71 hosted checks with 7 intentional skips at `d90d37ad`.

### Evidence still required

- The four pilot repairs, KAMA runtime updates, Tillson candidate and ALMA candidate reuse compatible saved native baselines and measure builder paths only. The other queued stages establish missing compatible baselines once. Every stage preserves its candidate binaries and source hashes; runtime and hardware checks govern saved-baseline reuse under the [baseline reuse policy](results/shared-rollout/baseline-reuse-policy.json).
- The completed pivot worker attempt is **diagnostic only**: its monitor omitted reusable MSBuild nodes and standalone compiler activity. Its latency ratios are excluded from the table and from qualification. Interrupted attempts are also excluded. The corrected monitor includes compiler processes and descendants and distinguishes the benchmark's own process tree from competing work.
- The remaining builder-only evidence gaps overlap as follows. Matching runtime alone does not establish a best-path comparison.

| Builder-only cohort | Mappings | Evidence still needed |
|---|---:|---|
| Runtime mismatch and no saved native serial/parallel method pair | 100 | Matching-runtime builder evidence and compatible native baselines |
| Runtime mismatch with both native methods retained | 2 (KAMA) | Matching-runtime builder evidence, with fixture and materialization compatibility checked |
| No recorded runtime mismatch, but no saved native method pair | 17 | Compatible native baselines and resolution of fixture/output-contract differences |

The 102 runtime-mismatched mappings account for 204 size rows. The saved-artifact audit inspected 1,651 repository JSON artifacts (968 benchmark reports) and found no parallel rows keyed to the other 117 mappings; unkeyed reports retain their separate comparison families. A bounded external follow-up read 997 named BDN reports from SI temporary folders, excluded two unreadable reports, and likewise found no keyed matches. These are file counts, not unique benchmark runs; [audit scope and invalidation rules](results/shared-rollout/baseline-reuse-policy.json) retain the limits of this search. Establish a missing baseline only when required for qualification, save it for reuse, and do not rerun compatible saved competitors. Some gaps also require benchmark implementation: older builder-only harnesses and normalized `LibraryPairBenchmarks.Competitor` results do not provide direct native serial/parallel measurements. The following fixtures now expose both eager native arms and both builder histories at 1,000 and 10,000 bars. Their first direct-native timings remain pending, so qualified coverage is unchanged.

| Direct-native fixture | Mappings | Passing contract cases | Benchmark cases | Evidence |
| --- | ---: | ---: | ---: | --- |
| Skender RSI / ADX | 2 | 19 | 16 | [Strength](results/shared-rollout/skender-strength/direct-native-contracts/summary.json) |
| Trady strength family | 5 | 46 | 40 | [Trady strength](results/shared-rollout/trady-strength/direct-native-contracts/summary.json) |
| Skender MACD / PVO / PMO / TSI / TRIX | 5 | 81 | 40 | [EMA family](results/shared-rollout/skender-ema-family/direct-native-contracts/summary.json) |
| Skender Alligator / Gator / Elder Ray | 3 | 43 | 24 | [Smoothing](results/shared-rollout/skender-smoothing/direct-native-contracts/summary.json) |
| Skender Awesome / BOP / CCI | 3 | 49 | 24 | [Finite windows](results/shared-rollout/skender-finite-window/direct-native-contracts/summary.json) |
| Skender ATR Stop / SuperTrend / Chandelier / Keltner / Starc | 5 | 56 | 40 | [ATR family](results/shared-rollout/skender-atr-family/direct-native-contracts/summary.json) |
| Skender Stochastic / Stochastic RSI / SMI | 3 | 73 | 24 | [Stochastic](results/shared-rollout/skender-stochastic/direct-native-contracts/summary.json) |
| Skender ROC / ROC with bands | 2 | 35 | 16 | [ROC](results/shared-rollout/skender-roc/direct-native-contracts/summary.json) |
| Skender Doji / Marubozu / Heikin-Ashi | 3 | 56 | 24 | [Candles](results/shared-rollout/skender-candle/direct-native-contracts/summary.json) |
| Skender Beta / Correlation / EPMA | 3 | 43 | 24 | [Statistics](results/shared-rollout/skender-statistics/direct-native-contracts/summary.json) |
| Skender MA envelopes / SMA analysis / PRS / deviation channels | 4 | 54 | 32 | [Diagnostics](results/shared-rollout/skender-diagnostics/direct-native-contracts/summary.json) |
| Trady EMA / WMA / SMA oscillator / EMA oscillator | 4 | 65 | 32 | [Averages](results/shared-rollout/trady-average/direct-native-contracts/summary.json) |
| Trady MACD / MACD histogram | 2 | 33 | 16 | [MACD](results/shared-rollout/trady-macd/direct-native-contracts/summary.json) |
| Trady directional movement / DI / DX / ADX / ADXR | 7 | 99 | 56 | [Directional](results/shared-rollout/trady-native-directional/direct-native-contracts/summary.json) |
| Trady raw / Fast / Slow / Full stochastic and differences | 7 | 127 | 56 | [Trady stochastic](results/shared-rollout/trady-native-stochastic/direct-native-contracts/summary.json) |
| Trady stochastic momentum / smoothed momentum / stochastic RSI | 3 | 52 | 24 | [Momentum](results/shared-rollout/trady-stochastic-momentum/direct-native-contracts/summary.json) |
| Trady Chandelier / Keltner channels | 2 | 25 | 16 | [ATR channels](results/shared-rollout/trady-atr-channel/direct-native-contracts/summary.json) |
| Trady CCI / KAMA | 2 | 31 | 16 | [CCI / adaptive](results/shared-rollout/trady-cci-adaptive/direct-native-contracts/summary.json) |
| Trady positive / negative volume index / ADL | 3 | 69 | 24 | [Volume](results/shared-rollout/trady-native-volume/direct-native-contracts/summary.json) |
| Trady Dark Cloud Cover / Dragonify Doji / rising and falling three methods | 4 | 65 across two runs | 32 | [Trady candles](results/shared-rollout/trady-native-candle/direct-native-contracts/summary.json) |
| TA-Lib morning / evening stars, doji stars and Abandoned Baby | 5 | 61 | 40 | [Star reversals](results/shared-rollout/ta-star-reversal/direct-native-contracts/summary.json) |
| TA-Lib exhaustion / crows and soldiers / penetration patterns | 8 | 153 | 64 | [Reversal / continuation](results/shared-rollout/ta-reversal-continuation/direct-native-contracts/summary.json) |
| TA-Lib multi-bar / Hikkake / gap patterns | 10 | 111 | 80 | [Multi-bar](results/shared-rollout/ta-multibar-native/direct-native-contracts/summary.json) |
| TA-Lib existing triple-body patterns | 4 | 32 | 32 | [Triple body](results/shared-rollout/ta-triple-body/direct-native-contracts/summary.json) |
| TA-Lib AvgDev / Accbands | 2 | 27 | 16 | [Windows](results/shared-rollout/ta-window-native/direct-native-contracts/summary.json) |
| TA-Lib MACD / fixed MACD / extended MACD | 3 | 46 | 24 | [MACD](results/shared-rollout/ta-macd-native/direct-native-contracts/summary.json) |
| TA-Lib Stoch / StochF / StochRsi | 3 | 40 | 24 | [Stochastic](results/shared-rollout/ta-stochastic-native/direct-native-contracts/summary.json) |
| TA-Lib MA / APO / PPO | 3 | 40 | 24 | [Averages](results/shared-rollout/ta-average-native/direct-native-contracts/summary.json) |
| TA-Lib Trix / KAMA / Mavp | 3 | 40 | 24 | [Adaptive / rate](results/shared-rollout/ta-adaptive-rate-native/direct-native-contracts/summary.json) |
| TA-Lib Hilbert phase / sine / trendline / trend mode | 4 | 54 | 32 | [Hilbert](results/shared-rollout/ta-hilbert-native/direct-native-contracts/summary.json) |

The two remaining KAMA runtime gaps reuse their saved .NET 10.0.12 native serial/parallel measurements. Ten existing fixture tests passed; fixture generation, native implementations and package references match baseline source 93cb7084. Only eight builder cases are queued on the matching runtime. [Compatibility evidence](results/shared-rollout/kama-baseline-reuse/contracts/summary.json) preserves baseline hashes and cross-run limitations.

All 15 snapshot mappings retain their standalone output contracts; these comparisons do not establish BuildAsync performance. Fourteen mappings have eager native serial/parallel arms. ZigZag has its original eager serial arm because independent partitioning has not been established for its unbounded future retraces. All listed contract cases and the associated 100-case local GPU checks passed; first timings remain pending.

| Snapshot fixture | Mappings | Passing contract cases | Timing cases | Contract retained |
| --- | ---: | ---: | ---: | --- |
| [DPO / Fractal Chaos Bands](results/shared-rollout/skender-retrospective-native/direct-native-contracts/summary.json) | 2 | 43 | 12 | DPO uses 11 future bars; FCB replays confirmed bands. |
| [Hurst](results/shared-rollout/skender-hurst-native/direct-native-contracts/summary.json) | 1 | 19 | 6 | Twenty preceding closes; both exponents retained. |
| [Skender Ichimoku](results/shared-rollout/skender-ichimoku-native/direct-native-contracts/summary.json) | 1 | 33 | 6 | 77 preceding and 26 future bars; five shifted fields. |
| [Trady Ichimoku](results/shared-rollout/trady-ichimoku-native/direct-native-contracts/summary.json) | 1 | 30 | 6 | Explicit native coordinates retain Count + 51 rows. |
| [Parabolic SAR variants](results/shared-rollout/parabolic-native/direct-native-contracts/summary.json) | 4 | 98 | 24 | TA packed doubles, Skender reversals and Trady sequential cache behavior. |
| [Volatility Stop](results/shared-rollout/skender-volatility-stop-native/direct-native-contracts/summary.json) | 1 | 21 | 6 | Prefix replay falls back to full input until startup suppression is final. |
| [Calendar / trend pivots](results/shared-rollout/skender-pivot-native/direct-native-contracts/summary.json) | 2 | 61 | 12 | Calendar prefixes; 22 future bars for retrospective trend lines. |
| [Renko / ATR Renko / ZigZag](results/shared-rollout/skender-chart-native/direct-native-contracts/summary.json) | 3 | 61 | 16 | ATR size computed inside timing; ZigZag uses the original serial API. |

The table includes 113 newly implemented mappings and four mappings from the existing triple-body fixture. Its 32 tests passed using current binaries; the existing 100-case GPU receipt was reverified. The saved serial-only triple-body comparison uses different inputs, so this existing four-arm fixture is queued for its first compatible native baseline.

The packed TA harness retains native integer or double columns, including integer TrendMode outputs. All 247 affected contract cases passed, plus 100 GPU cases. Seven queued packed fixtures use the latest common source state before first timing.

The packed TA harness now supports prepared native inputs and full owner-output checks for Trix/KAMA. All 193 affected cases across five fixture families passed, plus 100 GPU cases. All five queued candidates use this verified common source state before first timing.

The TA window and MACD fixtures share packed output allocation, native alignment checks and worker joining. All 73 affected contract cases passed, plus 100 GPU cases. Their preserved timing candidates use this shared source state before their first measurements.

The three TA candle fixtures share native packing, alignment and worker overlap mechanics. All 325 affected cases passed, including shifted Hikkake formation/confirmation examples across worker boundaries. The three queued candidates use the latest verified shared source state before their first timing runs. The linked multi-bar evidence contains the updated source hashes and combined test record.

Trady candle evidence covers 64 passing follow-up cases plus the completed 10,000-bar Falling Three Methods case retained from an intentionally stopped run. That earlier run is not reported as passing. Its completed case also verified repeated output ownership; the follow-up retains those repeat checks through 1,000 bars and checks both native paths against independent references at every size. The pinned native LongDay implementation rebuilds full-series percentiles inside individual predicate evaluations; diagnostic test durations are not performance measurements. [Coverage verifier and source evidence](results/shared-rollout/trady-native-candle/direct-native-contracts/verify-case-coverage.py) retain this distinction.

The seven Skender fixture families share partition setup, eager native result joining and date checks. All 356 affected contract cases passed after this consolidation ([evidence](results/shared-rollout/skender-native-support/summary.json)); first native timings remain pending.

The evidence retains source hashes, native API/materialization choices and independent arithmetic/output checks. Each batch refreshes the required 100-case local GPU receipt. All 71 hosted checks passed for combined strength/EMA-family commit 1006ef85, with 7 intentional skips; later fixture batches await hosted verification.

- Snapshot-only measurements retain their separate API contract; they do not establish builder or GPU performance. Auto remains CPU; RequiredGPU executes a supported device path or rejects it.

The sections below retain the historical implementation and measurement record. Their dated coverage totals are milestones, not the current aggregate.

## Implemented comparisons

- SMA, WMA, and EMA in all four libraries.
- Skender and TA-Lib DEMA/TEMA, with distinct initialization formulas.
- TA-Lib and QuanTAlib triangular windows, plus TA-Lib rolling sum.
- TA-Lib Min, Max, MinMax, four price transforms, true range, and all five
  momentum/rate-of-change forms.
- Skender and Trady true range.
- Skender, TA-Lib, and Trady on-balance volume with explicit seed conventions.
  Skender includes its optional SMA and all startup presence flags.
- Skender ROC with momentum, nullable percentage change, and optional nullable SMA.
- Skender ROC with seeded EMA and symmetric RMS bands, including all presence flags.
- Twelve Trady rolling and historical high/low families.
- Trady momentum, gain/loss momentum, and their three generic difference families.
- Trady rate of change and generic percentage difference, including nullable results.
- TA-Lib engulfing, five average-range doji patterns, seven body/shadow patterns,
  four context-reversal patterns, Harami/Harami Cross/Homing Pigeon/Doji Star, and Matching Low/Stick Sandwich/
  Three Line Strike/Three Outside, Piercing Line/Dark Cloud Cover, and On Neck/In Neck/Thrusting,
  Three Inside, Two Crows, Upside Gap Two Crows, Unique Three River, Kicking/Kicking By Length, Rickshaw Man,
  Counterattack, Separating Lines, Gap Side-by-Side White Lines, Three Black Crows,
  Identical Three Crows, Three White Soldiers, Morning/Evening Star,
  Morning/Evening Doji Star, Abandoned Baby, Tristar, Tasuki Gap,
  Up/Down-side Gap Three Methods, Breakaway, Concealing Baby Swallow, Ladder Bottom,
  Hikkake, Modified Hikkake, Mat Hold, Rising/Falling Three Methods, Advance Block,
  Stalled, and Three Stars in the South: all 61 TA-Lib candle families.
- Trady candle direction, up/down trends, trend-qualified engulfing, Doji,
  Dragonfly Doji (both API spellings), Gravestone Doji, and eight percentile-based
  body/shadow patterns, plain and directional Harami, delayed Dark Cloud Cover,
  upside/downside Tasuki gaps, bullish/bearish Abandoned Baby,
  variable-length Rising/Falling Three Methods, and all five trend-star API families.
- Skender Doji and Marubozu, including all eleven calculated outputs and nullable
  price presence. TA-Lib rolling minimum/maximum indices (including the combined
  API), close midpoint, and high/low midpoint.
- TA-Lib raw Balance of Power and Skender simple-averaged Balance of Power,
  including missing windows caused by zero-range candles.

The candle counterparts add 92 public v2 indicators with independent validation
contracts and boundary, extreme-value, reset, and lifecycle checks. Different
competitor definitions have distinct public indicators: Trady uses decimal-ratio
and trend definitions, while TA-Lib uses adaptive prior-window thresholds.
Hammer/Hanging Man near windows exclude the current and immediately preceding
candle. Gaps and shadow comparisons are strict; near boundaries are inclusive.
Takuri requires a lower shadow longer than twice the body; Belt Hold checks the
opening-end shadow.

Trady scalar and close extrema use prepared close-only bars; high/low extrema use
original OHLC fields. Historical counterparts use an Ooples window as long as the
complete input, preserving every expanding-prefix output. Distinct-field fixtures
verify these mappings. Price and extrema timings use realistic OHLC datasets.

TA-Lib AvgPrice maps to Ooples FullTypicalPrice (OHLC/4). Ooples AveragePrice is
the open/close midpoint and is not the same formula. All three true-range
comparisons start at the second candle; Ooples additionally supplies the first
candle's own range. TA-Lib 0.5.0 rejects one-bar inputs even for zero-lookback price
transforms. Executable boundary tests pin that limitation, which is recorded in
the manifest. Ooples' first-bar transform values are checked separately.

Harami permits inclusive body containment; Harami Cross and Homing Pigeon require
strict containment, and Doji Star requires a strict directional body gap. The prior
long-body window excludes that prior candle. The current short-body/doji window
includes it. Independent references and hand-checked tests cover both offsets,
threshold equality, candle color, strict boundaries, and extreme/subnormal prices.

Matching Low and Stick Sandwich use inclusive close tolerances of one twentieth
of the mean range preceding the anchor candle. Three Line Strike uses one-fifth
near-body tolerances on separate historical windows, strict advancing closes,
and a final opposite candle crossing the first open. Its signal retains the first
three candles' direction. Three Outside requires strict engulfing and close
confirmation, without a color constraint on the third candle; its lookback is three.

EMA uses an initial SMA in Skender, TA-Lib, and QuanTAlib. Trady instead starts
from the first close, matched by the new public `FirstValueEma`. Its exact convex
recurrence handles extreme and subnormal values without intermediate overflow.
QuanTAlib's compensated initialization and explicit-alpha constructor remain
listed as pending configurations even though its conventional EMA pair is ready.

`LaggedPriceChange` exposes difference, fractional change, percentage change,
price ratio, percentage ratio, and positive gain/loss as explicit formula choices. History is lazy,
startup outputs are zero, and a zero denominator yields zero for ratio forms.
Exact arithmetic preserves finite results across overflowing intermediate
differences; the runtime rejects mathematically unrepresentable outputs.
Independent rational references verify that distinction and lifecycle behavior.
Trady negative difference is the positive loss magnitude, not a signed negative
change. Both directional forms clamp the opposite direction before overflow.
Pinned-package tests also check the generic APIs and tuple entry points.

`RateOfChangeWithValidity` publishes percentage change and an `IsDefined` flag.
It distinguishes a real zero return from missing history or a zero denominator,
matching Trady's nullable results. The comparison derives its presence mask from
the public flag, and checks the competitor's nullable output against an independent
reference. Both public outputs have exact contracts and lifecycle checks.

`RateOfChangeWithAverage` supplies Skender's three numeric outputs and explicit
ROC/average validity flags. Its average uses rounded ROC values and becomes
unavailable while any missing ROC remains in the window. Disabled averaging
is checked as an entirely absent output. Tests cover disabled and multiple
averaging periods; timings enable a 20-bar average and return all three outputs.
The primary output is ROC. Public raw momentum and ROC overflow are rejected;
exact rolling sums preserve finite averages when an ordinary sum would overflow.

`RateOfChangeRmsBands` matches Skender ROC-with-bands: its bands are symmetric
root-mean-square returns, not centered standard deviation. A missing return
permanently invalidates the EMA, including during its seed; bands recover after
missing returns leave their window. Exact square accumulation avoids intermediate
overflow. Seven public outputs (four numeric series and three validity flags)
have independent contracts; the comparison preserves all four nullable series.

`SeededExponentialAverage` implements DEMA and TEMA with explicit initialization:
Skender seeds all stages with the same first SMA; TA-Lib seeds each stage from
the preceding stage's mature outputs. Their startup lengths differ. Exact convex
recurrences and final extrapolation preserve extreme constants and reject true
output overflow. TA-Lib rejects period one, pinned by tests and the manifest;
Skender and Ooples support it. QuanTAlib's compensated DEMA/TEMA remain pending.

`TriangularWindowAverage` uses exactly N prices, with weights rising toward the
center and falling symmetrically. Even periods have a two-price plateau. This
is distinct from the existing two-N-period-SMA triangular formula. Two exact
rolling sums implement the convolution with one final rounding and constant
update time. `RollingPriceSum` preserves cancellation and rejects true sum
overflow. Both grow storage lazily. TA-Lib rejects period one for both APIs;
Ooples supports it, as does QuanTAlib Trima. Boundary tests pin those differences.

SeededOnBalanceVolume starts at zero for Skender and at first volume for TA-Lib
and Trady, including negative or zero initial closes. Equal closes ignore volume.
Its optional average excludes the seed bar, matching Skender: the first N-value
window is bars 1 through N. Exact accumulation retains small residuals through
large cancellation; true output overflow is rejected. TA-Lib rejects singleton
inputs, recorded by an executable boundary check. Timings use varying volumes.

Piercing Line requires two strictly long bodies, a strict opening gap below the
prior low, and a close strictly inside the prior body above its midpoint. Dark
Cloud Cover requires only the preceding bullish body to be long, a strict gap
above its high, and a close strictly inside its body beyond the penetration
fraction (default 0.5). Exact rolling sums and product comparisons preserve
boundaries at extreme and subnormal prices. Both threshold windows exclude the
body they classify. Dark Cloud Cover accepts finite nonnegative penetration;
values at least one cannot match. Trady PiercingLine remains a throwing stub
with this independently verified alternate counterpart.

StrictHaramiCandle uses opposite bearish/non-bearish colors with strict body
containment and optional strict shadow containment. A doji may match after a
bearish body. BullishHaramiPattern and BearishHaramiPattern additionally require
a strictly directional current candle and prior consecutive falling/rising opens
and closes, respectively. They use constant storage, without subtracting prices.

Trady's directional Harami object/generic routes ignore containedShadows and use
open/close trends rather than high/low trends. Their tuple wrappers instead run
plain Harami, honor containedShadows, and ignore trend period. Executable checks
cover every route and both shadow settings. Tuple counterparts use
StrictHaramiCandle; the public directional counterparts honor their shadow option.
These package limitations are recorded in the manifest.

DelayedDarkCloudCoverPattern preserves Trady's distinct formula: a bullish
anchor after strictly rising highs/lows, then a bearish candle opening above the
anchor close and closing below its midpoint. There is no long-body requirement,
no gap above the prior high, and no lower bound on the bearish close. A delay of
one reports immediately on the bearish candle; larger delays only defer reporting.
Trady's downTrendPeriodCount selects that delay without confirming a downtrend.
Its longPeriodCount/longThreshold arguments are ignored; delay zero fails when it
accesses beyond the final bar. Executable tests cover object, generic, and tuple
routes. The public counterpart requires positive delay and uses exact midpoint
comparisons and storage that grows with received history, not the declared delay.

OnNeckCandle, InNeckCandle, and ThrustingCandle require a prior strictly long
black body and a white candle opening strictly below its low. On Neck closes
within an inclusive tolerance of the prior low; In Neck closes from the prior
close through that close plus tolerance; Thrusting closes strictly beyond that
tolerance and at or below the prior body midpoint. White includes unchanged
open/close values. Tolerance is one twentieth of the mean of five prior ranges;
longness uses ten prior bodies. Both windows exclude the prior pattern candle.
The public periods are independently configurable, with exact rolling sums and
comparisons that preserve range/body overflow and subnormal boundary behavior.

ThreeInsideCandle requires a short second body strictly contained in a long
first body and an opposite-color final close beyond the first open. TwoCrowsCandle
requires a long white body, a black body gapping above it, and a third black
candle opening inside the second body and closing inside the first. It has no
short-body requirement. UpsideGapTwoCrowsCandle instead requires a short second
body and a third black body strictly engulfing it while closing above the first.
UniqueThreeRiverCandle requires a long black first body, a black second body
closing inside it and making a lower low, then a short white body opening above
that low. Its second open may equal the first open.

Three Inside and Upside Gap Two Crows allow short-body equality; Unique Three
River requires strict shortness. Each threshold excludes the body it classifies,
including the third-body threshold in Unique Three River. Exact rolling sums,
independent rational references, and hand-calculated fixtures pin these offsets
and boundary rules. Trady UpsideGapTwoCrows is a confirmed throwing stub linked
to the verified alternate counterpart.

KickingCandle and KickingByLengthCandle require two opposite, strictly long
bodies with both shadows strictly below one tenth of the preceding mean range,
plus a strict full-range gap in the second candle's direction. Each candle uses
its own preceding body/range windows. Kicking reports the second color; Kicking
By Length reports the longer body's color, selecting the first on an exact tie.
RickshawManCandle requires an inclusive doji-body threshold, both shadows strictly
longer than the body, and a body intersecting the inclusive range-midpoint band.
Its band extends one fifth of a separate preceding mean range on either side.
Exact body-length and midpoint comparisons preserve ties and finite classification
when ordinary subtraction or midpoint addition would overflow.

CounterattackCandle requires opposite, strictly long bodies with closes inside an
inclusive tolerance of one twentieth of the mean range preceding the first candle.
SeparatingLinesCandle instead compares opens with that tolerance and requires only
the current body to be strictly long, with its opening-end shadow strictly below
one tenth of the mean range preceding the current candle. Both report the second
color. GapSideBySideWhiteLinesCandle reports the direction of two strict body gaps
relative to the first candle. The second and third candles are white (including
dojis), with inclusive open and body-size tolerances of one twentieth and one
fifth of their respective mean ranges preceding the second candle. Exact linear
comparisons preserve inclusive boundaries and unrepresentable intermediate bodies.

ThreeBlackCrowsCandle requires a preceding white candle, three black candles with
strictly declining closes and very short lower shadows, and later opens strictly
inside the preceding bodies. IdenticalThreeCrowsCandle instead uses inclusive
open-to-prior-close tolerances of one twentieth of preceding mean ranges; it does
not require a preceding white candle. ThreeWhiteSoldiersCandle requires strictly
increasing closes and very short upper shadows; later opens may extend up to one
fifth of the preceding mean range beyond the prior close. Body shrinkage must be
strictly less than three fifths of the preceding mean range, and the final body
must strictly exceed its prior mean. Each threshold excludes its classified candle.
Exact arithmetic preserves strict and inclusive boundaries even for overflowing
rolling sums and subnormal prices. Default lookbacks are 13, 12, and 12 bars.

MorningStarCandle and EveningStarCandle require a long first body, a short middle
body separated by a strict body gap, and a final opposite body above its prior
short-body mean. MorningDojiStarCandle and EveningDojiStarCandle replace the middle
short-body threshold with one tenth of the preceding mean range. The middle
threshold is inclusive; both outer-body thresholds and final penetration are
strict. Penetration defaults to 0.3, accepts finite nonnegative values, and may
exceed one because the final close need not remain inside the first body. Independent
periods have lazy storage and exact rolling comparisons, including exact products
for penetration. Default first-valid index is 12 for all four patterns.

AbandonedBabyCandle applies the doji-star reversal body and penetration thresholds
in either direction, but requires strict full high/low gaps on both sides of the
middle doji. TristarCandle compares all three dojis against the same range threshold
preceding the first candle; a strict middle-body gap followed by a retreat of the
last body signals reversal without requiring a second complete gap. TasukiGapCandle
requires the opposite final body to open strictly inside the second body and close
strictly inside the gap. Its body-size difference must be strictly below one fifth
of the range mean preceding the second candle; the first candle color is irrelevant.
UpDownSideGapThreeMethodsCandle instead requires the first two colors to agree,
with the opposite final close strictly inside the first body. Its lookback is two;
default Tasuki, Tristar, and Abandoned Baby lookbacks are seven, twelve, and twelve.

BreakawayCandle requires a long first body, a strict body gap, two further candles
with strictly continuing highs/lows, and an opposite fifth close inside the gap.
The third color is unrestricted. ConcealingBabySwallowCandle requires four black
candles and reports a bullish signal: the first two have strictly very short
shadows; the third gaps down, with an upper shadow strictly above its threshold
and extending into the prior body; the fourth strictly engulfs its full range.
LadderBottomCandle requires three black candles with declining opens/closes,
a fourth black candle with an upper shadow above its threshold, and a white final
candle opening above the fourth body and closing above its high. A final doji can
qualify. Their default lookbacks are fourteen, thirteen, and fourteen.

The four star reversals and Abandoned Baby have a documented penetration boundary
difference. TA-Lib rounds the binary64 product and then the threshold, while Ooples
evaluates the supplied binary64 parameter exactly. With a body of 5 and penetration
0.3, TA-Lib rounds the product to 1.5; the exact product is slightly smaller. A close
exactly at the rounded threshold can therefore produce different strict signals.
A separate fixed-grid integer oracle verifies Ooples; an explicit rounded-operation
oracle verifies TA-Lib. The shared verifier checks every value, repeat invocation,
and buffer against the appropriate independent reference. Regression tests pin
both signals and prove that other output corruption still fails verification.

HikkakeCandle detects an inside candle followed by a strict directional high/low
breakout. ModifiedHikkakeCandle requires two inside candles and an inclusive near
close on the first inside candle, using one fifth of its preceding mean range.
Both return signed 100 for formation and signed 200 for strict confirmation within
the following three bars. Confirmation occurs once; a new formation takes priority
and replaces pending confirmation. Formations are tracked during the three bars
before visible output, so the first visible value can be a confirmation. Default
lookbacks are five and ten; the modified near period is configurable. State uses
bounded confirmation age rather than an ever-growing bar index.

RisingFallingThreeMethodsCandle requires long first/final bodies surrounding three
strictly short opposite bodies that overlap the first candle's range. Reaction
closes move against the initial direction; the final open and close strictly
resume it. Partial overlap is sufficient. MatHoldCandle requires a white long
first body, a gapped black short second body, two declining short reaction bodies
inside the permitted penetration, then a white close above all reaction highs.
The third/fourth colors are unrestricted, and the final body need not be long;
a final doji can qualify. Both have independent long/short windows and a default
lookback of fourteen. Mat Hold accepts finite nonnegative penetration, defaulting
to 0.5. It also has a pinned exact-versus-rounded boundary: with a first body of 5
and penetration 0.1, a reaction minimum at 6.5 can pass exact arithmetic and fail
TA-Lib's rounded threshold. Both outputs have independent references.

## Verification and performance

```sh
dotnet test benchmarks/OoplesFinance.StockIndicators.CompetitorTests -c Release -p:GeneratePackageOnBuild=false --filter "FullyQualifiedName!~FullTrajectoryMatchesIndependentFormula"
# Run the shard CLI below for SHARD=0 through 19 concurrently, then check the union.
dotnet benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll --verify-library-shard SHARD 20 results/shard-SHARD.json
python scripts/verify-competitor-shards.py benchmarks/competitor-library-manifest.json results 20
dotnet run --project benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks -c Release -- --filter '*LibraryPairBenchmarks*'
```

Sparse outputs carry explicit presence flags. A missing result requires both a
false flag and a NaN placeholder; an unmasked NaN remains a failure. Presence
flags receive the same retained-buffer and poisoning checks as numeric arrays.
Only present mature numbers contribute to the reported checked-value count.

Independent formulas check every mature value and named output, returned-buffer
isolation, repeated execution, adversarial fixtures, and both actual benchmark
inputs (1,000 and 10,000 bars). The permanent workflow runs on stacked PRs, builds
once, checks the API inventory, and discovers contract methods from the compiled test
assembly. Contracts and full trajectories each run across up to 20 parallel shards.
Each contract shard requires passed TRX evidence for every assigned method; failed,
skipped, missing, and unexpected methods fail the job. The final gate requires all
contract shards and the exact complete trajectory union before performance starts.

Separate BenchmarkDotNet ShortRun jobs reuse those binaries across up to 20
parallel shards. Both arms at both sizes require at least three timing samples;
missing or failed cases fail CI. Benchmark setup checks both timed delegates once against the reference; buffer
isolation and repeated invocation remain fully checked by correctness shards.
Inputs are prepared outside measurement; public
runtime work and returned output allocation are included. Initial measurements
need longer follow-up before precise performance claims.

1,036 measured timing cases across 259 pairs are committed under
`results/2026-10-06-library-comparisons`. Raw reports retain observed
differences without universal speedup claims. Every currently paired family has
recorded timing evidence.

The eight percentile candles classify body or shadow lengths using a current-inclusive
window and linearly interpolated quantiles. Public constructors honor period and
percentile parameters. Production compares exact ranks, while the independent
contract reference sorts rational lengths and interpolates the threshold.

## Competitor limitations and remaining work

Executable tests confirm that 15 Trady candle APIs throw NotImplementedException.
Thirteen now link to independently verified TA-Lib counterparts: LongLeggedDoji,
Marubozu, SpinningTop, Hammer, HangingMan, InvertedHammer, ShootingStar, HaramiCross, StickSandwich, PiercingLine, UpsideGapTwoCrows, ThreeBlackCrows, and ThreeWhiteSoldiers.
ShortShadow and Stars now have the explicitly owned Ooples definitions below.
Throwing stubs have no native formula or timing comparison; the manifest
distinguishes alternate package mappings from owned definitions. Executable
package-availability tests keep these limitations checked.

Trady percentile candles recompute the whole percentile series for each candle.
Their full 10,000-bar verification is expensive. Use parallel shards without an
additional serial trajectory replay. CI allows 20 minutes per correctness shard
and 60 per performance shard, and prints per-pair progress and elapsed times.

Trady BearishLongDay, BullishShortDay, and BearishShortDay ignore their period and
percentile constructor arguments. Executable tests pin this behavior; comparison
runners use the effective 20-bar default configuration for Ooples and the reference.
Trady LongLowerShadow actually checks strictly below the 25th percentile, so its
counterpart is explicitly named LowerShadowBelowPercentileCandle. All these
limitations appear in the manifest. LongDay, ShortDay, BullishLongDay, and both
shadow patterns honor their parameters and are compared at multiple periods.

Implement every remaining mapping, including all candle families, and collect all
paired performance evidence before marking this full-library PR complete.

Trady Tasuki gaps use a high/low trend ending at the middle candle. Upside
requires a final open strictly inside the preceding body but allows its close to
cross the entire gap. Downside requires a close strictly inside the full-range
gap and does not restrict the final open. Trady ignores sizeThreshold for both;
object, generic, and tuple tests pin these asymmetric definitions.

Trady abandoned babies combine a high/low trend, two percentile-long outer bodies,
a strict body/range doji, and strict full-range isolation gaps. The bullish body
honors the requested window and percentile; the bearish body uses Trady’s fixed
20-bar 0.75 percentile. Both use the current candle in their percentile windows.
No midpoint penetration test is part of this definition.

Trady Three Methods searches backward for the newest rejection barrier or long
anchor, rather than requiring five candles. The Ooples state retains that anchor
and its following candle; an independent backward scan verifies the equivalent
result. Falling long bodies use Trady’s fixed 20-bar 0.75 percentile; short bodies
honor the requested settings. Overlapping short/long percentiles preserve
rejection precedence.

`LibraryPairBenchmarks` fixes its in-process job to a bounded 30-minute execution
timeout per case. CI uses this configuration without overriding the toolchain.
Timing jobs use one invocation per iteration and an unroll factor of one for
both arms, retaining three warmup and three measured ShortRun iterations. This
bounds repeated calls to competitors with expensive implementations; recorded
samples and confidence intervals retain the resulting timing uncertainty.

Trady trend-qualified stars use percentile outer bodies and a strict relative
first-body midpoint target. Separate independent references verify exact Ooples
and rounded native decimal comparisons. Zero midpoints return no match in
Ooples; native Trady can throw. The misspelled MoringinDojiStar tuple API is
covered separately.

## Explicit conventional definitions and additional contracts

`ConventionalStarCandle` and `ShortShadowsCandle` are Ooples definitions for the
two remaining Trady stubs. The star requires a preceding body at least half its
range, a current body at most one quarter its range, and a strict body gap in
either direction. Both ranges must be positive; shadows may overlap.
ShortShadows requires both shadows to be at most one tenth of a positive range.
Equality qualifies at the size thresholds. Independent rational references,
golden boundaries, extreme/subnormal prices, and lifecycle checks verify both.
No native parity or timings are claimed for these unimplemented Trady APIs.

`PriceCandlePattern` preserves Skender's price-relative Doji and range-relative
Marubozu definitions, including zero-range fractions of one and bearish
Marubozu signals on zero-range candles. Its exact supplied-price/percentage
comparisons can differ from Skender's rounded decimal/binary ratios; separate
references and a pinned boundary regression verify each contract. Price
presence is explicit. Native metadata/sorting is tested; timed inputs use the
same preordered bars for both libraries.

`WindowExtremeIndex` chooses the newest exact tie and reports absolute indices.
`WindowRangeMidpoint` supports close extrema or high/low extrema and computes
their mean without overflowing the intermediate sum. Both grow storage lazily.
Ooples supports period one; the pinned TA-Lib functions reject it. Native
midpoint overflow is pinned independently from the finite Ooples result.

`BalanceOfPowerWithValidity` uses a simple average of complete valid windows.
A zero-range candle makes the value missing until it leaves the window.
TA-Lib's raw formula uses zero for that candle and reuses the existing public
`BalanceOfPower` implementation. Both definitions have independent trajectories.

Accumulation/distribution now covers TA-Lib, Skender, and Trady. Skender includes
the multiplier, flow volume, cumulative line, and optional simple average with
explicit presence. Trady starts with the first volume, adds price-change-based
flow on flat candles, and stays missing after an undefined flat-candle ratio.
Its counterpart is `PriceAdjustedAccumulationDistribution`; the ordinary detailed
line is `AccumulationDistributionWithAverage`. TA-Lib reuses `Adl`.

These timing fixtures use a common 1/1024 price grid, exactly representable in
both decimal and binary64. Unrounded generated workloads remain correctness
stress tests. Separate integer-grid oracles model exact Ooples arithmetic,
native binary64 stage rounding, and decimal package inputs. A pinned one-ULP
price-range fixture demonstrates input collapse during decimal conversion;
corruption tests ensure these separate contracts cannot hide wrong outputs.

`RollingPercentile` covers QuanTAlib and Trady median and interpolated percentile.
Ranks run from zero to one, using rank = percentile*(window count-1). Exact
binary64 rank and convex interpolation avoid overflowing opposite-sign differences
or same-sign sums. Its optional startup average matches QuanTAlib's incomplete
windows; Trady comparisons start at the first complete window. Endpoint and
interior ranks, odd/even windows, duplicate expiration, subnormal rounding,
lifecycle, lazy large periods, and Trady generic/tuple entry points are verified.
Separate references retain native binary64 rounding and decimal input conversion.
QuanTAlib Percentile rejects period one; the Ooples counterpart supports it.

`WilderMovingAverage` covers Skender/QuanTAlib Smma and Trady ModifiedMovingAverage.
Its coefficient is 1/period. The default seed uses the first full-window mean,
with expanding means during startup; its first-value option begins the recurrence
immediately. Each convex update is rounded once and preserves finite extreme and
subnormal inputs. Independent references verify both seeds and package rounding;
period one, maximum periods, lifecycle, and Trady generic/tuple routes are covered.
QuanTAlib Rma remains pending: its pinned implementation uses a divergent recurrence,
so it must not be treated as another name for this conventional Wilder average.

`SeededAverageTrueRange` covers ordinary TA-Lib and Trady ATR.
`AverageTrueRangeWithDetails` supplies all three Skender outputs and presence flags.
The initial candle only establishes previous close; the first average is the mean
of true ranges 1..period. Each later update uses Wilder smoothing. Unpublished
ranges retain extended upper exponents until averaging, so an oversized range can
still produce a finite scalar ATR. Detailed outputs reject unrepresentable published
true ranges or percentages. Percentage is signed 100*ATR/close, absent only when
close is zero or the average is not ready. Later nonzero closes recover normally.
Independent references cover exact Ooples stages and native binary64/decimal formulas.
Skender rejects period one; the Ooples, TA-Lib, and Trady scalar forms accept it.

TA-Lib Natr is covered by the separate normalized comparison below. QuanTAlib Atr has a distinct bar-route defect documented below.

QuanTAlib 1.0.0 `Atr.Calc(TBar)` does not update inherited `Input.IsNew`, yet passes
that stale false flag to its internal EMA. The EMA restores zero state for every
bar. Its normal bar stream therefore returns `(1/period)*trueRange`, including
high-low on the first candle. Executable direct and event-source regressions pin
this behavior; `ScaledTrueRange` is its explicitly named counterpart. This is not
temporal smoothing. Conventional seeded Wilder ATR is available separately.
Manually setting the package's inherited Input property can change its behavior;
the comparison exercises the normal documented bar route without such intervention.
The scaled counterpart divides the exact range before final rounding, preserving
finite results across overflowing intermediate ranges. Independent references
model this contract and the native binary64 multiply/divide stages separately.

`NormalizedSeededAverageTrueRange` computes signed 100*seededATR/close, with zero
for zero-close bars and during startup. It always normalizes period one and never
revises earlier results. Extended unpublished ranges and averages allow finite
percentages even when raw ATR cannot be represented. The TA-Lib Natr comparison
uses separate native and conventional references: native period one returns raw
true range, and later zero closes overwrite the first output while leaving the
current slot untouched. The adapter explicitly zero-initializes its output buffer;
a sentinel regression also exposes that unwritten slot. Prefix-stability and
corruption tests prevent those differences from hiding unrelated errors. Timings
use ordinary positive-price workloads where both implement normalized ATR.

`WindowLinearRegression` covers TA-Lib LinearReg, LinearRegSlope,
LinearRegIntercept, and Tsf, plus Skender Epma. It selects one independently rounded
endpoint, slope, window-local intercept, or one-step forecast from the existing
exact fit engine. The new local intercept uses x=0 at the oldest candle; the
existing LinReg global-series intercept remains unchanged. Only the selected
reading is published, so an unrepresentable slope cannot invalidate a finite
endpoint. Independent centered rational and batch integer-grid oracles cover
startup, window expiration, local coordinates, extreme/subnormal inputs, lazy
maximum periods, lifecycle, and corruption. These native APIs require at least
two observations per window; the Ooples counterpart also defines period one.

`NormalizedConvolution` covers QuanTAlib Convolution with immutable copied,
newest-first weights. Each available prefix is normalized by its exact mass;
zero mass divides the signed weighted response by available count. This does not
replace the weights with equal weights. `EndpointWeightedAverage` covers QuanTAlib
Epma using fixed 2*period-1-3*lag weights, including startup. Only the complete
window equals a regression endpoint; partial windows differ from partial OLS.
Independent references model exact Ooples ratios and native coefficient and
normalization rounding, including nearly cancelled kernel mass. Signed/zero
kernels, direction, copied settings, extreme/subnormal ratios, lifecycle, lazy
maximum endpoint periods, and corruption are verified.

`EndpointWeightedAverage(period, true)` also covers QuanTAlib Mma's expanding-mean
startup. At a complete window its weighted-moment formula equals the regression
endpoint, not Wilder smoothing. Both endpoint startup modes now use exact rolling
moments, avoiding repeated window scans. Independent weighted references retain
coverage of every original fixed-weight configuration. A full 46,341-bar native
regression proves the package's Int32 denominator overflow reverses the correction;
the Ooples endpoint remains correct. Period one is supported by Ooples but rejected
by native Mma. Both Mma and Epma were remeasured after the implementation change.

`WindowRegressionStatistics` supplies all five QuanTAlib Slope readings: slope,
one-based window-local intercept, population standard deviation, R-squared, and
the fitted endpoint, each with a presence flag. Available history is used during
startup. Only slope exists on the first observation. R-squared is undefined on a
flat window; QuanTAlib instead retains its previous R-squared, and its independent
comparison reference explicitly models that difference. Exact rolling moments
preserve variance and correlation for subnormal inputs and avoid intermediate
overflow. The production validation reference uses centered rational regression;
the competitor comparison uses a separate decimal batch calculation.

`RegressionSnapshot.Calculate` supplies Skender Slope's complete batch output.
Intercepts use global one-based coordinates. Its Line field overlays the final
window's fit on the final period rows, including rows whose own rolling statistics
are still in startup. Appending input changes that overlay. This retrospective
contract is separate from the causal window-local indicator. Flat-window
R-squared is absent. Unrepresentable published statistics are rejected; oversized
unpublished startup readings do not invalidate later finite results.

`WindowLinearRegression` also selects slope angle in degrees for TA-Lib
LinearRegAngle. It applies arctangent to the rounded slope. An oversized unpublished
slope still produces a finite signed 90-degree angle; period-one angle is zero.

`WindowDispersion` selects variance, standard deviation, or the latest close's
Z-score from the current window's own mean. It supports sample (count-1) and
population (count) denominators, available-history startup, and a signed finite
multiplier applied before final rounding. One observation and constant-window
Z-scores are zero. Exact rolling moments preserve tiny spreads; normalization and
scaling happen before any potentially overflowing or underflowing publication.
History grows lazily, including maximum-period configurations.

QuanTAlib Variance and Stddev default to sample statistics and also offer population
mode. Zscore always uses sample deviation. TA-Lib Var/StdDev and Trady
StandardDeviation use population statistics with full-window startup. TA-Lib's
uncentered mean(x*x)-mean(x)^2 arithmetic can lose the variance of a large-offset
series entirely; its comparison has a separate staged binary64 reference, while
Ooples is checked against an independent centered reference. TA-Lib Var accepts
period one, StdDev requires two, and both reject one-bar inputs at their range
validator. QuanTAlib requires at least two; Trady and Ooples support period one.
Trady's object, generic, and tuple routes are compared on finite inputs. Its
nullable decimal values and output conversion retain their native startup rules.

`StandardDeviationWithDetails` covers all four Skender StdDev outputs and their
presence flags: population deviation, mean, Z-score, and optional deviation SMA.
The price window must be complete; smoothing then requires its own full window
of published deviations. Disabled smoothing stays absent, and a period of one
reproduces the deviation output. Both histories grow lazily without adding their
periods in an integer counter. Exact zero variance makes Z-score absent; nonzero
subnormal variance remains defined even if the published deviation rounds to zero.

Skender's floating mean can create a nonzero deviation and defined Z-score on a
constant nonbinary series such as 0.1. Decimal quote conversion can instead collapse
distinct binary64 prices into a constant series. Separate exact-grid and staged
native references cover both differences; the independent square-root oracle
certifies its estimate against exact adjacent-double midpoints. Skender also
throws OverflowException when its checked lookback+smoothing addition overflows,
even when the input is too short to produce any complete window. The Ooples counterpart uses actual window counts.

`WindowMeanAbsoluteDeviation` and `MovingAverageDiagnostics` cover TA-Lib AvgDev
and Skender SMA analysis. The detailed output includes the mean, absolute error,
squared error, signed relative absolute error, and four presence flags. Errors
are centered on the published rounded mean: for prices 2^53 and 2^53+2, the
published mean is 2^53 and squared error is 2, rather than population variance 1.
Relative error averages abs(price-mean)/price with signed denominators and no
factor of 100. Zero prices make only relative error absent, with recovery after
the zero expires. Exact intermediate ratios retain finite cancellation even when
individual terms exceed binary64 range; unrepresentable published outputs are
rejected. Scalar absolute deviation does not compute unselected error outputs.
Both histories grow lazily, including maximum periods. The scalar publishes
available history; detailed outputs require a full window. TA-Lib requires period
two, while the Ooples counterparts and Skender support one. Separate native
references retain summation order and decimal quote conversion; finite quote,
tuple, and chained Skender routes receive matching field checks.

`PriorPriceChannel` supplies the full preceding-window channel for Skender
Donchian: upper, lower, center, width, and explicit presence for each field. The
current candle is excluded, and the first reading follows a complete preceding
window. Width divides the range by the exact midpoint before rounding; an exact
zero midpoint makes width absent, while an underflowed published midpoint does
not. True negative extrema are preserved. Skender initializes its highest price
to zero, clamping all-negative high windows; its independent native reference
retains that defect and its decimal arithmetic. Both native behavior and the
Ooples formula have direct regressions.
Skender's decimal midpoint addition also overflows on constant decimal.MaxValue
prices even though the midpoint itself is representable; a direct boundary test
pins that limitation.

`WindowPriceRange` supplies Trady's highest-high minus lowest-low difference,
with available-history startup aligned to Trady's first complete window. All
three native object/generic/tuple routes are checked. Trady returns missing values
for nonpositive periods, while Ooples rejects them. Both new public counterparts
grow storage lazily, and the runtime rejects unrepresentable final outputs.

Skender Williams %R and TA-Lib WillR use the existing exact `WilliamsR` public
counterpart. Skender computes stochastic minus 100 and returns -100 for flat
windows. TA-Lib divides by a range already scaled by -100 and returns zero when
that denominator is zero, including after underflow. Its overflowing range can
also turn a finite result into zero. Separate staged native references retain
these differences, and direct flat/subnormal/overflow tests verify them. Ooples
preserves the complete ratio before rounding and returns -100 on flat ranges.
TA-Lib requires period two; Skender and Ooples support period one.

`WindowAroon` covers five Aroon families with explicit window and equal-extreme
conventions. TA-Lib uses period+1 current-inclusive candles and the newest equal
extreme; Skender uses that window with the oldest equal extreme; Trady uses
period candles with the newest equal extreme. All three Ooples values and their
presence flags require a complete window. Percentages and oscillator are rounded
independently from exact ratios, and the period+1 size uses a wide counter so
maximum periods do not overflow or allocate eagerly.

The Skender maximum search starts at price zero and an index before the series,
giving invalid negative Up values on nonpositive high windows. Its oracle retains
that defect, while Ooples retains the true extrema. Skender's oscillator subtracts
published binary64 components, Trady subtracts published decimal components,
and TA-Lib multiplies an already rounded factor by the index difference. Separate
references and regressions pin these differences. Native Trady object/tuple
routes and its public generic oscillator route are verified. TA-Lib requires
period two; Skender, Trady, and Ooples support period one.

`VolumeWeightedPrice` covers Skender VWAP/VWMA and Trady VWAP. It supports
cumulative history or a complete rolling window, typical price or close, and an
inclusive date cutoff. Exact price-volume products and total volume are retained
until the final ratio; typical price is not rounded before weighting. Zero total
volume is explicitly absent and can recover. Rolling history grows lazily;
cumulative mode uses constant storage. Skender rejects an anchor before the first
nonempty quote series but accepts a future anchor with absent outputs. Ooples
treats earlier anchors as including all history. Trady throws for zero total
volume and can overflow its decimal products despite a finite weighted price.
Its cumulative and rolling modes and both object/tuple routes are verified.

`MoneyFlowWithDetails` supplies all three Skender CMF fields: multiplier, flow,
and an unscaled, unclipped CMF ratio. Multiplier and complete flow are rounded
separately. CMF uses the exact sum of those published flows divided by exact total
volume; it does not use a sum of unrounded theoretical flows. Zero range yields
zero flow, and zero volume makes only CMF absent. Native binary64 stages and
decimal quote conversion have separate references; collapse of small ranges or
volumes is pinned by direct regressions. Wide and subnormal sums normalize before
rounding. All published fields, presence, and final-overflow rejection are checked.

`SeededForceIndex` supplies Skender's SMA-seeded EMA of volume-weighted price
changes. The seed requires Period changes after the first candle. Raw products
and seed sums remain exact even when individually unrepresentable, and each
published recurrence is rounded once. Skender's staged recurrence can lose a small
change even at period one; its checked period+1 also overflows at int.MaxValue.
Ooples uses a wide denominator and constant storage, with boundary regressions.

`VolumeConditionedIndex` supplies Trady's positive/negative volume indices, seeded
at 100. Only strict volume changes select a price-ratio update. The previous-price
denominator retains its sign, and held readings still advance the previous price.
Ooples rounds the complete index-times-price ratio once; Trady's decimal quote
conversion can turn strict volume changes into ties, and its price difference
can overflow before a finite index. A selected zero previous close throws in
Trady and makes the Ooples index absent until reset. Native object/generic/tuple
routes, exact signed/subnormal/overflow cases, and both arms' corruption checks
are covered.

`ReverseWilderAverage` is the explicit counterpart of QuanTAlib 1.0.0 Rma's
implemented recurrence. Despite the native Wilder-smoothing documentation, its
post-seed update is previous+(previous-close)/period: it extrapolates away from
the new price and can diverge even on bounded inputs. Conventional smoothing is
available through `WilderMovingAverage`. The reverse indicator's seed uses an
expanding mean of the previously rounded value, rather than an exact prefix sum.
Each complete owned update rounds once; the native staged arithmetic has a
separate oracle. Constant storage, extreme/subnormal values, period-one behavior,
source subscription, last-value revision/reset, and overflow rejection are tested.
Native period*2 warmup metadata wraps at maximum periods; the owned implementation
avoids that arithmetic.

`CandleArithmetic` covers TA-Lib Add, Sub, Mult, and Div with independent selection
of each OHLC operand. Comparisons use close on the left and open on the right.
`PriceRoundingTransform` covers Ceil, Floor, and Sqrt on close. These stateless
operations have no warmup. Rational production references and independent
integer-grid arithmetic, integer quotient rounding, and certified square-root
midpoints check complete outputs, including subnormal values.

Division by zero and the real square root of a negative input are absent in
Ooples and recover on the next admissible input. TA-Lib publishes infinity or
NaN for those requests; the adapter preserves these as invalid present values,
so the comparison verifier cannot silently accept them as missing. Final
arithmetic overflow is rejected by the owned runtime. Timings use finite,
defined inputs. TA-Lib rejects empty or one-element spans, whereas the owned
indicators support a single candle. Float/double and in-place routes, inclusive
requested range ends versus exclusive returned ends, short output buffers,
field selection, chaining, and corrupted values/presence have direct regressions.

`FixedPeriodWma` matches QuanTAlib's available-prefix weights: the newest close
has weight Period, its predecessor Period-1, and so on. Ordinary expanding WMA
uses different startup weights. The updated Quan WMA comparison retains all
startup values; its earlier warmup-skipping timing report is replaced. DWMA
composes two `FixedPeriodWma` instances through `Of`, rounding the intermediate
series once. Exact running sums support constant-work updates and lazy storage.
Native rounded kernels, prefix normalization, and dot products have a separate
reference, including the unchecked period-product wrap at 65,536. Quan Wma hides
`Init`, leaving convolution history intact through the public reset route and
inside Dwma; source subscription, revision, and reset regressions pin the behavior.

`WindowMode` matches QuanTAlib Mode's expanding-mean startup and full-window mean
of the most frequent distinct values. Exact ties keep adjacent doubles distinct.
Owned means round once and remain finite across overflowing native sums. Native
startup uses SIMD lane sums; mature tied modes are summed in ascending scalar
order. Independent references model both. Quan's inherited `Init` retains its
circular buffer even while resetting the index; the owned reset clears all
history. Maximum periods grow lazily. Tests cover ties and their expiration,
startup transitions, source/revision/reset routes, and corrupted outputs.

## Logarithmic, exponential, and hyperbolic transforms

`PriceTranscendentalTransform` supplies natural/common logarithms, exponential,
and hyperbolic sine/cosine/tangent. Each close is transformed independently and
both `Value` and `IsDefined` are published. Nonpositive logarithm arguments are
absent for that bar and recover on the next positive close. Other finite
arguments are mathematically defined; the runtime rejects final overflow.

Production uses runtime intrinsics. Validation uses independent 192-bit series;
the benchmark oracle uses a separate 256-bit Horner polynomial and logarithm
series. Every comparison uses zero absolute tolerance, relative tolerance
4e-15, and an exact sign check. This prevents tiny nonzero answers from being
accepted as zero. The budget is a test criterion, not a formal platform-wide
proof of libm accuracy.

The boundary fixtures cover adjacent values around one, subnormal logarithm
arguments, exponential underflow at -744/-745/-746, tiny signed hyperbolic
signals, and finite sinh/cosh at +/-710 even though exp(710) overflows. Native
nonfinite answers remain present in the adapter and cannot pass as missing
values. TA-Lib's empty/single-element span restriction, inclusive request versus
exclusive output range ends, float/double overloads, in-place aliases, invalid
ranges/lengths, and corrupted value/presence results have direct checks.

Performance inputs use a bounded varying walk, so tangent is not measured on an
artificially saturated constant. Both libraries return the complete output
trajectory. Undefined native requests are documented and tested separately from
the finite comparable timing workloads.

Validation: 42 focused checks passed (including shared elementary-math and inventory
regressions), followed by 71,713 complete-trajectory values across the six families.
All 24 new timing cases passed setup correctness and the archive verifier.

## Zero-seeded Laguerre and Elder-ray details

`ZeroSeedLaguerreFilter` supplies QuanTAlib Ltma's four-stage, zero-initialized
Laguerre recurrence. Gamma defaults to .1 and accepts finite values in [0,1].
Gamma zero gives the [1,2,2,1]/6 impulse response; gamma one remains zero. All
startup values are included. Complete stages round once with an extended upper
exponent; the final weighted mean rounds once. Seven gamma configurations,
including subnormal gamma and the adjacent value below one, are compared exactly
to separate owned and native rounding references. Native NaN gamma acceptance,
source subscription, revision, and reset behavior have explicit tests.

`ElderRayWithDetails` supplies Skender's EMA, BullPower, and BearPower, with a
shared presence flag. A complete SMA seeds the close EMA. The two powers subtract
the published EMA from high and low and retain their signs. Exact seed and
complete updates avoid intermediate overflow, use constant storage even for the
maximum period, and preserve subnormals. Quote conversion and native sequential
rounding have a separate exact oracle. Distinct OHLC fields, startup, period one,
all output/presence corruption, the native reusable combined value, and genuine
final power overflow are covered.

Validation: 21 focused checks, 52,624 full-trajectory values, and eight paired
timing cases passed. All 924 archived timing cases across 231 pairs pass the
permanent evidence verifier. This does not complete the remaining API families
or the final complete correctness union.

## Expanding regularized and zero-lag EMA

`ExpandingRegularizedEma` publishes two direct seed prices, then applies
alpha=2/(min(observations,period)+1) with lambda regularization. Period one
therefore retains regularization when lambda is nonzero. The complete update is
rounded once, including the normalization by 1+lambda. Finite lambda may exceed
one; nonfinite or negative values are rejected. State uses constant storage.

`ExpandingZeroLagEma` uses 2*close-laggedClose with lag floor((period-1)/2),
clamped to available history, and the same expanding alpha. The synthetic value
is not prematurely rounded or allowed to overflow before the final update.
History grows lazily even for maximum periods. Native QuanTAlib allocates a
full-period buffer, so maximum-period construction is intentionally not run.

Both comparisons require exact agreement with separate full-ratio and native
operator-stage references. Five lambda configurations, odd/even periods, all
startup values, available-lag transitions, wide/subnormal inputs, true final
overflow, and source/revision/reset behavior are covered. Native Rema accepts
NaN and positive-infinite lambda; direct checks retain that limitation.

Validation: 21 focused checks, 34,612 full-trajectory values, and eight timing
cases passed. The archive verifier accepts 932 cases across all 233 currently
paired families. The final complete correctness union remains outstanding.

## Curvature through public composition

QuanTAlib Curvature is compared with `WindowRegressionStatistics(period)` reading
`WindowLinearRegression(period, WindowRegressionOutput.Slope)` through `Of`.
Each first-stage slope is rounded before entering the second regression. Selecting
only the slope avoids evaluating unused first-stage outputs in the production
runtime. Both timing arms use prepared input data.

The comparison includes curvature, intercept, population standard deviation,
R-squared, the fitted endpoint, and all presence flags. Independent integer moments
and midpoint-certified roots verify our composed outputs exactly. A separate
reference models every native operator in both fits, including underflow and
retained R-squared values on flat windows. Our flat-window correlation is absent.

Native `Init` clears the outer history but retains the nested slope's history;
source and revision routes are tested separately. Native `IsHot` becomes true
after period samples although its warmup metadata is 2*period-1. Empty, single,
adjacent-double, subnormal, wide finite, and lazy maximum-period cases are covered.
All five outputs and their presence flags reject deliberate corruption.

Validation: ten focused checks, 63,145 complete-output values, and four timing
cases passed. No production files changed for this batch; verification reused
those binaries. The archive verifier accepts 936 cases across 234 pairs.

## Holt-Winter forecast

`HoltWinterForecast` maintains level, velocity, and acceleration, seeded with the
first close and two zeros. The published forecast is level+velocity+acceleration/2.
Each complete state update rounds once with an extended upper exponent, and the
forecast rounds once. A targeted test makes level and velocity exceed binary64
range and cancel to a finite zero; the native calculation becomes nonfinite.

Constructors support period defaults, three explicit finite factors, and the
native coefficient-only convention that truncates (2-levelFactor)/levelFactor to
a positive signed 32-bit period. Period one overrides factors with (1,0,0).
Factors outside [0,1] remain supported and may diverge. Nonfinite factors and
invalid derived periods are rejected. The default period divisor is evaluated
widely; pinned native int.MaxValue wraps its integer period+1 denominator.

Separate exact references model complete owned stages and individual native
operators. Six factor settings, three derived-period settings, maximum periods,
subnormal half-terms, extreme finite inputs, genuine final overflow, source,
revision, reset, and corrupted outputs are covered. Native nonfinite parameter
acceptance and overflowing period-one differences are retained as limitations.

Validation: 16 grouped focused checks plus one targeted extended-stage check,
28,112 complete-trajectory values, and four timing cases passed. The targeted
check reused unchanged production and benchmark binaries; timings were not
repeated. All 940 archived cases across 235 pairs pass the evidence verifier.

## Normalized window entropy

`NormalizedWindowEntropy` computes normalized Shannon entropy from exact close
frequencies over available rolling history. Its explicitly documented QuanTAlib
convention returns one for a single distinct value, including the first bar.
Signed zeros share a group; adjacent doubles remain distinct. Window storage grows
lazily, and the calculation is independent of close magnitudes. Compensated
logarithms preserve rare-event entropy in nearly constant windows.

Independent fixed-point logarithms and sorted frequency runs verify complete
outputs. Seven focused checks cover unequal frequencies and expiration, signed
zero/subnormal/maximum values, maximum-period lazy storage, native source,
revision, reset and hot flags, and corrupted values/presence on both arms.
The trajectory run checked 13,808 values. Four timing cases are archived; all
944 cases across 236 paired families pass the performance evidence verifier.

## Gaussian and sine weighted windows

`GaussianWeightedAverage` and `SineWeightedAverage` pair QuanTAlib Gma and
Sinema with fixed newest-first kernels and available-prefix normalization.
Gaussian here means Gaussian coefficients, not the geometric mean. Coefficients
use documented binary64 elementary operations; each complete weighted ratio
rounds once. Owned weights and history grow lazily, including maximum periods.
Gaussian rejects its undefined period-one case and zero/nonfinite width; finite
negative widths are equivalent to positive widths. Tiny widths avoid the native
zero-denominator NaNs; prefixes whose weights all underflow publish zero.

The independent oracle models the native full-kernel normalization followed by
prefix normalization separately from owned unnormalized coefficients. Fixed-point
sine and exponential series check the coefficients; integer-grid arithmetic checks
weighted responses. Both native wrappers retain their nested convolution history
through public Init, which is explicitly tested and documented.

All 32 focused/shared convolution checks passed, including lifecycle, source,
revision/reset, odd/even and unit periods, width variants, extreme/subnormal
prices, lazy maximum periods and corrupted coefficients/outputs. The two complete
trajectory runs checked 32,906 values. Eight timing cases are archived; all 952
cases across 238 pairs pass the evidence verifier.

## Arnaud Legoux startup conventions

`ArnaudLegouxWindow` pairs Skender's full-window-only ALMA and QuanTAlib's
available-length ALMA. Exact squared-distance differences shift Gaussian
logweights to a zero maximum before rounding exponents and applying binary64
exp; the final weighted ratio rounds once. At least one weight remains one,
avoiding all-weight underflow. Exact center ties are preserved even at extreme
widths. Finite out-of-range offsets and zero/negative sigma cover Quan's broader
parameter conventions; zero sigma gives the uniform mean. History grows lazily.

Independent references model native unshifted coefficients and staged arithmetic
separately. Skender's final NaN is absent; Quan's remains present and is rejected
by the correctness verifier, with that rejection explicitly regression-tested.
This native extreme-width limitation is not treated as a successful finite pair.
Quote conversion, tuple/reusable routes, source, revision/reset, both startup
rules, tiny/wide prices and lazy maximum periods are covered. Sixteen focused
checks passed and complete trajectories checked 44,457 values. Eight timing
cases are archived; all 960 cases across 240 pairs pass the evidence verifier.

## Hull period and startup conventions

`HullWindow` pairs QuanTAlib's immediate fixed-period prefixes, Skender's
full-window floor periods, and Trady's full-window nearest periods (including
even half-period ties). Each stage rounds once. Unpublished synthetic values
keep binary64 precision with an extended upper exponent, preserving finite final
outputs after oversized intermediate values. Final overflow remains an error.
Lazy rolling moments give constant update work without eager period allocation.

Native stage rounding is modeled independently, including Quan's twice-normalized
convolution weights, Skender's per-term binary64 division and Trady's decimal
per-term division. Quan's reset retains nested histories; overflowing synthetic
input invokes its convolution fallback. Trady's degenerate periods zero/one
produce zero/negative-price outputs, while the owned indicator requires period
at least two. Trady nullable closes contribute zero with fixed weight divisors;
callers comparing such inputs must map those missing closes to zero explicitly.

Twenty-five focused/shared checks passed, covering lifecycle, all three period
rules, quote/tuple/reusable/mapper/range routes, source/revision/reset, extreme and
subnormal prices, lazy maximum periods, and value/presence/convention corruption.
Complete trajectories checked 41,170 values. Twelve timing cases are archived;
all 972 cases across 243 pairs pass the performance evidence verifier.

## Moving-average percentage envelopes

`WindowAverageEnvelope` covers all nine Skender modes: ALMA, DEMA, EMA, EPMA,
HMA, SMA, SMMA, TEMA and WMA. Existing average states feed centerline, upper and
lower outputs with shared explicit presence. Upper and lower formulas round once
from the published centerline, preserving near-100-percent residuals. Negative
centers retain the native labels without sorting. Histories grow lazily and
chaining feeds the selected average. Nonfinite percentages are rejected; native
NaN/infinity validation holes remain documented limitations.

Independent references model each centerline and native staged percentage
arithmetic. Sixty-three focused/shared checks passed, including lifecycle,
quote/tuple/reusable routes, minimum periods, tiny/wide values, large percentages,
lazy maximum periods and every output's value/presence corruption. Complete
trajectories across all nine modes and width variants checked 184,914 values.
Four envelope timing cases use the default SMA mode and 2.5 percent; individual
averaging families have separate timing evidence. All 976 archived cases across
244 pairs pass the performance evidence verifier.

## Circular and inverse circular transforms

`PriceCircularTransform` covers sine, cosine, tangent and their inverse functions
in radians. It exposes value and domain presence; inverse sine/cosine outside
[-1,1] produce absent values and recover on the next defined input. Native NaNs
remain present and fail comparison rather than being silently masked.

Validation uses a 2,560-bit Machin pi identity, nearest-quadrant reduction and
forward series. The benchmark oracle independently uses a 3,072-bit two-angle
identity, modular reduction and backwards polynomials. Both retain tiny phase
residuals and verify zero absolute / 4e-15 relative tolerance with exact sign.
Eleven focused checks passed, including maximum finite magnitudes, exponent
sweeps, binary64 pole neighbors, subnormals, inverse endpoints, lifecycle,
chaining, float/double aliases and ranges, and value/presence corruption.
Complete trajectories checked 71,734 values. All 24 new timing arms passed;
the archived union now contains 1,000 measured cases across 250 pairs.

## Awesome oscillator with normalized output

`AwesomeWithDetails` publishes the full-window midpoint SMA difference and
100*oscillator/currentMidpoint, with independent presence flags. Both wait for
the slow window; a zero midpoint leaves only the normalized field absent.
Midpoints, averages, differences and normalized ratios round separately without
unpublished overflow. Chaining explicitly uses the upstream value as the price,
because the builder replaces close while preserving the original high/low.
History grows lazily, including maximum-period startup.

Independent exact references distinguish owned arithmetic from native decimal
midpoint conversion and staged operators. Nine focused checks passed, including
quote/tuple/reusable routes, startup/domain flags, source chaining, lifecycle,
subnormal and maximum values, genuine final overflow and every output's corruption.
Four period configurations checked 28,082 trajectory values. Four new timing
cases passed; the archived union is 1,004 cases across 251 pairs. Native decimal
HL2 addition can overflow before averaging; that limitation is explicitly tested.

## Delayed Alligator lines and Gator distances

`AlligatorWithDetails` uses three SMA-seeded Wilder midpoint averages, each
published after its positive delay with independent presence. No future rows are
appended. `GatorWithDetails` reports absolute jaw/teeth distance, negative absolute
teeth/lips distance, and both expansion decisions with independent presence.
Expansion uses strict published-value comparisons. Each decision waits for a
present previous distance; a missing current distance then reports false.
`GatorWithDetails.FromLines` also accepts supplied nullable Alligator samples,
retains their enumeration order and timestamps, and starts fresh on enumeration.

Midpoints and complete recurrence updates round once without intermediate
overflow. Delay queues grow lazily. Chaining uses upstream prices as midpoints.
Parameter-order checks use wide sums; Skender instead throws OverflowException
when its int period-plus-offset addition overflows. Native custom Gator can also
publish infinite distances, which the owned API rejects.

Sixteen focused and ten shared Wilder checks passed. Independent exact native
and owned references checked 95,069 trajectory values across three configurations.
Coverage includes quote/tuple/reusable/custom-line routes, staggered startup,
missing values, exact ties, tiny and maximum values, maximum periods/delays,
chaining/lifecycle and every output/presence corruption. Eight timing cases
passed; all 1,012 archived cases across 253 pairs pass the evidence verifier.

## Heikin-Ashi complete OHLCV candles

`HeikinAshiCandles` publishes open, high, low, close and unchanged volume from the
first candle. Close is the once-rounded full OHLC mean; initial open averages raw
open/close, then follows the mean of the previous published open and close.
High and low include both transformed values. Close is the primary output for
chaining. Incoming chaining replaces raw close while retaining other fields.
All means remain finite for finite inputs without overflowing intermediate sums.

Six focused checks and 64,065 full-output values passed independent grid and
native decimal-stage references. Regressions include exact seed/recurrence,
subnormal rounding, maximum magnitudes, lifecycle, incoming/outgoing chaining,
sorted generic quotes, repeated candle transforms and every field/presence
corruption. Native decimal quote conversion erases binary64 subnormals and native
decimal sums can overflow before averaging; neither limitation is hidden.
Four timing cases use distinct OHLCV fields. The archive now has 1,016 verified
cases across 254 pairs.

## Rolling Chande momentum and efficiency ratio

`WindowPathRatio` covers Skender CMO as 100 times signed net movement divided by
rolling path length, and Trady EfficiencyRatio as absolute net movement divided
by the same path length. It waits for a full period of changes. Flat paths are
absent; zero net movement along a nonflat path is a present zero. Exact differences,
path sums and a once-rounded ratio preserve subnormals and movements wider than
binary64. History grows lazily, including maximum-period startup.

`FromValues` handles nullable Trady inputs: both window endpoints must exist,
and unknown adjacent steps contribute nothing to path length. This can produce
an efficiency above one. Enumeration is independent and repeatable; nonfinite
inputs and unrepresentable final ratios are rejected. Native period zero yields
null, while negative periods fail on evaluation; the owned API requires a positive
period. Trady's params-index overload returns one value; the nullable start/end
and IEnumerable-index overloads return sequences, all explicitly checked.

Fifteen focused checks passed, including quote/tuple/reusable/generic routes,
nullable gaps, window expiration, flat recovery, exact tiny movements, extremes,
source chaining, lifecycle and value/presence corruption. Independent references
checked 23,200 full-output values. Native decimal arithmetic and staged binary64
CMO arithmetic have separate references; native Skender overflowing ratios can
become absent. Eight paired timing cases passed. All 1,024 archived cases across
256 pairs pass the performance evidence verifier.

## Wilder RSI and Chande strength

`WilderStrengthOscillator` covers TA-Lib default RSI/CMO and Skender RSI. It seeds
from a full period of exact changes, then rounds each complete gain/loss update
to 53 significant binary digits. The unpublished means have extended exponents
in both directions, preserving tiny ratios and avoiding overflowing movements.
The bounded final percentage rounds once to binary64. TA flat values are zero;
Skender RSI flat values are 100. Optional unstable periods suppress initial
outputs without changing the recurrence. State and alignment storage are bounded.

Positive dyadic updates retain up to 4,096 alignment bits. The dominant term is
always exact within that bound; a discarded subordinate positive tail is retained
as a sticky bit for midpoint rounding. Independent rational and normalized-ratio
references verify this arithmetic. Fourteen focused tests passed, including
maximum periods/suppression, tiny and huge movements, lifecycle/source routes,
5,000-bar decay, and a 12,000-bar decay followed by a midpoint-sensitive update.
Native double/float, aliases, ranges, invalid buffers, settings and output/presence
corruption are checked with isolated global settings. Complete comparisons
checked 35,830 values; all 12 new timing cases passed.

TA-Lib 0.5.0 Metastock RSI/CMO has a verified compatibility defect: at exactly one
period of input it reads beyond the array; on longer inputs it labels a value one
bar before the data used and returns a shortened range. Independent native
references and direct boundary/future-input regressions pin this behavior. The
owned streaming contract is causal and does not claim parity with this defective
mode. Native gain/loss means can also underflow, and overflowing differences can
produce NaN. These remain explicit limitations. All 1,036 archived timing cases
across 259 pairs pass the performance evidence verifier.

## Nullable strength and lagged momentum

`NullableStrengthOscillator` covers Trady RelativeStrength, RelativeStrengthIndex,
NetMomentumOscillator, RelativeMomentum and RelativeMomentumIndex. Strength uses
Wilder smoothing of one-bar changes; momentum uses EMA smoothing of lagged changes.
All modes omit zero-loss results; Relative Momentum Index also omits zero gain.
The public nullable route skips missing changes while seeding; an empty seed or
any missing change after seeding leaves subsequent results absent. Lag storage
grows lazily, including maximum-period startup.

Unpublished averages retain 53 binary digits with extended exponents. Bounded
indices compute their final ratio directly, avoiding intermediate quotient
overflow; direct strength ratios reject genuinely unrepresentable outputs.
Native decimal seed arithmetic can underflow or overflow. Its rounded decay
can stall both averages at the same floor and alter their ratio, which is pinned
by a 5,000-bar regression. These native limitations remain explicit.

Thirty focused/shared tests passed, including all five conventions, nullable
seeds/gaps, tuple/generic/index/range routes, repeat enumeration, chaining,
lifecycle, extremes, long decay and value/presence corruption. Independent scaled
and decimal-stage references checked 57,596 full-output values. Twenty new timing
cases passed; the complete archive contains 1,056 verified cases across 264 pairs.

## Simple and exponential average differences

`MovingAverageDifference` covers Trady SimpleMovingAverageOscillator and
ExponentialMovingAverageOscillator. It subtracts two close averages, with
independent rounding of each average and their final difference. SMA waits for
both complete windows; EMA seeds both averages from the first price. Positive
equal and reversed periods are supported. SMA history grows lazily and EMA
storage is constant, including maximum-period startup.

Seventeen focused tests passed, covering independent lifecycle references,
tuple/generic/index/range routes, repeated reads, startup, chaining, extreme
prices, genuine final overflow and value/presence corruption. Native SMA with
period zero returns absent values; native EMA permits period zero and fails
during evaluation for period minus one. Our API requires positive periods.
Native decimal conversion can erase tiny prices, and decimal sums, recurrences
or differences can overflow. Separate decimal-stage and integer-grid references
checked 26,398 full-output values. Eight timing cases passed. All 1,064 archived
cases across 266 pairs pass the performance evidence verifier.

## Retrospective DPO and confirmed fractals

`DetrendedPriceSnapshot` covers Skender DPO with both published fields: row i
subtracts the SMA ending at i + floor(period/2) + 1 from close[i]. Startup and
unconfirmed trailing rows remain absent. Appending prices can fill trailing
values; this batch API explicitly exposes retrospective alignment. Owned means
and differences round once and reject genuine final overflow. Native staged
SMA sums can overflow, as pinned by a maximum-magnitude tuple regression.

`FractalSnapshot.Calculate` covers Williams Fractal with independent left/right
spans and either high/low or close endpoints. A selected price must strictly
exceed every wing price; any tie invalidates that extremum. Both extrema can be
present on the same candle. Linear-time monotonic deques preserve exact binary64
comparisons. `ChaosBands` covers Fractal Chaos Bands: symmetric high/low fractals
update independent carried lines only after the right wing is complete.
Span arithmetic is widened, avoiding native FCB's checked 2*span overflow.

Twenty-one focused checks passed: exhaustive independent window/confirmed-center
references, future-data alignment, unchanged confirmed prefixes, all fields and
presence corruption, odd/even and asymmetric periods, native generic/tuple/chain
and sorted routes, empty/short/maximum periods, ties, adjacent prices, subnormals
and extremes. Explicit decimal-conversion regressions show native prices losing
strict fractals retained by our binary64 API. Complete trajectory verification
checked 48,176 values; twelve timing cases passed. The archive now contains
1,076 verified cases across 269 pairs.

## Fifty-seeded stochastic KDJ and differences

`FiftySeedStochastic` covers Trady RawStochasticsValue. It returns fifty before
the complete price window and for exactly flat ranges, then computes the
high/low stochastic percentage. `StochasticSmaKdj` covers Fast, Slow and Full
with configurable K/D smoothing and J=3*K-2*D. Fast uses K period one; Slow uses
three. Full D averages the known K values in its complete calendar window,
skipping startup nulls. This can produce D before a full window of present K
values. Each output has independent presence. `StochasticSmaDifference` covers
the three corresponding K-D oscillators without publishing unselected K/D/J.

All ratios, means and combinations round independently. Unpublished values
retain binary64 precision with an extended upper exponent, using existing
scaled arithmetic, so oversized raw ratios can later average or cancel into
finite results. Only unrepresentable published outputs are rejected. Storage
grows lazily for maximum periods; chaining replaces close and retains high/low.
Native decimal products/sums can overflow, and its difference implementation
evaluates an unselected J that can overflow even when K-D is zero. Decimal price
conversion can turn a tiny range into a flat window. These cases are pinned.

Twenty-seven focused/inventory checks passed, including independent lifecycle
and extended arithmetic references, startup and nullable smoothing, all native
tuple/generic/index/range routes, repeated reads, chaining, maximum periods,
finite cancellation, genuine overflow, and every field/presence corruption.
Full trajectory references checked 160,828 values; 28 timing cases passed.
All 1,104 archived cases across 276 pairs pass the performance evidence verifier.

Reflection verifies that the outer Trady Stochastics and StochasticsOscillator
classes only contain named nested types and expose no calculation methods.
They are classified as utilities; their six callable nested families are
individually paired. This leaves 126 pending families in the 421-family inventory.
The prior fractal equality warning is documented as intentional: exact ties
invalidate strict extrema, while adjacent floating-point prices remain distinct.

## Window stochastic momentum and double-smoothed index

`WindowStochasticMomentum` covers Trady midpoint displacement with full-window
startup. `DoubleSmoothedStochasticMomentum` covers Trady StochasticsMomentumIndex
and Skender SMI, including Skender's signal. Both EMA stages seed at the first
complete price window. Each displacement, range and convex update rounds once;
unpublished stages retain an extended upper exponent using existing arithmetic.
The final index evaluates 200*smoothedDisplacement/smoothedRange without
prematurely overflowing a midpoint, displacement or range. Histories grow lazily.

A zero smoothed range leaves index and signal absent; the next defined index
seeds a new signal. Native Skender instead publishes present NaN on a flat range,
and its signal remains NaN even after the index recovers. Direct regressions keep
these nonfinite native values visible and require the comparison verifier to
reject them. Trady decimal half-range rounding can erase tiny outputs, while
midpoint/range/product arithmetic can overflow before a finite index is computed.
Native zero/negative smoothing behavior and Skender period-addition overflow
are separately pinned; the owned API requires positive periods.

Eighteen focused checks passed, covering independent lifecycle and rational
references, quote/tuple/generic/index/range routes, sorting, seed/signal presence,
source chaining, tiny/wide values, oversized unpublished displacement, long decay,
flat recovery, genuine output overflow and every field/presence corruption.
Complete integer-grid and native decimal/binary64-stage references checked
47,964 values. Twelve timing cases passed. All 1,116 archived cases across
279 pairs pass the performance evidence verifier.

## Nullable and smoothed stochastic RSI

`NullableStochasticRsi` covers Trady's unit-scale nullable RSI range. After a
complete calendar window, flat or entirely missing RSI windows yield 0.5;
otherwise a missing current RSI remains absent. `SmoothedStochasticRsi` covers
Skender's 0�100 range, full-window smoothing and signal, with a zero flat range.
Both compose the verified RSI conventions, use exact range ratios and means,
and grow histories lazily without overflowing combined startup periods.

Seventeen focused checks passed, covering independent references, lifecycle,
nullable gaps, native routes and sorting, repeated enumeration, chaining,
maximum periods, tiny/wide prices and every value/presence corruption.
Full trajectories checked 36,126 values. Eight timing cases passed; the complete
archive verifies 1,124 cases across 281 pairs. Native decimal rounding, invalid
period behavior and checked period-sum overflow remain explicitly tested.

## Rolling and decaying close extrema

`DecayingWindowExtreme` covers Quan minimum and maximum with optional decay.
New or equal extremes reset elapsed age. Each step moves toward the available
window mean by 1-exp(-0.1*decay*age/period), then caps the result at the actual
window extreme. Zero decay uses a monotonic deque directly. Positive decay uses
an exact window sum and once-rounded convex update, preserving finite results
when native intermediate sums or differences overflow. Storage grows lazily.

Fifteen focused tests passed: independent rational lifecycle contracts, integer-grid
and native SIMD-stage references, ties, eviction, source chaining/subscription,
revision, lazy maximum periods, tiny/wide prices and corrupted values/startup.
Native reset retains old window prices and native nonfinite decay is accepted;
these differences and present nonfinite outputs are explicitly pinned.
Full trajectories checked 42,760 values across decay and period configurations.
Eight timing cases passed; the archive verifies 1,132 cases across 283 pairs.

## Compensated double and triple exponential averages

`CompensatedExponentialAverage` covers Quan DEMA/TEMA with zero seeds and
shared residual-mass compensation. Alpha, residual and reciprocal are binary64
stages; each convex recurrence, compensated stage and final weighted combination
rounds once. Unpublished stages retain an extended upper exponent; subnormal
stages retain ordinary binary64 rounding. State uses constant storage.
The compensation cutoff checks the updated residual against 1e-10. This differs
from single EMA's extra compensated update and remains a separate convention.

Thirteen focused checks passed, covering independent rational lifecycle/grid
references, native staged references, startup/cutoff transitions, source
subscription, revision/reset, chaining, maximum periods, tiny/wide inputs,
finite cancellation, genuine final overflow and output/alignment corruption.
Native period+1 integer overflow and nonfinite staged products are exposed.
Full trajectories checked 64,504 values. Eight timing cases passed; the complete
archive verifies 1,140 cases across 285 pairs. Explicit-alpha/compensated single
EMA and QEMA use the separate convention described below.

The compensated CPU stages now reuse the exact dyadic blend helper. Certified
subtractions use fused multiply-add; other differences, compensation products
and final combinations retain exact arithmetic and their original rounding.
The net10.0 build, 12 focused unit tests and 32 comparison/setup tests passed.
DEMA/TEMA state updates allocate zero managed bytes over 4,000 ordinary inputs
after JIT warmup and state reset, including seeding/compensation startup. Existing
seeded variants are covered by the same regression tests. These are state-level
allocation checks; updated builder latency is pending consolidated timing.

## Mass-normalized single and quadruple EMA

`MassNormalizedEma` covers Quan's compensated integer-period and explicit-alpha
constructors; `MassNormalizedQuadrupleEma` covers four independently configured
stages combined as 4*E1-6*E2+4*E3-E4. Each stage retains a normalized mean and
rounded mass. Exact weights alpha and (1-alpha)*previousMass form a once-rounded
weighted ratio, followed by rounded mass. The separate binary64 residual preserves
single EMA's extra compensated update at the 1e-10 cutoff. When compensation ends,
the unnormalized sum is published and mass resets to one. QEMA's final combination
rounds once, rejecting genuine overflow. State is constant-sized for all periods.

This equivalent mass representation has an explicit rounding convention distinct
from native unnormalized stages. It avoids subtraction cancellation for tiny alpha:
native 1-residual can become zero even for positive alpha. Owned alpha is finite
and in (0,1]; native parameter holes and nonfinite outputs remain directly tested.
Native QEMA reset retains child EMA state; owned reset clears every stage.

Nineteen focused and existing EMA tests passed, including independent rational
lifecycle and grid references, native staged references, both EMA constructors,
distinct QEMA weights, cutoff transitions, source subscription, revision/reset,
chaining, maximum periods, tiny alpha/subnormal/extreme prices, final overflow,
and output/startup/stage corruption. Complete trajectories checked 122,384 values.
The outstanding EMA configuration list is now empty. Its unchanged SMA-seeded
benchmark retains valid archived timings; four new QEMA timing cases passed.
The complete archive verifies 1,144 cases across 286 pairs.

The shared mass-normalized stage now returns the exact input on its first
update and uses certified fused blends after compensation has ended. Zero blends
retain the exact path to preserve negative underflow. The startup mass recurrence
and cutoff-transition update retain their existing arithmetic. The net10.0 build,
six focused state tests and 16 comparison tests passed. Single and quadruple EMA
steady-state updates allocate zero managed bytes over 4,000 ordinary inputs after
warmup in the regression dataset. Startup beyond the first update can still
allocate; updated builder latency remains pending consolidated timing.

## Adjusted skewness and excess kurtosis

`WindowSampleShape` covers Quan's adjusted Fisher-Pearson sample skewness and
Sheskin excess kurtosis. Available-history startup returns defined zero until
three/four observations respectively. Flat skewness returns zero; mature flat
kurtosis is absent with an explicit presence output and recovers as soon as its
window has nonzero variance. Rolling exact moments and once-rounded rational
or square-root ratios avoid overflowing powers and preserve tiny differences.
History grows lazily for maximum periods.

Eleven focused checks passed, including independent centered rational lifecycle
and integer-grid references, staged native references, known coefficients,
startup, flat recovery, subnormal and adjacent extreme prices, maximum periods,
source subscription/revision/reset, chaining and value/presence corruption.
Native flat kurtosis publishes present NaN; a direct test requires the verifier
to reject it. Native extreme powers and tiny underflow failures remain visible.
Paired kurtosis trajectories/timings use nonflat windows; flat behavior has
separate independent owned and native defect checks, not a masked native value.

Full trajectories checked 25,730 values. Eight timing cases passed. The complete
archive verifies 1,152 cases across 288 pairs.

## Correlation and paired population statistics

`WindowCorrelation` covers TA-Lib Pearson correlation with explicit zero-flat
behavior. `WindowPairStatistics` covers all five Skender outputs: correlation,
R squared, covariance and both population variances, with individual presence
outputs. The two series use independently selected candle fields. Full-window
startup is absent; Skender-style zero variance leaves correlation/R squared
absent while covariance and variances stay defined. Each result rounds once
from exact rolling moments; R squared rounds the exact squared correlation.
Scalar correlation remains finite when variances exceed binary64. Publishing
all statistics rejects genuine variance/covariance overflow. History grows lazily.

Thirteen focused checks passed: independent centered rational lifecycle/grid
references, native recomputed/rolling moments, known coefficients, all output
presence, tiny/adjacent/wide prices, maximum periods, field selection/chaining,
Skender quote/tuple/reusable/sorted routes and date/length validation, TA-Lib
ranges/in-place output, native cancellation/overflow and every field corruption.
TA-Lib rejects one-element ranges, including period one; direct native tests
and independent owned one-bar checks cover that boundary separately. Concrete
TA-Lib input range ends are inclusive, while output range ends are exclusive.
Native Skender converts NaN to absent but retains infinity; these behaviors
remain visible in defect tests.

Full trajectories checked 74,348 values. Eight timing cases passed; the complete
archive verifies 1,160 cases across 290 pairs.

## Return beta and directional beta statistics

`WindowReturnBeta` covers TA-Lib's first-input market denominator and zero-flat
slope. `WindowBetaStatistics` covers Skender's evaluation/market orientation,
all four subset modes, and all seven outputs: standard/up/down beta, ratio,
convexity and both return series, with individual presence flags. Startup needs
period returns, or period+1 prices. A zero previous price gives zero return;
zero market returns enter only standard beta. Empty/flat Skender subsets are
absent, and zero down beta makes the ratio absent.

Each owned return rounds once with an extended upper exponent. Rolling exact
moments produce once-rounded beta; derived ratios/convexity also round once.
This preserves finite scalar beta across oversized unpublished returns. Full
statistics reject genuinely overflowing published returns or final outputs.
Histories grow lazily. Native Skender divides prices then subtracts one;
TA-Lib subtracts prices then divides. Their separate staged references retain
these numerical differences.

Thirteen focused tests passed, including independent centered rational lifecycle
and integer-grid references, known slopes/directional coefficients, all modes,
zero prices, flat subsets, tiny/wide values, maximum periods, true overflow,
field selection/chaining, Skender quote/tuple/reusable/sorted routes and date
validation, TA-Lib ranges/aliasing/one-element failure, native parameter holes,
and every output/presence corruption. Full trajectories checked 116,112 values.
Eight timing cases passed; the complete archive verifies 1,168 cases across
292 pairs.

Three annotation-only fixes move existing exact-flat RSI justifications onto
the comparisons flagged by Sonar. The formulas and their verified behavior
are unchanged; the current-head quality gate still requires confirmation.


## Commodity channel index conventions

Skender, TA-Lib, and Trady CCI now have complete comparisons through
`WindowCommodityChannelIndex`. Standard CCI uses the current window mean;
Trady averages distances from each historical price's own trailing mean and
starts after 2*period-1 bars. Skender flat windows are absent; TA-Lib and Trady
return zero. Trady's conditional default literal resolves to decimal zero.

Typical prices and trailing means round once. Absolute deviations remain exact,
and the final ratio uses the exact constant 0.015 before rounding once. Lazy
histories avoid eager maximum-period allocation and summed-period overflow.
Independent rational/grid references cover the owned formula; staged binary64
and decimal references retain each native convention, including TA-Lib's
physical ring order. Tests retain native tiny-value collapse, nonfinite overflow,
Trady decimal underflow/division failure, invalid periods, and one-element
TA-Lib range rejection. Sorting, generic/tuple/index/range routes, close chaining,
reset, full presence masks, and value/presence/formula mutations are covered.

Verification: 16 focused tests and 37,302 trajectory values passed. Twelve timing
arms passed; the full archive verifies 1,180 arms across 295 pairs.


## Window-relative Ulcer Index

Skender Ulcer Index is paired with `WindowUlcerIndex`. Each complete window
restarts its running maximum at zero; any prefix lacking a positive peak makes
the result absent. This differs from averaging previously calculated trailing
window drawdowns. Percentage drawdowns round once with an extended upper
exponent, exact squared sums avoid intermediate overflow, and the final root
rounds once. Genuinely overflowing outputs are rejected. Histories grow lazily.

Independent rational/grid and staged native references cover complete values
and presence. Regression tests cover window restarts, missing-prefix recovery,
flat/tiny/adjacent/maximum inputs, lazy maximum periods, genuine output overflow,
reset and close chaining. All quote, tuple and reusable-result routes, sorting,
default parameters and startup alignment are checked. Native squared-percentage
overflow can produce infinity despite a finite owned result; decimal quote
conversion can erase tiny positive peaks. Dedicated tests preserve those limits.

Verification: 9 focused tests, 12,201 trajectory values and 4 timing arms passed.
The archive now verifies 1,184 arms across 296 pairs, with no unmeasured pair.


## True-interval Choppiness Index

Skender Choppiness Index is paired with `WindowChoppinessIndex`. Each interval
includes the preceding close, and period intervals require period+1 input bars.
A zero full-window span is absent. Exact range differences and rolling sums
avoid intermediate overflow. Stable logarithms preserve ratios near one;
subsequent division by log(period) and multiplication by 100 use binary64 stages.
Histories grow lazily. Inconsistent candles that produce zero summed ranges but
a nonzero span imply a nonfinite output, which normal output validation rejects.

Independent rational log1p/fixed-point references cover near-unit ratios and
large/tiny prices, while a staged native oracle retains its summation order.
Tests cover known 0/100 values, gaps, startup, flat-window recovery, default and
maximum periods, sorting, close chaining, reset, and output/presence mutations.
Native decimal quote conversion can erase tiny ranges and excludes binary64
extremes. Native division can round a non-unit ratio to one, losing a small
positive Choppiness value; this limitation remains a dedicated regression.

Verification: 9 focused tests, 12,475 trajectory values and 4 timing arms passed.
The archive verifies 1,188 arms across 297 pairs, with no unmeasured pair.


## Directional Money Flow Index conventions

Skender and TA-Lib MFI are paired with `WindowMoneyFlowIndex`. Typical HLC/3
rounds once; exact comparisons assign flow direction and equal prices contribute
neither flow. Exact volume products and rolling sums preserve the final ratio
through underflow, overflow and cancellation. The final percentage rounds once.
Skender's zero-negative-flow case returns 100; TA-Lib's total-below-one case
returns zero. Startup requires period changes after the first typical price.
Optional additional suppression reproduces TA-Lib unstable-period alignment.
Histories grow lazily and startup arithmetic does not overflow at maximum periods.

Independent rational/grid references check owned values and presence. Separate
native references retain Skender's chronological sums and subtractive formula,
and TA-Lib's rolling sums and total-flow threshold. Tests cover known coefficients,
exact threshold boundaries, tiny/adjacent/maximum inputs, flat ties, lifecycle,
chaining with unchanged high/low/volume, sorting, defaults, ranges, aliasing,
float inputs and restored global unstable settings. Native decimal collapse,
underflowed/overflowed products, cancellation to zero, rejected one-element TA
ranges and overflowing TA lookbacks remain visible. Undefined or overflowing
owned final outputs are rejected.

Verification: 12 focused tests, 24,950 trajectory values and 8 timing arms passed.
The archive verifies 1,196 arms across 299 pairs, with no unmeasured pair.


## Chandelier Exit selections and conventions

Skender and Trady Chandelier Exit are paired with `WindowChandelierExit`, reusing
the verified mean-seeded Wilder ATR engine. Skender long floors the rolling high
at zero; short uses the actual low. Trady emits both actual-extreme exits and
permits zero and negative multipliers. Output begins at index period. Selected
levels evaluate independently, so an overflowing unselected exit cannot reject
a finite selected exit. Extended ATR stages and exact complete offset expressions
preserve finite cancellation; histories grow lazily.

Independent rational/grid and native binary64/decimal references cover values,
presence, selections and multipliers. Trady's 1.0m smoothing constant is retained:
decimal scale can affect conversion to double even for numerically equal decimals.
Tests cover both conversions, generic/tuple/index/range/repeated routes, defaults,
sorting, chaining, reset, tiny/maximum inputs and selected-output overflow.
Skender period one is rejected through ATR; unknown selections are only rejected
when mature, while NaN/infinite multipliers pass its validation. Owned constructors
reject invalid selections and nonfinite multipliers. Native decimal overflow and
quote conversion that erases tiny ranges remain explicit regressions.

Verification: 12 focused tests, 47,644 trajectory values and 8 timing arms passed.
The archive verifies 1,204 arms across 301 pairs, with no unmeasured pair.


## Keltner and STARC envelope conventions

`WindowAtrBands` now pairs Skender Keltner, Skender STARC and Trady Keltner.
Skender Keltner uses an SMA-seeded EMA; Trady uses a first-close EMA; STARC uses
a rolling SMA. Keltner centers start at max(centerPeriod,atrPeriod)-1, while STARC
centers publish independently at centerPeriod-1. Bands require both a center and
mature mean-seeded Wilder ATR. Every center, band and presence flag is checked;
Skender Keltner also includes normalized width, absent for an exactly zero center.

Center recurrences and complete band expressions round once. Extended ATR stages
preserve finite offsets across oversized ranges. Width uses the exact difference
of the published bands, avoiding intermediate subtraction overflow. Width is
optional, so its genuine overflow cannot reject Trady/STARC's finite bands.
Histories grow lazily and periods do not overflow during startup arithmetic.

Independent rational/grid, native binary64 and scale-preserving decimal references
cover unequal periods, early centers, multipliers, all outputs and masks. Tests
include lifecycle, generic/tuple/index/range routes, decimal-to-double conversion,
defaults, sorting, chaining, and value/presence/seed mutations. Native nonfinite
multiplier holes, decimal overflow and loss of tiny quote ranges remain explicit.

Verification: 14 focused tests, 160,232 trajectory values and 12 timing arms passed.
The archive verifies 1,216 arms across 304 pairs, with no unmeasured pair.


## Vortex and Ultimate true-range ratios

`WindowVortex` pairs both Skender positive/negative movement ratios and their
presence flags. Output begins after period movements; a zero total true range
is absent. `WindowUltimateOscillator` pairs Skender and TA-Lib's 4:2:1 pressure
ratios. Skender requires strictly increasing periods and makes any zero-range
contribution absent. TA-Lib sorts positive periods, permits duplicates and uses
zero for zero-range contributions. All differences, sums and weighted ratios
remain exact until final rounding. Histories grow lazily.

Independent rational/grid references and staged chronological/rolling native
oracles cover all outputs and period conventions. Tests cover known ratios,
startup, flat recovery, tiny/maximum prices, lazy maximum periods, lifecycle,
sorting/defaults, chaining, value/presence/policy mutations, and TA ranges,
aliasing and float inputs. Native decimal quotes can erase tiny ranges; TA
intermediate overflow remains visible. TA all-one-period lookback returns zero
and its full-range call attempts an invalid previous-close access. That native
failure and one-element range rejection are directly tested; the owned all-one
configuration has independently verified values from index one.

Verification: 15 focused tests, 50,020 trajectory values and 12 timing arms passed.
The archive verifies 1,228 arms across 307 pairs, with no unmeasured pair.


## Trady directional movement and early ADX

`WindowDirectionalMeasure` supplies seven selected counterparts: signed raw
positive/negative movement, dominant positive/negative Wilder-mean DI, DX,
early-seeded ADX, and delayed ADXR. Differences are compared exactly before
rounding selected movement; smoothed stages retain extended upper exponents.
Published raw overflow is rejected without rejecting finite normalized results.

Trady's DX returns zero before DI is available, but null when mature positive
and negative DI are both zero. Early ADX averages DX at indices 1 through period,
including startup zeroes. Later missing DX permanently propagates absence.
ADXR requires both aligned ADX endpoints; zero lag is valid and maximum lag uses
lazy storage. These conventions differ from Skender and TA-Lib.

Independent rational validation, integer-grid comparison, and staged decimal
native references verify every output and presence flag. Native smoothing keeps
its decimal scale (`1.0m / period`). Decimal overflow and collapse of tiny prices
remain explicit native limitations. Generic, candle, tuple, index, range,
repeated, chained and lifecycle routes have regression coverage, as do value,
presence and formula mutations.

Verification: 21 focused tests, 91,355 full-output trajectory comparisons and
28 timing arms passed. The complete timing archive now verifies 1,256 arms
across 314 pairs, with no unmeasured pair. Final complete correctness union and
current-head gates remain required before readiness.


## Skender sum-seeded directional index

`SumSeededDirectionalIndex` covers Skender ADX's five outputs: Pdi, Mdi, Dx,
Adx and Adxr, each with an explicit presence flag. It selects dominant movement
using exact differences, rounds selected movement/range and each sum stage once,
and retains extended upper exponents for finite normalized results. DI/DX start
at index period; ADX starts at 2*period-1 and ADXR lags by a full period.

Zero smoothed range skips the ADX update and publishes no outputs. A skipped
seed never recovers; a later range gap retains the prior ADX state, while ADXR
requires both published endpoints. Lazy queues and 64-bit derived lookbacks
support maximum periods. Independent rational/grid references cover these gaps,
tiny distinct movements, exact ties and oversized ranges. The native reference
retains Skender's chronological binary64 arithmetic; decimal input conversion
can erase tiny movements. Sorting, defaults, chaining, lifecycle, all values and
presence, and altered-period mutations have regression coverage.

Verification: 10 new focused tests plus all 21 Trady directional tests passed;
64,564 trajectory comparisons and four timing arms passed. The timing archive
now verifies 1,260 arms across 315 pairs, with no unmeasured pair. Final complete
correctness union and current-head gates remain required before readiness.


## TA directional measures and unstable startup

`PriorSeededDirectionalMeasure` covers PlusDM, MinusDM, PlusDI, MinusDI, DX,
ADX and ADXR. Exact differences choose strictly dominant movement; selected
movement/range, period-minus-one sum seeds and Wilder stages round once with
extended upper exponents. Period one returns raw dominant movement or DI as a
ratio without times100, and ignores unstable suppression. Longer-period DI uses
percentages. DX retains its previous published value at zero denominators;
ADX retains its previous average when direction is absent. ADXR uses a
period-minus-one lag and inherits ADX's unstable setting; this package has no
separate ADXR unstable setting. Wide lookbacks and lazy rating history support
maximum owned periods and suppression without eager allocation.

The installed native DX API reports packed offsets starting at zero, not input
bar indices. Its adapter validates that exact returned range before placing the
unchanged values on the bars implied by the lookback; full and subrange API tests
preserve direct evidence of the defect. Native DX also updates true range twice
after its first published output; the counterpart preserves that recurrence.
Native lookbacks can overflow and raw movement can become infinite; direct
boundary tests retain these limitations, while owned final overflow is rejected.

Independent rational/grid and chronological native references cover complete
values and presence. Regressions include settings, period one, subranges, aliasing,
float input, exact ties, subnormal retention, extended ranges, chaining, lifecycle,
and value/presence/seed/suppression mutations.

Verification: 16 focused tests, 97,939 trajectory comparisons and 28 timing arms
passed. The full timing archive verifies 1,288 arms across 322 pairs, with no
unmeasured pair. Final complete correctness union and current-head gates remain
required before readiness.


## Tillson T3 seed conventions

`TillsonAverage` covers Skender, TA-Lib and both QuanTAlib startup options.
Skender seeds all six stages from the first price. TA-Lib seeds six full means
in sequence and supports unstable suppression. QuanTAlib's default publishes
the first value, then builds prefix means excluding that first price; its
alternate option uses first-price EMA stages. Each owned stage uses exact
2/(period+1) weights and rounds once. The final cubic volume-factor polynomial
is evaluated exactly before publication, retaining finite cancellation even
when individual coefficient products exceed binary64. Period storage is constant
and derived lookbacks use 64 bits. Any finite volume factor is supported.

Independent rational/grid references cover owned outputs and staged native
references preserve the native coefficient arithmetic, EMA operation order and
Quan prefix SIMD lane sums. Quote/tuple/reusable/sorted/default/chained routes,
TA subranges/aliasing/float/settings, Quan subscription/revision/hot-state routes,
lifecycle and value/presence/seed/factor mutations have regression coverage.
Skender throws on maximum-period addition overflow; TA lookbacks can wrap.
Quan allocates six period-sized buffers and its Init retains the sample index,
so its reset output differs from a fresh instance. Native NaN factors and
nonfinite coefficient arithmetic remain visible. Owned nonfinite factors and
true final overflow are rejected; exact finite cancellations are retained.

Verification: 10 focused tests, 104,164 output comparisons and 12 timing arms
passed. The full timing archive verifies 1,300 arms across 325 pairs with no
unmeasured pair. Final complete correctness union and current-head gates remain
required before readiness.


T3 now caches the four exact cubic coefficients and their common denominator
once per state, removing repeated fixed-factor products from each update.
All three seed modes and classical-average composition share this path. The
net10.0 build, three extended-value/reset tests and 25 T3/classical-average
comparison tests passed. Updated allocation and latency measurements remain
pending; smoothing stages still use their existing exact arithmetic.

## Triple EMA rate and optional signal

`TripleExponentialRate` covers Skender TRIX, EMA3 and optional SMA signal,
and TA-Lib TRIX under default and Metastock compatibility with per-stage EMA
unstable suppression. Skender starts all three stages from one price mean;
TA starts each stage separately. Owned EMA stages and percentage changes round
once using exact arithmetic. Signals require a complete window of present rates.
Shared zero/zero yields absence; unbounded change is rejected as final overflow.
Cascaded zero denominators return zero. Lazy signal queues and 64-bit derived
lookbacks support maximum owned periods without eager allocation.

Independent rational/grid and staged native references verify every value and
presence flag. Tests cover quote/tuple/reusable/sorted/chained routes, optional
signal lengths, TA subranges/aliasing/float/settings, lifecycle, oversized finite
price differences, subnormals, and output/presence/signal mutations. The native
TA subrange pipeline reseeds its later EMA stages from a truncated first-stage
buffer, so it is tested independently of a full-history slice. TA accepts period
one but gives a negative lookback and throws; the owned definition remains valid.
Skender accepts zero/negative signal lengths with NaN/zero results, while owned
signals require positive lengths. Native checked period/signal sums and unchecked
TA lookbacks have explicit overflow regressions.

Verification: 10 focused tests, 76,517 output comparisons and eight timing arms
passed. The full archive verifies 1,308 arms across 327 pairs with no unmeasured
pair. Final complete correctness union and current-head gates remain required
before readiness.


## Mean-seeded true strength and price momentum

`SeededTrueStrength` and `SeededPriceMomentum` cover Skender TSI and PMO,
including complete value and signal presence. Independent rational/grid and
staged native references cover mean seeds, all smoothing/signal combinations,
reset, chaining, route equivalence, missing inputs and extended intermediate
exponents. TSI preserves the late smoothing-one startup and shortened signal
seed; signal one remains absent. Native present-NaN startup is checked explicitly
and represented as absent by the finite counterpart. PMO retains 2/period
smoothing, separate times-ten scaling, smoothing-one overshoot and permanently
missing recurrences after zero prior prices. Published overflow is rejected.

Validation: 9 focused tests and 151,450 complete trajectory comparisons passed.
Eight timing arms passed; the cumulative archive contains 1,316 arms across
329 pairs, with 73 families remaining. Native checked parameter sums, quote
conversion, nonfinite startup and overflowing intermediate returns are explicit
regressions. Final complete correctness union and current-head gates remain
required before readiness.


## Price relative strength

`PriceRelativeStrength` covers Skender PRS, optional SMA of its ratios, and
optional difference of fractional returns. Evaluation/base fields provide
synchronized series. Complete ratio and return expressions round once; ratio
means accumulate published values exactly. Zero base prices produce absence,
missing ratios invalidate only affected mean windows, and histories grow lazily.
Native quote/tuple/reusable routes, date/length rejection, optional parameters,
sorting, chaining and every output/presence mutation have independent checks.
Oversized native return terms can become null after intermediate overflow while
the owned complete expression retains finite cancellation. Genuine published
overflow is rejected.

Validation: 9 focused tests, 85,652 trajectory comparisons and 4 timing arms
passed. The archive now contains 1,320 arms across 330 pairs, with 72 families
remaining. Final complete correctness union and current-head gates remain
required before readiness.


## McGinley dynamic conventions

`QuarticDynamicAverage` covers Skender Dynamic and QuanTAlib Mgdi. Its complete
quartic recurrence rounds once, retaining finite results through overflowing
differences, powers and scale products. Immediate startup publishes the first
close and substitutes ratio one at zero previous state. Reset-delay startup
omits the first result and restarts a period-long delay at zero previous state.
Singular zero-price transitions and overflowing recurrence states are rejected,
including during restarted delays. State storage is independent of period.

Independent rational/grid and staged native references cover complete values,
presence, factors, periods, lifecycle, chaining and mutations. Native checked
restart-index overflow, nonfinite factors/results, Quan subscriptions, revisions,
hot flags and reset behavior have explicit regressions. Rejection checks verify
the finite prefix and exact failure point; native nonfinite results stay visible.

Validation: 8 focused tests, 41,639 trajectory/boundary checks and 8 timing arms
passed. The cumulative archive contains 1,328 arms across 332 pairs, with 70
families remaining. Exact arithmetic is substantially slower than the native
binary64 recurrences in these measurements; the archive records this cost.
Final complete correctness union and current-head gates remain required.


## Historical and realized log-return volatility

`WindowLogVolatility` covers QuanTAlib Historical sample deviation and Realized
root mean square, with optional 252-day annualization. Directed fixed-point
log intervals certify each rounded return before exact moment accumulation;
the complete root including annualization rounds once. This preserves tiny
dispersion between nearly equal returns and finite logs of extreme price ratios.
Independent rational and fixed-point references cover the complete outputs.

Zero prior prices skip returns. Historical continues reading the existing
window; Realized emits zero on the skipped bar. Startup zero is present.
Nonpositive ratios consume an undefined return: Historical recovers when the
return expires, while Realized preserves its permanently poisoned sum as absent
output. Native nonfinite results are checked without masking them.

Native revised bars fail to restore prior state, and bar-only calls consume
stale value input. Tests cover both defects, subscription, paired-value input,
reset/hot flags, invalid input, period validation and the historical maximum-
period capacity overflow. Owned histories grow lazily.

Validation: 10 focused tests, 37,232 trajectory checks and 8 timing arms passed.
The archive contains 1,336 arms across 334 pairs; 68 families remain. The Sonar
exact-equality finding in the native infinity verifier has an explicit inline
justification; current-head gate confirmation remains required, as does the
final complete correctness union.


## EMA differences, MACD signals and volume percentages

`EmaDifferenceSignal` covers Skender MACD's oscillator, signal, histogram and
fast/slow averages; Skender PVO's volume-based percentage oscillator, signal and
histogram; and Trady MACD and histogram. Each EMA uses an explicit complete-mean
or first-price seed. The signal begins at the first available oscillator; a
missing percentage at zero slow EMA contributes zero to signal while leaving
the histogram absent. Period state uses constant storage.

Every stage rounds once with extended upper exponents. Output selection lets a
histogram retain finite cancellation when its hidden oscillator and signal are
unrepresentable; only selected published overflow is rejected. Independent
rational/grid and staged binary64/decimal references verify all public outputs,
presence, source choice, period configurations, chaining, lifecycle and mutations.
Quote/tuple/reusable/sorted Skender routes and Trady tuple/generic/index/range/
repeated routes are covered. Native zero-signal rejection, checked Skender
period overflow, Trady invalid smoothing periods and decimal overflow have
explicit regressions.

Validation: 10 focused tests, 182,998 trajectory comparisons and 16 timing arms
passed. The archive contains 1,352 arms across 338 pairs, with 64 families
remaining. Final complete correctness union and current-head gates are still
required before readiness.


## TA-Lib aligned and fixed-coefficient MACD

`AlignedEmaMacd` covers TA-Lib MACD and MACDFIX with all three outputs. Mean
seeds share the slow-period endpoint; first-price compatibility processes both
averages from bar zero. Startup suppression applies before oscillator startup
and again before signal publication. Fixed mode uses binary64 0.15/0.075
coefficients with periods 12/26. Periods are sorted and state size is constant.

Every complete stage rounds once with extended upper exponents; selected
histograms retain cancellation across unrepresentable hidden differences and
signals. Independent rational/grid and generic native float/double references
cover full trajectories, shifted subrange seeds, each input/output alias route,
compatibility, suppression, period configurations, chaining and mutations.
Native signal one passes the wrapper but its negative EMA lookback triggers a
slice exception; the counterpart provides its mathematical zero histogram.
One-element ranges and overflowing lookback/allocation arithmetic are explicit
regressions.

Validation: 10 focused tests, 117,696 trajectory comparisons and 8 timing arms
passed. Two Sonar findings about repeated startup assignments in the volatility
references were removed by initializing their arrays once; all 10 affected
volatility tests passed again. The cumulative archive contains 1,360 arms across
340 pairs, with 62 families remaining. Final correctness union and current-head
gates remain required before readiness.


## Smoothed accumulation oscillators

`SmoothedAccumulationOscillator` covers Skender Chaikin Oscillator (all four
outputs) and TA-Lib ADOSC. Skender independently mean-seeds its two EMAs;
TA seeds both from the first cumulative flow, preserves period order, ignores
compatibility, skips nonpositive candle ranges, and honors EMA unstable
suppression. TA subranges restart the cumulative line at start minus lookback.
The counterpart computes complete flow expressions before rounding, accumulates
rounded flows exactly, and rounds each EMA stage with extended upper exponents.
Hidden overflow can cancel into a finite oscillator; publishing an overflowing
selected detail is rejected. Period state is constant-space.

Independent rational and integer-grid references cover lifecycle, every output
and presence flag, startup, negative candle ranges, reversal, large periods,
overflow, chaining, and mutation detection. Native quote ordering, binary64
stages, generic floats, shifted ranges, and four input/output alias routes are
verified separately. Eight focused tests and 73,321 full-trajectory comparisons
passed. Eight timing arms bring the archive to 1,368 arms across 342 pairs;
60 families remain before final union verification and current-head gates.


## Range acceleration bands

`RangeAccelerationBands` covers all three TA-Lib Accbands outputs, averaging
transformed highs, closes, and transformed lows over a complete window. Exact
zero high+low leaves both candle extrema unchanged. Complete transformed inputs
round once with extended exponents, and each mean rounds once; hidden overflow
can cancel before publication. The public counterpart supports period one and
lazy histories for very large periods. TA requires periods of at least two.

Independent rational, integer-grid, and native generic references verify all
outputs, lifecycle, startup, zero denominators, extreme periods, negative values,
subnormal inputs, finite results after native overflow, chaining, mutations,
subranges, float arithmetic, and all nine input/output alias combinations.
Nine focused tests and 43,251 trajectory comparisons passed. Four archived timing
arms bring coverage to 1,372 arms across 343 paired families, with 59 pending.
Final union verification and current-head gates remain required.


## Ichimoku cloud snapshot

`IchimokuCloudSnapshot` covers Skender's three overloads and all five nullable
outputs with independently configurable forward/backward offsets. Leading A
retains the native extra startup gate. Lagging close uses future input; appending
bars can fill trailing missing rows. Input order is retained by the snapshot,
while native quote routes sort. Exact rounded midpoints preserve negative and
subnormal extrema and avoid overflow in finite averages. Native decimal highs
are incorrectly clamped to zero and decimal.MaxValue lows appear missing;
independent native references and explicit regressions preserve those findings.
Large native offsets throw OverflowException; counterpart indexing uses Int64.

Six focused tests and 105,783 trajectory comparisons passed, including all
nullable masks, native overloads/custom quote mapping, offsets, append behavior,
large periods and corruption detection. Four archived timing arms bring coverage
to 1,376 arms across 344 paired families, with 58 pending. Trady's extended
Ichimoku output range remains a separate pending family. Final union verification
and current-head gates remain required.


## Extended Ichimoku cloud

`IchimokuCloudSnapshot.Extended` covers Trady's full output range from index
1-basePeriod through count-1+basePeriod. All five nullable fields are verified,
including leading projections outside the input and the lagging shift of
basePeriod-1. Output rows are lazy, use Int64 coordinates, and capture their input
values before enumeration. Explicit output-coordinate ranges are supported.
Native Compute(start,end) expands both endpoints; explicit native index requests
and tuple/generic/candle mappings are separate regressions.

Eleven focused checks (including the Skender regressions), 99,197 trajectory
comparisons and four timing arms passed. Empty input, negative values, extended
startup, future projection, captured input isolation, subnormals, large shifts,
Int64 endpoint indexes and every output/mask mutation are covered. The archive
now contains 1,380 arms across 345 pairs; 57 families remain pending before final
union verification and current-head gates.


## Seeded adaptive averages

`SeededAdaptiveAverage` covers Skender and QuanTAlib KAMA. Both seed from the
close at period-1. Skender publishes KAMA and efficiency ratio and resets to the
current close on a flat window; Quan also publishes earlier closes, caps the
fast period at the efficiency period, and uses slow smoothing when efficiency
is zero. Exact differences and sums prevent overflow, and each specified logical
stage rounds once. Startup metadata matches actual average presence. History
grows lazily; maximum-period startup is checked without allocating that period.

Independent rational and grid/native-stage references cover lifecycle, seed and
flat transitions, coefficient configurations, all outputs and masks, extreme
prices, subnormals, chaining, quote/tuple/reusable/sorted routes, native event
updates, revisions, reset and mutation detection. Native slow-period addition
can overflow. Eight focused tests, 46,879 trajectory comparisons and eight timing
arms passed. The archive contains 1,388 arms across 347 paired families, leaving
55 pending; final union and current-head gates remain required.


## Trady adaptive average

Trady KaufmanAdaptiveMovingAverage is paired with `SeededAdaptiveAverage` using
last-price seeding, full-window startup and slow smoothing for flat windows.
The native implementation instead dereferences its missing flat efficiency and
throws InvalidOperationException. Dedicated checks require that exact native
failure while independently verifying the finite counterpart; successful native
trajectories retain decimal arithmetic and literal scaling. Missing nullable
inputs, tuple/generic/candle routes, index requests and expanded seed behavior
are verified separately.

Thirteen focused tests, including existing adaptive regressions, 15,285 trajectory
comparisons and four timing arms passed. Corrupt values/masks, wrong coefficients,
wrong flat continuation and suppressing the native exception are detected. The
archive contains 1,392 timing arms across 348 pairs; 54 families remain pending.
Final complete union and current-head gates remain required.


## TA-Lib adaptive average

TA KAMA is paired with `SeededAdaptiveAverage` using fixed fast2/slow30,
`resetFlat:false`, `fastFlat:true` and `outputDelay:1+suppression`. Average
publication starts at period plus the KAMA unstable setting; efficiency remains
available from its ordinary startup. Compatibility has no effect. Native
references preserve sliding volatility rounding and the signed-change saturation
condition, range-local restarts, generic float arithmetic and aliased input.
The counterpart uses exact differences, convex recurrence stages and overflow-safe
Int64 publication delays. Native overflowing lookbacks are explicit regressions.

All 18 adaptive-average tests passed. The exact-zero oracle spelling change
(`T.IsZero`, preserving the formula) also passed 14 affected checks including
Acceleration Bands. 18,646 trajectory comparisons and four new timing arms
passed. The archive has 1,396 arms across 349 paired families; 53 remain pending.
Final union verification and current-head gates remain required.


## Six-price-seeded MAMA and FAMA

`SeededPhaseAdaptiveAverage` covers Skender and QuanTAlib MAMA/FAMA with bounded
Hilbert history. Skender quotes use HL2; tuple/reusable routes use their supplied
series. Skender publishes a six-price mean seed, while Quan publishes expanding
means during startup. Quan's zero-input phase/period branches reuse values from
two bars earlier. Both output values and masks are compared.

Exact grid and rational references verify rounding of complete FIR sums,
correction products, phasor differences, homodyne products, smoothing and convex
output stages with extended upper exponents. Period/phase controls explicitly
use binary64 operations and Math.Atan of a rounded exact ratio. Native references
retain their staged binary64 operations and nonfinite results. Finite extreme
prices can overflow the native seed/filter arithmetic; direct regressions retain
those infinities/NaNs while the counterpart stays finite. Quan Init only resets
its exposed Fama value; a regression demonstrates retained averaging state.

Eight focused tests, 56,238 trajectory comparisons and eight timing arms passed,
covering lifecycle, coefficients, startup, HL2/tuple/reusable/sorted input routes,
events, revisions, reset, chaining, subnormals and output/mask mutations. The
archive contains 1,404 arms across 351 pairs, with 51 pending families. Final
complete union verification and current-head gates remain required.

## Delayed zero-seeded MAMA and FAMA

`DelayedPhaseAdaptiveAverage` covers TA-Lib MAMA with zero filter/average seeds,
Hilbert stages starting at index twelve, and publication at 32 + suppression.
Fast and slow limits independently span [0.01,0.99], including reversed limits.
Storage is bounded independently of suppression; metadata saturates safely.
Independent rational and grid references cover both outputs and presence flags.
The native reference preserves staged arithmetic, float, shifted subranges and
both alias routes. Compatibility and unrelated EMA suppression do not affect
MAMA. Native NaN limits and overflowing lookback behavior remain explicit tests.

Fifteen focused tests (including shared seeded-MAMA regressions), 34,492 output
comparisons and four timing arms passed. The archived union now has 1,408 arms
across 352 pairs, with no unmeasured pairs and 50 families pending. The final
complete correctness union and current-head gates remain required.

## Deviation-ratio adaptive average

`DeviationRatioAdaptiveAverage` covers QuanTAlib VIDYA: short-window means for
the first long-period observations, then gain = alpha times the population
deviation ratio. Independent positive periods and finite signed alpha are
supported; omitted long period is four times short period with checked range.
Exact rolling moments, lazily growing history and separately rounded ratio,
gain and complete recurrence stages with extended upper exponents preserve
finite results even when the intermediate ratio exceeds binary64 range.

A zero long variance leaves the recurrence undefined until reset. Our value
and presence outputs represent that explicitly. Native NaNs are retained in
direct regressions and represented as missing in the paired adapter; infinities
are never hidden. Native Init retains both price buffers. Rational lifecycle,
independent centered-grid and native staged references verify startup, window
order, signed/zero gain, source events, revisions, reset, chaining, extremes,
subnormals and output/presence/gain mutations. Eight focused tests and 19,175
output comparisons passed. Four updated timing arms passed; the archive union
contains 1,412 arms across 353 pairs. There are 49 pending families; final full
correctness union and current-head gates remain required.

## Complete-window deviation bands and width

`WindowDeviationBands` covers Skender Bollinger Bands, Trady Bollinger Bands
and Trady Bollinger Band Width. The public counterpart has six values and
matching presence flags: center, upper, lower, band position, exact-distribution
Z-score and relative width. Trady width is percentage-scaled; Skender width is
unscaled. Native object, generic, tuple, reusable-result and sorting routes
are verified. Trady supports period one and signed/zero factors; Skender
requires periods above one and positive factors, but admits NaN factors.

Mean and population deviation round once, followed by the scaled deviation
with extended upper exponents. Ratios use this center and offset before final
band rounding, preserving position when published bands coincide. Zero offset
makes position absent; zero center makes width absent; zero variance makes
Z-score absent. Independent rational, centered-grid and native binary64/decimal
references retain each contract. Native Skender overflow/nonfinite outputs and
Trady decimal variance overflow are explicit boundary regressions.

Ten focused tests plus one expanded overflow-boundary check, 194,368 output
comparisons across three shards and twelve timing arms passed. Production
binaries were reused for adapter/test corrections. All timings retain the
public counterpart's complete six-output calculation, including when a native
API returns fewer outputs. The archive union has 1,424 arms across 356 pairs,
with no unmeasured pairs and 43 pending families. Final complete correctness
union and current-head gates remain required.

## Generic classical moving average routing

`ClassicMovingAverage` covers all nine TA-Lib MA methods. Period one is identity
regardless of method or startup settings. EMA/DEMA/TEMA use full-mean or
first-price seeds and per-stage suppression. KAMA/MAMA/T3 retain their own
startup rules; MAMA ignores requested periods above one. Exact exponential
recurrences and final extrapolation use extended upper exponents. Lookbacks
use Int64, metadata saturates, and window storage grows with observed data.

Independent rational lifecycle and integer-grid references cover every method.
A separate native full-history model verifies float/double stages, shifted
ranges, both compatibility modes, suppression and aliased buffers. Native
period-one identity even accepts undefined MAType values; our enum validation
rejects these. Native WMA's Int32 divisor overflows: a 65,536-bar constant-one
window returns 65,537; the counterpart returns one. These discrepancies and
one-element rejection, extreme/subnormal data, chaining, reset and output,
mask, method and seed mutations have executable regressions.

Fifteen focused tests, 89,002 output comparisons and four timing arms passed.
The generic API's default SMA is timed here; constituent methods also retain
their separately archived direct-API timings. The archive union contains
1,428 arms across 357 pairs; 45 families remain pending. Final complete
correctness union and current-head gates remain required.

## Classical absolute and percentage price oscillators

`ClassicPriceOscillator` covers TA-Lib APO and PPO across all nine average
methods, both compatibility seeds, suppression, reversed/equal periods and
independent startup alignment. Periods are ordered before calculation. APO
rounds the complete difference; PPO rounds 100 times that difference divided
by the slow average, returning zero for an exactly zero denominator. Ooples
supports positive periods including one; native APIs require at least two.

DEMA/TEMA/T3 components retain their rounded values with extended upper
exponents until oscillator publication. Their standalone outputs are unchanged.
A lifecycle regression exposed that an unrepresentable component average can
still produce a finite PPO; retaining the wide value fixes this without
misclassifying intermediate overflow as final output overflow. Independent
rational and grid references verify these cases, ordinary/subnormal inputs,
source chaining, masks, formula/period mutations and lifecycle. Separate native
references retain staged subtraction/division/multiplication, float/double,
shifted ranges, aliases and nonfinite extreme arithmetic.

The grouped oscillator/generic-average/T3 suite passed 36 tests. APO/PPO passed
696,188 output comparisons; four directly affected averaging pairs passed
193,166 further comparisons. Eight timing arms passed. Sonar S1244 in the
Bollinger native reference was corrected with exact nullable equality plus
explicit NaN handling; its ten tests and 109,292 comparisons passed without
introducing a tolerance. Current-head Sonar confirmation remains required.
The archive union contains 1,436 arms across 359 pairs, with 43 families pending.
Final complete correctness union and current-head gates remain required.


## Classical deviation bands

`ClassicDeviationBands` supplies TA-Lib BBANDS across all nine classical averages,
independent nonnegative upper/lower factors, both exponential seeds and suppression.
It aligns the center and population deviation with the current bar and publishes
three values plus presence flags. Wide intermediate arithmetic preserves finite
results; true published overflow is rejected.

The native reference separately preserves TA-Lib's uncentered variance cancellation
and overflow, nonfinite factor behavior, and MAMA misalignment when the deviation
window starts after the average. The period-80 regression proves that native middle
values retain the older packed prefix. Float/double, shifted ranges, aliases,
all methods, reset/chaining, extreme values and mutations have executable checks.

Ten focused tests and 1,589,676 output comparisons passed. Four timing arms were
recorded and the archive verifier passed 1,440 arms across 360 pairs. There are
42 pending families. Final complete correctness union and current-head gates
remain required before readiness.


## Variable-period classical averages

`VariablePeriodClassicAverage` covers TA-Lib MAVP for all nine classical average
methods. Its pure per-bar selector is truncated and clamped to ordered positive
limits. The maximum-period lookback fixes the publication boundary. Each selected
average aligns its seed to that boundary; first-price EMA begins at input zero,
while later DEMA/TEMA stages consume only their aligned predecessors. The same
internal alignment supports the existing classical average without changing its
default startup. History and engines are allocated only as observations and
selected periods arrive; revisiting a period replays its missed observations.

The selector sees a chained close and is called once per eligible update. Nonfinite
selections are rejected before advancing state. The owner defines period-one
identity and accepts arbitrary finite selections without narrowing overflow.
TA-Lib requires both limits at least two, rejects a periods array shorter than the
maximum lookback even for an empty result, and converts nonfinite selections
through `Int32.CreateTruncating`. Native shifted ranges, float/double arithmetic,
price/period aliases and overflow are preserved in independent regressions.

The grouped variable-period/classical-average suite passed 31 tests. MAVP passed
81,564 output comparisons; generic MA, APO, PPO and BBANDS passed 2,374,866 affected
comparisons in parallel. Four new timing arms passed. The full archived timing
union is 1,444 arms across 361 pairs, with no unmeasured paired family. The local
MAVP measurements retain the competitor's speed/allocation advantage; they do not
claim a speedup. Forty-one families, final correctness union and current-head
gates remain before readiness.


## Classical stochastic RSI

`ClassicStochasticRsi` covers TA-Lib STOCHRSI with all nine signal average methods,
independent RSI and signal suppression, and explicit exponential signal seeding.
It uses the verified zero-flat Wilder RSI and a complete RSI range, maps that range
to [0,100] with exact differences, and withholds both K and D until signal startup.
RSI remains causal when first-price exponential signal seeding is requested.
All period arithmetic is wide and histories grow lazily, including huge requests.

Separate native references preserve float/double rounding, shifted ranges and the
Metastock RSI defect: later seed observations and the unfilled zero tail affect
stochastic outputs. Direct regressions prove the final zero-tail output, native
failures for single-element intermediate buffers, underflow/overflow behavior,
and input/output aliases. Aliasing K and D together overwrites D with K in the
native API. The owner defines positive period-one requests and rejects invalid
configuration. Chaining, reset/lifecycle and value/mask/parameter mutations are
covered independently.

Fifteen focused tests and 256,928 output comparisons passed. Four new timing arms
passed; the archived union now verifies 1,448 arms across 362 pairs. Forty families,
the final complete correctness union and current-head gates remain before readiness.


## Window stochastic K/D/J

`WindowStochasticKdj` covers Skender's standard and extended stochastic APIs.
SMA smoothing waits for complete windows, while SMMA uses the first available
value to seed each Wilder recurrence. K publishes independently; D/J wait for the
signal. Configurable finite positive J factors combine in one exact expression.
Unpublished raw ratios and averages retain binary64 precision with extended upper
exponents so cancellation can produce finite published values. Period arithmetic
uses wide sums and history grows lazily.

The raw-range helper now accepts an internal flat value and exposes readiness;
its existing Trady default remains fifty. Independent native regressions retain
Skender's short-history SMMA exception, checked period-sum overflow, decimal
conversion, nonfinite J factors and staged product overflow. Both API overloads,
result aliases and reusable K, chaining, startup flags, true published overflow,
wide unpublished cancellation and mutations are checked.

The grouped suite passed 33 tests initially; its remaining test used a constant
close fixture with a nonzero range. Correcting that fixture to equal high/low/close
made the zero-range test pass, with unchanged production binaries. The new family
passed 99,067 output comparisons, and seven affected Trady pairs passed 160,828
further comparisons in parallel. Four new timing arms passed; the archive union
verifies 1,452 arms across 363 pairs. Thirty-nine families, the final correctness
union and current-head gates remain before readiness.


## Classical MACD with independent averages

`ClassicMacd` covers TA-Lib MACDEXT with independent fast, slow and signal
methods, aligned starts, compatibility seeds and suppression. Reversing periods
also reverses their associated methods. All three outputs publish together.
Wide internal rounded values preserve finite published results after hidden
overflow, including all nine signal methods. True published overflow is rejected.
The shared classical window backend retains exact rolling integer moments and
lazy histories. Public period-one identities remain supported.

Independent rational, integer-grid and native references cover all 729 method
combinations, reversed/equal periods, subranges, float/double buffers, aliases,
extreme/subnormal inputs, lifecycle, chaining and mutations. Native one-element
intermediate failures and nonfinite arithmetic remain explicit regressions.

All 121 grouped regression tests passed in 4 minutes 39 seconds. Sixteen
trajectory checks passed 3,549,354 values, including 575,577 for MACDEXT.
Four new timing arms and 24 refreshed arms for MA/MAVP/APO/PPO/BBANDS/STOCHRSI
passed the permanent performance verifier. The archive union now verifies
1,456 arms across 364 pairs, with none unmeasured. These short measurements
show substantial MACDEXT disadvantages against TA-Lib; no universal speedup
is claimed. Thirty-eight families, the final correctness union and current-head
gates remain before readiness.


## Classical fast and slow stochastic

`ClassicStochastic` supplies TA-Lib STOCH/STOCHF counterparts with independent
K/D classical averages; K period one selects fast stochastic. Complete high/low
windows use exact differences and zero for a flat range. Both outputs wait for
D startup. Wide rounded intermediates preserve finite cancellation even when
raw ratios exceed binary64; genuinely unrepresentable published values fail.
Compatibility seeds, suppression, period-one identities, lazy huge periods,
chaining and independent output presence are explicit.

Independent rational/grid/native references cover every K/D method combination,
float/double subranges, input aliases, extreme/subnormal ranges and mutations.
TA-Lib divides the range by 100 first, so underflow and overflowing differences
can produce zero or NaN where the owned exact ratio remains finite. Native
single-element intermediate buffers fail. If D aliases an input, native D
overwrites the shared temporary buffer before K is copied: K then contains a
mixture of shifted D and surviving intermediate values. In-place DEMA also
leaves an EMA1 tail. Dedicated alias regressions preserve those native defects;
sharing the two output arrays leaves K in their final shared buffer.

The initial grouped run passed 30 tests and failed one alias fixture that used
an exclusive endpoint instead of TA-Lib's inclusive input endpoint. Correcting
it exposed the native D/input overwrite and DEMA tail; the final alias test
passed after both were independently modeled. The 15 affected stochastic-RSI
tests also passed. Production binaries were unchanged during these corrections.
New trajectories passed 542,840 values, with 256,928 affected stochastic-RSI
values. Eight timing arms passed; the complete archived performance union now
verifies 1,464 arms across 366 pairs, with none unmeasured. Thirty-six families,
the final correctness union and current-head gates remain before readiness.


## Hilbert cycle period and phasor

`HilbertCyclePeriod` and `HilbertPhasor` cover TA-Lib HT_DCPERIOD and HT_PHASOR.
Their zero-seeded filters start at index twelve and publish at 32+suppression.
They share the extracted, bounded Hilbert stages with the existing adaptive
averages, retaining their rounding and zero-input conventions. Exact rounded
wide filter and homodyne stages preserve finite period controls after hidden
overflow. Only the selected published readings are subject to overflow rejection.

Rational and full-history integer-grid references independently cover the owned
formula. The native array model retains IEEE operation order, subrange reseeding,
float/double behavior, aliases and NaNs. Native lookbacks wrap with oversized
suppression; owned lookbacks use Int64 and fixed storage. Reset/lifecycle,
startup, chaining and value/presence/suppression mutations are checked.

All 123 grouped regression tests passed, including affected adaptive and
classical-average callers. Fourteen trajectory checks passed 3,979,103 values,
including 56,598 for the new families. Eight new timing arms and twelve refreshed
MAMA arms passed; the archived union verifies 1,472 arms across 368 pairs with
none unmeasured. Thirty-four families, the final correctness union and
current-head gates remain before readiness.


## Delayed Hilbert trendline

`DelayedHilbertTrendline` covers TA-Lib HT_TRENDLINE. Its Hilbert filters start
at index 37 and outputs publish at 63+suppression. The rounded smoothed period
selects a mean of closes, followed by a 4/3/2/1 weighted mean. Each mean rounds
once; wide filter intermediates keep final values finite at extreme prices.
History is bounded to fifty prices and three previous means.

Independent rational, integer-grid and native references verify the complete
formula. Native overflowing WMA/filter arithmetic can produce a NaN period,
which converts to a zero cycle length and yields zero trend means. Dedicated
regressions preserve that native result while the owned trendline retains a
constant maximum price. Float/double subranges, aliases, startup, huge
suppression, lifecycle, chaining and mutations are covered.

All 32 grouped tests passed. Six trajectory checks passed 167,104 values,
including 19,776 for the new pair. Four timing arms passed; the archived union
verifies 1,476 arms across 369 pairs, with none unmeasured. Thirty-three families,
the final correctness union and current-head gates remain before readiness.

## Delayed Hilbert phase, sine, and trend mode

`DelayedHilbertPhase`, `DelayedHilbertSine`, and `DelayedHilbertTrendMode`
complete TA-Lib's phase/sine/trend-mode families. They share the existing
index-37 Hilbert engine and publish from index 63 plus optional suppression.
Phase projections use rounded wide intermediates; trend mode retains the
sine crossing, phase-change, and 1.5% deviation rules with exact comparisons.
A fixed-size mean history is shared with `DelayedHilbertTrendline`.

Independent rational, integer-grid, and native IEEE references cover full outputs,
float/double subranges, aliases, zero and extreme inputs, lifecycle, chaining,
and output/configuration mutations. Native overflow and nonfinite results remain
explicit limitations in the manifest.

Verification: 35 grouped contract cases and seven affected trajectory pairs
passed (189,970 output comparisons). The permanent contract runner also passed
a real compiled-test smoke check and 17 script tests. Twelve new timing arms
and four refreshed trendline arms passed after correctness finished. The archive
verifies 1,488 arms across all 372 paired families, with none unmeasured. Thirty
families and final union/current-head gates remain before readiness.

## Early-start Hilbert trendline

`SeededHilbertTrendline` supplies Skender's early-start Hilbert trendline, smooth
price, and nullable rounded cycle length. The default input is the high/low
midpoint; an explicit close mode supports tuple/chained comparisons. Raw input
publishes through index 10, smoothing starts at index 6, and the weighted cycle
mean starts at index 11. The implementation reuses the bounded Hilbert filter
and weighted-mean histories. Independent rational and integer-grid references
cover rounded wide stages; a separate native IEEE reference preserves overflow
and null behavior.

Verification: 24 grouped contract cases passed, including reset/lifecycle,
wide/subnormal inputs, tuple/reusable routes, chaining, and all output/presence
mutations. Six affected trajectory pairs passed 193,354 output comparisons.
Four timing arms passed. The archive verifies 1,492 arms across all 373 paired
families, with none unmeasured. Twenty-nine families and the final correctness
union/current-head gates remain before readiness.

## Range and filtered-deviation adaptive averages

`RangeFractalAverage` compares with QuanTAlib FRAMA using prefix means, an
oldest-first split at floor(period/2), and the epsilon-adjusted range ratio.
The exact ratio rounds before Math.Log/Exp; gain is clamped to [0.01,1].
`FilteredDeviationAverage` compares with DSMA: first-price seeding, two-pole
filtering of price minus previous average, fixed-period RMS during startup,
and gain clamped to [0.1,1]. Its normalized magnitude rounds the complete
square-root ratio, retaining meaningful controls at extreme and subnormal prices.
Both round wide stages and convex updates and grow history lazily.

Independent rational/grid/native models cover every output. Native SIMD sum
order, IEEE overflow, DSMA NaN scale acceptance, and DSMA retaining RMS samples
after Init are explicitly verified. Ten grouped tests cover lifecycle/reset,
huge periods, extreme values, revisions, chaining, and mutations. Two trajectory
pairs checked 66,532 values; eight timing arms passed. The archive now verifies
1,500 arms across all 375 pairs. Twenty-seven families and final gates remain.

## ATR trailing stop and SuperTrend

`SeededAtrTrailingStop` shares the mean-seeded ATR engine across close, high/low,
and midpoint candidate bands. It supplies Skender ATR Stop and SuperTrend,
including all active-band outputs and presence flags. Midpoint initializes
direction against midpoint; the other bases compare against previous close.
Exact close equality to the active band selects the upper/bearish result.
Wide rounded candidates preserve finite selected stops even when an unpublished
band exceeds binary64. Native binary64 stages and final decimal casts are
modeled separately, including decimal overflow and NaN multiplier acceptance.

Ten grouped tests passed, including rational lifecycle checks, all bases, ties,
wide cancellation, lazy periods, chaining, and every output/mask/configuration
mutation. Two full-trajectory pairs checked 136,504 values.
Eight timing arms passed; all 377 paired families now have archived timings
(1,508 arms). Twenty-five families and final gates remain.

## Retrospective volatility stop and window Fisher transform

`VolatilityStopSnapshot` preserves prior-bar ATR, favorable-close extremes,
strict reversal comparisons, and all four nullable outputs. Its first guessed
trend is erased through the first reversal, but is retained when no reversal
occurs. Appending data can erase earlier rows; this behavior is explicitly a
snapshot in supplied order. Wide internal stops are validated only after
retrospective removal, so discarded rows do not cause false overflow rejection.

`WindowFisherTransform` publishes Fisher and its one-bar trigger from available
windows, with initial Fisher zero and absent trigger. Flat windows reset the
position state; values beyond +/-0.99 clamp to +/-0.999. Midpoints and range
ratios are safe at extreme prices; binary64 smoothing and Math.Log define the
transform. Both midpoint and close routes have independent references.

Eight contract tests cover lifecycle, exact snapshot goldens and prefix erasure,
huge periods, extreme/subnormal values, native NaN/null propagation, chaining,
and every output/mask mutation. Two trajectory pairs checked 131,890 values.
Eight timing arms passed; the archive verifies all 379 paired families
(1,516 arms). Twenty-three families and final gates remain before readiness.

## Retrospective regression channels

`RegressionChannelSnapshot` reuses the verified exact regression engine to fit
nonoverlapping blocks backward from the final observation. A leading incomplete
block is absent, while BreakPoint is always defined. Null period fits the entire
input and requires at least two observations. Appending input realigns blocks.
Centers round once from exact fitted positions; rounded population deviation
and wide channel arithmetic determine both bands.

Independent integer-moment and native binary64 references cover all outputs.
Skender's unused decimal final-line conversion is retained as a native limit: it
can reject large finite channels. Quote, tuple and reusable-result routes are
checked against their actual inputs, preserving decimal conversion differences.
Four tests passed after correcting route inputs and adding invalid whole-input
cases to the harness. Full trajectories checked 205,055 values; four timing arms
passed. The archive verifies 1,520 arms across all 380 paired families, with
none unmeasured. Twenty-two families and final gates remain.

## Rolling and calendar pivot levels

`PivotLevelSnapshots` publishes nine nullable levels for Standard, Camarilla,
DeMark, Fibonacci and Woodie formulas. Rolling windows support offsets;
calendar snapshots support hour, day, invariant week and month windows.
Full calendar identities prevent sparse dates from merging distinct windows.
Woodie uses the current window open; rolling DeMark uses current bar open,
while calendar DeMark uses the previous window open.

Independent rational formulas verify once-rounded owned results; a separate
staged decimal reference retains native conversion, scale and overflow behavior.
Five contract tests cover formula goldens, sparse dates, huge lookbacks,
wide/subnormal prices, rejected inputs and every output/presence mutation.
Both trajectories passed 542,247 comparisons. Eight timing arms are archived,
bringing the verified archive to 1,528 arms across all 382 paired families.

## Single-stochastic Schaff trend cycle

`SchaffTrendCycleSnapshot` implements the Skender single-stochastic convention:
independently mean-seeded fast/slow EMAs, a complete stochastic window on their
difference, then a three-value simple average. Flat windows contribute zero.
Wide unpublished MACD values remain representable internally; histories grow
with observed input. Native zero-cycle requests fail in the nested stochastic.

The batch passed 15 contract/shared-EMA tests, 18,827 trajectory comparisons,
and all four timing arms. Independent rational and staged-native references,
startup goldens, quote/tuple routes, prefixes, maximum periods, wide/subnormal
prices and output/mask mutations are covered. Archive: 1,532 arms /383 pairs.

## Connors strength and return ranking

`ConnorsStrengthSnapshot` publishes RSI, delayed streak RSI, percent rank and
Connors RSI with independent masks. The inclusive rank window includes the
initial zero return, excludes returns with nonpositive previous prices and
retains integer-truncated percentages. Exact return ordering distinguishes
values that native overflowing divisions collapse into ties. Existing Wilder
strength arithmetic supplies both RSI legs; the final component mean rounds once.

Five contract tests and 83,634 trajectory comparisons passed. All four timing
arms are archived (1,536 total arms /384 pairs). Goldens, startup, quote/tuple
routes, huge periods, wide/tiny/negative prices, repeated calls, causal prefixes
and every output/presence mutation are verified.

## Oldest-first Hilbert trendline convention

`OldestFirstHilbertTrendline` retains QuanTAlib Htit's oldest-first indexing.
Its current phasor is multiplied against itself, making the finite imaginary
discriminator zero. A full native phasor reference independently verifies the
price-independent period reduction used by the owned implementation. The first
ten outputs are input prices; subsequent means and 4/3/2/1 oldest-first weighting
round once. Unused native phasor overflow cannot poison the owned output.

Five contract tests and 15,428 trajectory comparisons passed, including lifecycle,
source selection, extreme/subnormal arithmetic, native retained-history reset
behavior and corruption. Four timing arms are archived (1,540 arms /385 pairs).

## Sample-deviation relative volatility

`RelativeVolatilitySnapshot` starts the previous close at zero, computes sample
deviations of up/down changes, averages each deviation over available history,
and publishes their bounded percentage ratio. Exact changes and wide rounded
intermediates prevent unpublished overflow. A pairwise-distance/root oracle
independently verifies production rolling moments; a separate native oracle
models scalar deviations and SIMD summation. Native underflow-to-zero and
retained nested buffers after reset are explicit regression cases.

Five tests, 15,689 trajectory comparisons and all four timing arms passed.
The archive contains 1,544 arms across all 386 currently paired families.

## Median-adaptive signed-threshold convention

`MedianAdaptiveSnapshot` preserves three raw prices, 1/2/2/1 price smoothing,
upper medians over descending-by-two windows, the signed median error denominator
and the minimum final length of three. Complete updates round once and exact
ratio comparisons retain threshold distinctions without intermediate overflow.
Finite signed thresholds and positive periods are supported; history grows lazily.

Five tests, 29,009 trajectory comparisons and four timing arms passed. Independent
order-statistic/rational and staged-native references cover odd/even periods,
negative/zero medians, threshold neighbors, extreme prices, huge periods, fresh
calls, prefixes and output/threshold mutations. Archive: 1,548 arms /387 pairs.

## Seeded ATR shared allocation reduction (timing pending)

`SeededAtrWindow` now reuses the existing compact exact true-range helper and a
cached smoothing coefficient instead of constructing padded minimum-unit integers
for every candle. Seed rounding, extended unpublished ranges, inverted candles,
reset and presence are unchanged. This shared window also serves ATR bands,
trailing/chandelier/volatility stops, directional measures and ATR Renko.

Five focused tests passed, including zero managed allocation for 4,000 ordinary
streaming updates after warmup. All 118 selected comparison tests passed across
the shared consumers. This is a state-allocation result, not a measured builder
latency win or direct competitor qualification. Timing remains pending. The new
`SharedSeededAtrBenchmarks` fixture covers Skender ATR, TA-Lib ATR/NATR and Trady
ATR at 1k/10k, with both builder history modes and native serial/parallel results.
Native workers replay full prefixes; Trady uses its public tuple path. Four setup
tests passed at 64/513/1k/10k, checking exact values and every presence flag against
independent native and builder references. All 53 inventory checks passed.


## Triangular CPU and GPU execution (timing pending)

`TriangularWindowAverage` now reuses the pilot's exact-sum certificate for its
unrounded box-sum convolution. Certified modern-.NET CPU states use double sums; rejection
promotes every retained sum before including the new value. A concrete typed
kernel uses P-1 replay bars in the shared bounded parallel loop. Reset, startup
zeros, signed-zero canonicalization and extreme-value fallback remain intact.

Required GPU uses mode 2 of the existing cached weighted-average kernel, through
AiDotNet.Tensors 0.134.4. Each 64-output tile seeds the triangular total and two
box sums, then updates them without intermediate series or intermediate rounding.
The existing weighted-mass certificate limits supported periods to 1..65535 and
rejects uncertified inputs after validating all raw bars. Auto remains CPU.

Validation passed 36 focused CPU/GPU tests (no skips), including WMA mode-cache
regressions, 10 independent comparison/setup tests and 52 inventory checks.
`SharedTriangularBenchmarks` includes both history modes, required-device GPU and
native serial/eight-worker paths for QuanTAlib.Trima and TaLib.Functions.Trima at
1k/10k. Timing qualification is pending a quiet machine; concurrent test processes
in other repositories prevented a reliable campaign. Neither API has been added
to the 176 qualified count, and no new latency/allocation win is claimed yet.

## Selected TA directional builder qualification

`PriorSeededDirectionalMeasure` skips unused movement/range streams and allocates
rating history only for ADXR. It retains decoded price factors and uses the existing
compact exact accumulator and `RocBankValue` for rounded stages. Exact dominance
is decided before rounding; seed sums round at their original boundary. Unpublished
DI values retain an extended upper exponent so DX/ADX can remain finite even when
publishing DI would overflow. Suppression, presence and DX's extra range update
remain unchanged. Nine focused unit tests and 23 independent comparison/setup
tests passed, including reset, subnormals, source composition and extreme DI ratios.

`SharedTaDirectionalBenchmarks` compares all seven APIs at 1k/10k against direct
packed native arrays. Parallel native workers replay complete prefixes because
bounded overlap changes recursive results. The original 56 cases and native rows
remain in `ta-directional-cpu`; `scaled-directional` holds the 28 updated builder
cases. Every updated case clearly improved, with 96.0-99.8% lower allocation.
At 10k, LatestOnly +DM fell from 11.55 ms / 15.95 MiB to 2.20 ms / 159.74 KiB;
ADX fell from 50.16 ms / 72.74 MiB to 7.68 ms / 169.64 KiB. Native serial remains
faster: +DM 87.21 us / 78.04 KiB; ADX 138.09 us / 77.89 KiB. All builder arms still
lose to native. The catalog selects the new builders and retains original natives.

Directional profiling can be replayed with the benchmark executable's
`--profile-directional Adx 10000 4000` arguments. It validates the existing
benchmark setup, warms 100 builds, then repeatedly builds and disposes LatestOnly
results. An EventPipe sampled-thread trace of commit `c0f54212` attributed 97.8%
of time beneath `ValuesBarExecution.Execute` to `Node.Append`, including 62.7%
in directional `State.Update` and 14.6% in `Direction`. Inlined arithmetic is
attributed to callers; these percentages do not establish dispatch-only cost.
The trace is diagnostic, not benchmark qualification. A subsequent quotient/remainder
rewrite passed 17 focused unit tests and 23 comparison tests, but its 14 timing
cases showed no win during concurrent work in another repository. That production
experiment was reverted; the qualified results above remain unchanged.

## Shared SMA builder qualification

`SharedSmaBenchmarks` compares Full/LatestOnly with native serial and parallel
QuanTAlib, Skender tuple, TA-Lib packed-array, and Trady tuple paths at 1k/10k.
An integer-grid oracle checks every published value, startup presence, packed
ranges, and worker replay. No native result mapping is timed. The 32-case
qualification and original native rows are preserved in `sma-cpu`.

The existing fused SMA dispatch now starts at 8,192 rather than 65,536 bars.
Its worker caps, certificates, fallback replay and arithmetic are unchanged.
All eight affected 10k builder cases clearly improved: Full 75-88 us versus
109-113 us before; LatestOnly 19-20 us versus about 52 us. LatestOnly allocation
increased from 81.10 to 81.68 KiB. Both modes beat QuanTAlib, Skender and Trady
at both measured sizes; TA-Lib remains faster and smaller (10k parallel 11.56 us,
78.45 KiB). The follow-up builder rows are in `sma-threshold`; 1k and native rows
remain selected from the original campaign. Validation passed 107 execution
tests, four comparison tests spanning seven sizes, and 50 inventory checks.

## Exact Fibonacci and oldest-first sinc kernels

`FibonacciWeightedSnapshot` normalizes exact integer Fibonacci weights over the
available newest-first prefix, including period-one identity and kernels beyond
native floating overflow. `WindowedSincSnapshot` implements Afirma's published
response for all five windows, extending startup with the newest price. Its
unused cubic calculation is omitted. Undefined kernels are rejected explicitly;
complete weighted ratios round once. Kernel storage scales with period/taps.

`FibonacciWeightedAverage` adds the builder route with two exact running sums,
using F(k-2)=F(k)-F(k-1) and explicit removal of the expired term. The normalized
prefix mass is retained after warmup. Bounded CPU workers replay period-1 bars;
Required GPU rejects this indicator. The independent doubling-identity reference,
long-period eviction/reset, extreme binary64 values, and worker boundaries are
covered. In the eight-arm `fibonacci-cpu` campaign, both builder modes beat native
serial and parallel at 1k/10k. At 10k, Full/LatestOnly measured 201.38/186.27 us and
557.99/88.23 KiB versus native parallel 295.31 us and 2583.04 KiB. The LatestOnly
99.9% interval was wide (144.74-227.80 us), still below native's 289.85-300.78 us.

`WindowedSinc` adds the shared CPU builder path for Afirma. The snapshot and
builder reuse coefficient generation; decoded products and the exact denominator
are shared across workers. Private price rings retain oldest-first order and
repeat the newest value through missing startup taps. GPU remains unsupported.
Fourteen unit/registration tests, 15 independent comparison/setup tests, and eight
timing cases passed. At 10k, Full/LatestOnly measured 1449.94/1382.38 us with
4803.99/4334.23 KiB, versus native parallel 525.49 us and 481.80 KiB. Both builder
modes beat native serial latency but lose to parallel and allocate substantially
more. This is a qualified rollout path, not a performance win; wide exact-product
accumulation remains an optimization target.

Eight tests, 95,728 trajectory comparisons and eight timing arms passed.
The archive verifier passed 1,556 arms across all 389 paired families.

## Klinger volume and retrospective pivot trends

`KlingerVolumeSnapshot` preserves two-bar force initialization and independent
fast/slow/signal EMA startup. Exact direction/range calculations and wide rounded
force/EMA stages reject only published overflow. `PivotTrendSnapshot` reuses strict
fractals and connects unequal anchors within the maximum distance, preserving
future confirmation, all six masks, equal-anchor resets and retrospective trends.

Ten tests, 76,536 trajectory comparisons and eight timing arms passed, including
native staged references, extreme inputs, maximum periods/spans and mutations.
The archive verifier passed 1,564 arms across all 391 paired families.


## Parabolic stop conventions

Four public snapshot routes cover TA classic/extended, Skender confirmed and Trady five-bar initialization. Exact comparisons and wide once-rounded updates preserve finite stops through overflowing intermediate ranges. The confirmed route removes the guessed trend through its first reversal. Trady 3.2.8 replays and overwrites the preceding cached transition with already advanced state; its native reference models that defect, while our FourBar recurrence evaluates each transition once. Native decimal and double-stage values have separate references.

Verification: 13 contract/regression/mutation tests, 80710 full-output trajectory values and 16 timing arms passed. The archive verifier passed 1,580 arms across 395 pairs. Seven families remain pending.


## Completed competitor inventory

All 421 reflected API families now have an explicit disposition: 401 executable
pairs, 16 unavailable native implementations with independently verified owned
counterparts, and four non-indicator utilities. The contract suite rejects any
pending family. Trady ZigZag is an executable-probed throwing stub and maps to
our Skender-convention counterpart; it has no native performance result.

The final six pairs add nullable volatility-selected fixed-period RSI, both Hurst
exponents, Jurik adaptive smoothing, ZigZag with pivot types and both retracement
lines, and fixed/last-ATR Renko charts. Thirty-eight focused tests passed, along
with 403,589 full-output trajectory comparisons. References separately model
native decimal/binary64 stages and owned numerical conventions. Hurst uses an
explicit 1e-10 absolute/relative comparison budget for its independent gamma
and regression formulations; other final pairs require exact output agreement
with their own references.

Renko output is a lazy sequence of complete bricks, including exact timestamp
words, OHLCV and direction. Multiple bricks share a source candle and its evenly
allocated accumulated volume. Its binary64 brick size and baseline precision are
explicitly documented; native decimal brick-size conversion has a separate
reference. ATR-sized Renko and ZigZag repaint when future bars arrive.


Final correctness verification (2026-10-07): all 401 paired families passed the
20-shard complete-union check, covering 17,338,914 output values. All 3,004
contract cases across 1,042 discovered test methods passed. The Python evidence
validators passed 17 tests. Two inventory registration failures were corrected
and their affected contract shards rerun successfully. No pending family or
unresolved review thread remained in the current review snapshot.

The correctness job retains 20 parallel shards and allows 45 minutes per shard
because a shard can contain multiple quadratic native Trady implementations.
This changes the timeout ceiling, not the checks or measured workload.


Final performance verification (2026-10-07): the archive contains all 1,604
measurement arms across 401 pairs, with no unmeasured paired family. Dynamic
Momentum Index completed last. Builds and correctness workers were stopped
before the timing run. Results are machine-specific observations, with the
BenchmarkDotNet environment, sample errors and allocation measurements retained
in the archive.


## Trady directional allocation reduction (timing pending)

`WindowDirectionalMeasure` now retains compact rounded movement stages and reuses
the existing `JurikDyadic` exact arithmetic for weighted recurrence and DI/DX
ratios. Raw movements skip ATR state; single-direction outputs skip the unused
smoothing stream; rating history exists only for a positive rating lag.
Extended unpublished stages, strict exact dominance, early ADX seeding and
missing-value propagation retain their original rounding/presence contracts.

The net10.0 production build, 17 focused unit tests and 47 directional comparison
tests passed. All seven measures allocate zero managed bytes across 4,000 ordinary
streaming updates after 1,000 warmups in the regression dataset. Independent
reference checks cover reset, extremes/subnormals, exact ties, periods 1/3/20 and
int.MaxValue, and lags 0/2. This is a state-allocation result, not an end-to-end
builder latency claim. Consolidated timing remains deferred; qualification stays
at 176/401 APIs.


## Certified log-return fixed-width execution (timing pending)

`CertifiedLogReturn` now evaluates its directed 96-bit atanh interval with fixed
integer limbs on modern runtimes. Exact mantissa normalization, directed products,
and a geometric tail bound preserve the certificate: both endpoints must round
to the same binary64 value. Unresolved intervals use the existing adaptive
arbitrary-precision path; .NET Framework retains that path throughout.

The net10.0 production build, six focused log-return tests and ten logarithmic
volatility comparison tests passed. Checks cover adjacent prices, signed inputs,
subnormals, exponent extremes, reduction boundaries and explicit fallback. The
ordinary-return regression dataset allocates zero managed bytes over 4,000 calls
after 1,000 warmups. This does not include rolling moments or builder output
storage, and no latency claim or qualification increase is made pending timing.


## Compact logarithmic-volatility rolling moments (timing pending)

`WindowLogVolatility` reuses the existing exact dyadic representation for rolling
sums and squares. Ordinary updates fit fixed-width integers; larger exponent
spans and products retain arbitrary-precision fallback. Realized volatility skips
the unused running sum. Publication retains exact annualization and the existing
certified square root, and shared root bit-length checks no longer allocate a
byte array on modern runtimes.

The net10.0 production build, 22 focused unit tests and ten volatility comparison
tests passed. Ordinary rolling-moment add/remove operations allocate zero managed
bytes over 4,000 updates after warmup in the regression dataset. This excludes
square-root publication, which can still allocate. Exact-reference tests cover
wide cancellation, subnormal roots, copied state, missing returns, reset and
irreversible realized-volatility poisoning. Timing qualification remains unchanged.

The shared integer-root helper now uses fixed-width Newton iterations when its
input fits 128 bits, retaining arbitrary precision for larger values. This
follow-up passed 91 focused unit tests and ten volatility comparison tests. Its
iterations allocate no scratch objects over 4,000 warmed-up calls; conversion
and final midpoint arithmetic can still allocate. Latency remains unmeasured.


## Allocation-only observations after shared CPU updates

`--alloc-shared <operation> 10000 5` reuses the existing independently validated
benchmark methods, prebuilt inputs and native serial/parallel implementations.
It warms each arm ten times, counts managed allocations across all process threads,
and retains each result through the sample before disposing it outside the sample.
No timing is collected. Seven probe tests passed, including worker allocation
accounting and result disposal; all five 10k fixture setups verified their outputs.

Median KiB per call (five samples; EMA commit `24fc7e30`, volatility commit `e45827ba`):

| Operation | Builder Full | Builder LatestOnly | Native serial | Native parallel |
|---|---:|---:|---:|---:|
| Quan DEMA | 550.96 | 81.21 | 469.06 | 1838.43 |
| Quan TEMA | 551.01 | 81.26 | 469.08 | 1838.55 |
| Quan QEMA | 850.45 | 380.70 | 2033.41 | 8884.43 |
| Quan Historical | 630.22 | 160.47 | 4200.43 | 4211.11 |
| Quan Realized | 630.22 | 160.47 | 469.39 | 1840.87 |

Full retains additional OHLCV history; realized volatility Full still exceeds
native serial allocation. LatestOnly now allocates less than both native arms
for all five operations. The original samples, including observed variation,
remain in `results/shared-rollout/allocation-probes/recent-cpu.json`; the updated
volatility samples are in `results/shared-rollout/allocation-probes/root-publication.json`.
The compact root publication path passed 15 focused unit tests and ten volatility
comparison tests, including rounding ties, subnormals, overflow and wide fallback.
These allocation-only observations do not replace BenchmarkDotNet timing evidence
or raise the 176/401 direct qualification count. The targeted queued volatility
campaign selects `*SharedLogVolatilityBenchmarks.BuilderLatestOnly(*Count: 10000)`
and `*SharedLogVolatilityBenchmarks.Native*(*Count: 10000)` (six cases), avoiding
unnecessary reruns of unrelated sizes and methods.


## Sum-seeded directional allocation follow-up

The Skender-compatible sum-seeded directional engine now uses the existing compact exact dyadic arithmetic for movement selection, smoothed sums, DI/DX and ADX. Extended unpublished values, once-rounded recurrences, missed seeds and period-delayed rating presence are preserved. Five focused unit tests and ten independent comparison tests passed, including bitwise reference/reset checks over ordinary, extreme and mixed-exponent inputs. After warmup, 4,000 ordinary state updates allocate zero managed bytes. Production and benchmark builds passed. This is state-level allocation evidence, not an end-to-end timing win; the timing qualification count remains 176/401.


## Return-beta compact rolling moments

Return beta now reuses compact exact products and rolling sums, with arbitrary-precision fallback for wide exponent spans. Both scalar and full-statistics APIs preserve bitwise reference outputs, selection/presence rules, reset and extended unpublished returns. Production/benchmark builds, six focused unit tests, 21 comparison/probe tests and 53 inventory checks passed. Ordinary warmed-up rolling updates allocate zero bytes over 4,000 updates; this excludes beta publication.

An allocation-only probe of the existing 10k beta benchmark fixture measured median Full 7,452,568 bytes, LatestOnly 6,971,544 bytes, TA-Lib serial 79,896 bytes and TA-Lib parallel 80,280 bytes (five samples per arm). LatestOnly is below the archived 16,408,312-byte measurement but remains far above native allocation; publication still falls back to arbitrary precision. Original samples are in `results/shared-rollout/allocation-probes/beta-moments.json`. No latency qualification or win is claimed; 176/401 APIs remain timing-qualified.


## Compact ratio publication follow-up

The existing compact dyadic ratio helper now computes rounding-grid quotient bits with bounded remainder arithmetic when shifting the numerator would exceed 128 bits. This avoids arbitrary-precision scratch allocations while preserving exact ties, signed underflow and overflow. Production/benchmark builds, 26 focused unit tests and 60 beta/directional comparison tests passed. Complete scalar beta updates, including publication, allocate zero bytes over 4,000 warmed-up updates on the benchmark price sequence.

Five allocation-only samples at 10k bars were identical: Full 647,080 bytes, LatestOnly 166,056 bytes, TA-Lib serial 79,896 bytes and parallel 80,280 bytes. LatestOnly dropped from the preceding 6,971,544-byte median to 162.16 KiB, but still loses allocation to native serial (78.02 KiB). Wide operands retain the exact fallback. Raw evidence: `results/shared-rollout/allocation-probes/beta-publication.json`. Latency remains unmeasured after this change; qualification stays 176/401.


## Seeded true-strength allocation follow-up

The Skender-compatible true-strength engine now retains signed/absolute changes, both EMA stages and the signal in existing compact exact arithmetic, with wide-value fallback. Once-rounded extended values, the shortened smoothing-one startup, absent signal-one output, poisoned missing signals and reset behavior remain bitwise equivalent to the independent rational reference. Production/benchmark builds and all 18 focused unit/comparison tests passed. On ordinary prices, 4,000 state updates including startup after reset allocate zero managed bytes. This is state-level allocation evidence; no new end-to-end timing or competitor win is claimed, and qualification remains 176/401.


## Seeded price-momentum allocation follow-up

The Skender-compatible PMO engine now uses compact exact arithmetic for percent returns, both smoothing stages, the tenfold scaling boundary and the signal. Once-rounded extended values, smoothing-one overshoot, zero-prior-price poisoning, output presence and reset remain bitwise equivalent to the independent rational reference. Production/benchmark builds, seven focused unit tests and nine seeded-momentum comparison tests passed. On ordinary prices, 4,000 state updates including startup after reset allocate zero managed bytes. End-to-end latency and allocation qualification remain pending; this does not change the 176/401 timing-qualified count.


## Shared strength publication allocation follow-up

Nullable and Wilder strength/momentum formulas now reuse compact exact ratios for quotient and percentage publication. Common exponents cancel before publication, preserving unpublished means below the binary64 floor and retaining wide-value fallback. Production/benchmark builds, three focused arithmetic tests (including 1,000 rational-reference cases) and 30 nullable/Wilder comparison tests passed. Ordinary quotient, unsigned percentage and signed percentage publication allocates zero bytes across 4,000 repetitions. Gain/loss smoothing and input differences still allocate; this is not a zero-allocation state or an end-to-end performance claim. Timing qualification remains 176/401.


## Shared strength smoothing allocation follow-up

Positive strength means now retain their 53-bit mantissa in a long and use compact exact arithmetic for ordinary smoothing at a relative exponent. Certified exact price differences avoid minimum-unit padding after seeding. Large exponent gaps retain the established wide recurrence; positive means below the binary64 floor remain alive. All five nullable strength/momentum and three Wilder conventions allocate zero bytes across 4,000 ordinary updates after seeding. Startup still uses the existing exact seed accumulator.

Production/benchmark builds, 19 focused arithmetic/allocation tests and 30 independent nullable/Wilder comparison tests passed. Differential tests cover both recurrence types, periods through int.MaxValue, and unpublished exponents from -5000 through +5000. End-to-end timing and allocation qualification remain pending; the timing-qualified count stays 176/401.


## Direct Wilder-strength allocation evidence

A direct RSI/CMO fixture now measures both builder history modes against TA-Lib serial and eight-worker prefix replay. Setup validates complete builder values/presence against the independent reference and requires native parallel to match native serial bitwise. The benchmark build, 14 setup/probe tests and 54 inventory checks passed. Production code was unchanged.

At 10k bars, both RSI and CMO allocation probes measured medians of Full 664,680 bytes, LatestOnly 183,656 bytes, native serial 79,896 bytes and native parallel 439,736 bytes. Five samples per arm, including small runtime-allocation variation, are preserved in `results/shared-rollout/allocation-probes/wilder-strength.json`. LatestOnly (179.35 KiB) still loses allocation to native serial (78.02 KiB); startup and retained outputs remain costs. No timing win is claimed.

The six-case volatility timing attempt began after an idle preflight but a new AiDotNet build overlapped it. All six rows completed, but none enter qualification. The rejected report and reason are retained in `results/shared-rollout/rejected/volatility-08640579.json.gz`. Timing qualification remains 176/401.


## Shared strength seed allocation follow-up

Nullable/Wilder strength seed differences now accumulate on natural binary scales. Normalizing before rounding preserves means below the binary64 floor without padding each input to minimum-subnormal units. All eight state variants allocate zero bytes over 4,000 ordinary updates including startup after reset. Production/benchmark builds, 23 focused unit tests and 34 independent comparison/setup tests passed.

Five allocation-only samples for each 10k RSI/CMO arm were identical: Full 644,296 bytes, LatestOnly 163,272 bytes, native serial 79,896 bytes and native parallel 439,736 bytes. LatestOnly dropped from 179.35 to 159.45 KiB but remains above native serial (78.02 KiB). Raw samples are in `results/shared-rollout/allocation-probes/wilder-strength-seed.json`. No new timing qualification is claimed; coverage remains 176/401.


## Shared strength integer recurrence follow-up

Ordinary positive-strength weighted sums now use a guarded signed-64-bit path and the existing exact ratio rounder, avoiding repeated general dyadic construction when the complete numerator fits. Conservative product/shift bounds and a separate addition check retain the wider path otherwise. Production/benchmark builds, 24 focused arithmetic/allocation tests and 34 nullable/Wilder comparison/setup tests passed. Product, shift and sum boundary cases match the wide recurrence. State allocation remains zero for the tested ordinary updates including reset startup. Latency benefit is not yet measured; no timing qualification count or competitor-win claim changes.


## Seeded ATR trailing-band allocation follow-up

The seeded ATR trailing-stop layer now keeps rounded widths and candidate bands in compact exact arithmetic instead of padding them to minimum-subnormal units. Exact comparison signs preserve ties and reversals; midpoint arithmetic is skipped for the other basis modes. Production/benchmark builds, eight focused unit tests and ten independent comparison tests passed. All three price-basis modes allocate zero bytes over 4,000 ordinary state updates including startup after reset, while extreme unpublished bands retain the wide fallback. End-to-end timing and allocation qualification remain pending; coverage stays 176/401.


### RSI timing and guarded strength arithmetic

The completed four-arm 10k RSI campaign on production commit `3d07822e` measured Full 3,562.19 us / 644,383 bytes, LatestOnly 3,409.84 us / 163,518 bytes, TA-Lib serial 126.24 us / 79,903 bytes, and TA-Lib parallel 179.09 us / 439,744 bytes. LatestOnly remains about 27 times slower than the fastest native arm. Direct timing qualification is now **177/401 APIs**, not a win count. Raw results and machine observations are in `results/shared-rollout/wilder-strength-cpu/`.

One unprivileged sampled-thread-time trace located active work in `ValuesBarExecution.Node.Append`; waiting worker threads dominate the overall totals. A JIT disassembly check confirmed that the 11,801-byte Tier1 method includes inlined RSI arithmetic, so this does not isolate dispatch overhead. The profile context and compressed disassembly are retained beside the timing evidence.

The subsequent shared strength helper change certifies same-sign, nearby-price subtraction directly and uses guarded integer percentage numerators and denominators when aligned operands fit 56 bits. Both retain exact fallback, unpublished extended exponents and once-rounded publication. These paths serve nullable and Wilder strength indicators. The archived timing predates these guards; no post-change speedup is claimed.

Validation: net10.0 production and benchmark builds passed; all 26 focused strength unit tests, 34 nullable/Wilder comparison and fixture tests, and 54 inventory checks passed. The profiling CLI smoke check passed. Existing ordinary-state allocation assertions still pass. PR #255 has five review threads, all resolved.


### Shared compact quotient/remainder execution

Compact mean and dyadic ratio publication now share one guarded quotient/remainder operation. The x64 path uses the processor's 128-by-64 DIV when the divisor fits 64 bits and the numerator's upper limb is smaller than the divisor; this proves the quotient fits and excludes a zero divisor. Wider quotients/divisors and other CPUs retain `UInt128.DivRem`. Dyadic ratios also stop calculating `/` and `%` separately. Rounding, signed zero and overflow behavior are unchanged. This reuses the existing arithmetic across strength, directional, beta and other shared consumers.

The net10.0 production/benchmark builds, 38 focused unit tests and 59 strength/directional/beta comparison tests passed. All 12 primitive/rounding tests also passed with `DOTNET_EnableHWIntrinsic=0`, using the same binaries. Integer-reference tests cover 4,000 random divisions and hardware overflow boundaries; existing rational-reference and allocation assertions passed. A JIT-only RSI replay confirmed one DIV instruction in the guarded Tier1 path (`results/shared-rollout/arithmetic/compact-divrem-disasm.txt.gz`). No timing campaign was run and no latency win is claimed; direct timing qualification remains 177/401 APIs.


### RSI two-arm checkpoint and direct recurrence mantissas

A focused checkpoint on production commit `1f92c82f`, after three shared-arithmetic batches, measured 10k RSI LatestOnly at **1,762.89 us / 163,313 bytes**, versus TA-Lib serial **127.48 us / 79,904 bytes**. LatestOnly is about 48.3% faster than the preceding 3,409.84 us campaign on `3d07822e`, while native timing stayed close to its prior 126.24 us. The remaining latency loss is about 13.8 times and allocation about 2.04 times native. This measures the combined batches; it does not attribute improvement to an individual change. Confidence intervals were 1,708.37-1,817.42 us for the builder and 125.90-129.06 us for native. Both rows completed with statistics; preflight/completion process checks found no other dotnet builds/tests. The 11-second run finished before a separate mid-run sample. The two-arm report is retained as diagnostic evidence in `results/shared-rollout/wilder-strength-cpu/rsi-two-arm-checkpoint.json.gz`; it does not replace four-arm qualification or raise the 177/401 count.

The subsequent strength recurrence change stores the once-rounded integer quotient directly as the normalized 53-bit mantissa and extended exponent, removing an intermediate binary64 encode/decode. Exact ties-to-even, mantissa carry, small-dividend 128-bit rounding and the existing wide/.NET Framework fallback are preserved. Production/benchmark builds, 29 focused unit tests and 34 nullable/Wilder comparison/setup tests passed. Ordinary-state allocation assertions still pass. This subsequent change has not been timed.


### Shared Hilbert phase-filter allocation rollout

The shared Hilbert phase filter now retains price/filter histories, FIR stages, in-phase/quadrature readings and homodyne products in the existing compact exact dyadic representation. Every logical rounding boundary is preserved: the FIR sum before correction, corrected FIR output, difference/sum before blending, and homodyne product sum before blending. Wider operands retain the exact fallback; zero-phase/period retention and extended unpublished upper exponents are unchanged. Exact unit import/export bridges preserve compatibility with consumers still using padded units. Cycle period/phasor use direct binary64 inputs and publications.

This shared filter feeds ten competitor APIs across cycle period/phasor, phase/sine/trend signals, seeded/delayed trendlines and seeded/delayed MAMA/FAMA. The production and benchmark builds, nine focused arithmetic/allocation tests and 48 independent comparison tests passed. All four startup/control configurations allocate zero managed bytes over 4,000 ordinary phase-filter updates, including startup after reset. Bridge tests preserve exact imported bits through cancellation, wide values, subnormal ties and extended-range publication. Signal projections, trend averaging and adaptive-average layers still have padded-unit costs; these are not complete allocation-free pipelines. No new end-to-end timing or competitor win is claimed, and timing qualification remains 177/401 APIs.


### Hilbert signal projections and trend means

Hilbert phase/sine/trend projections, rolling trend means, weighted trend smoothing and seeded trendline midpoints now reuse compact exact arithmetic. Projection coefficients for the bounded 0-50-bar windows are cached with the original binary64 angle operation order. Tiny near-axis terms are accumulated separately and added exactly before the single final rounding; no term is discarded. Trend deviation comparisons preserve the exact 1.5% threshold, and wide inputs retain the fallback. Shared compact mean publication now divides without rebuilding an arbitrary-precision accumulator and preserves extended rounded means.

Production/benchmark builds, all 17 focused unit tests and 69 independent Hilbert/phase/directional comparison tests passed. Delayed and both seeded trendline state configurations allocate zero bytes over 4,000 ordinary updates including startup after reset. Phase, sine and trend signal states each allocate 2,874,880 bytes over that workload; wide projection tails remain. These are state-only observations, not builder/native measurements. Evidence is in `results/shared-rollout/allocation-probes/hilbert-signals.json`. No new timing campaign or competitor win is claimed; timing qualification remains 177/401 APIs.


### Compact phase-adaptive MAMA/FAMA state

Seeded and delayed MAMA/FAMA now retain their seed and both adaptive averages in compact exact arithmetic and accept ordinary closes/midpoints without padded-unit conversion. The update uses the existing certified fused blend when possible, otherwise exact `old + alpha * (new - old)` followed by the original stage rounding. This preserves the exact complement of alpha, including gains where binary64 `1 - alpha` would be rounded. Wide composition still imports/exports exact rounded units at the existing interface boundary.

Production/benchmark builds, nine focused unit tests and 31 independent seeded/delayed phase, classical-average and wide MACD-composition tests passed. All six ordinary configuration probes allocate zero managed bytes over 4,000 state updates including startup after reset. Constant-gain tests compare both stages with the original integer recurrence at alpha 0.1, 1 and the minimum subnormal, including unpublished values above binary64 range. Unit import/export for wide composition can still allocate. No new end-to-end timing or competitor win is claimed; qualification remains 177/401 APIs.


### Direct Hilbert builder/native fixture and first timing qualification

`SharedHilbertBenchmarks` now measures cycle period, phasor and MAMA/FAMA through Full/LatestOnly `BuildAsync()` and direct TA-Lib serial/full-prefix parallel calls. Native APIs write all mature outputs into disjoint spans of one retained packed array; no full-length expansion or nullable adapter is timed. Setup checks native outputs bitwise against an independent native-stage model, both builder histories bitwise against the exact reference, every presence column, and native prefix replay. Minimum 33-bar and uneven 65-bar cases, suppressed settings, and allocation-probe integration passed. The benchmark build, all 22 fixture/probe tests and 55 inventory checks passed; unchanged production binaries are from `7a518455`.

Five allocation-only 10k samples per arm produced these medians (bytes):

| API | Full | LatestOnly | Native serial | Native parallel |
|---|---:|---:|---:|---:|
| HtDcPeriod | 646,000 | 164,976 | 80,176 | 442,232 |
| HtPhasor | 806,408 | 325,384 | 159,920 | 880,824 |
| Mama | 806,608 | 325,584 | 159,920 | 880,824 |

Raw samples, including occasional larger observations, are in `results/shared-rollout/allocation-probes/hilbert-native.json`. Builder value/presence columns still cost about twice native packed output storage. State-level zero allocation does not mean zero allocation per build.

One four-arm 10k HtDcPeriod timing run completed after an idle preflight, with no other dotnet build/test workloads observed before, during or after it. Means were Full 31,806.57 us / 646,124 bytes, LatestOnly 31,654.61 us / 166,808 bytes, native serial 972.22 us / 80,236 bytes, and native parallel 1,026.52 us / 442,292 bytes. LatestOnly remains about 32.6 times slower than native serial. The small Full/LatestOnly latency difference shows history retention is not the main latency cost. BDN timing/allocation observations are distinct from the allocation-only probe above. Raw timing/context are in `results/shared-rollout/hilbert-cpu/period-10k*`; direct timing qualification rises to **178/401 APIs**, not a win count. Phasor/MAMA timings remain pending.


### Certified Hilbert arithmetic and circular histories (2026-10-11)

The prior 10k cycle-period campaign exposed a 32.6-times LatestOnly/native-serial latency loss. An unprivileged sampled-thread-time trace identified FIR arithmetic, exact products/rounding and history copying among active work; waiting threads dominate its totals. `results/shared-rollout/hilbert-cpu/phase-trace-summary.json` records the command, observations and attribution limits. Append includes inlined arithmetic and is not solely dispatch overhead.

HilbertRoundedKernel now certifies local two-product sums using FMA residuals and error-free sums, falling back to existing exact dyadic arithmetic when any certificate fails. Bounded product exponents prevent lost underflow residuals. FIR differences must be exact, and the sum still rounds before the correction product. Blends retain both literal binary64 coefficients .2 and .8. Smoothing divides only after certifying the weighted total is exact. Circular histories preserve all lags while removing per-bar struct shifts. This is a local CPU arithmetic change, not the broader pending compiler or GPU rollout.

Adversarial review covered overflow/underflow guards, cancellation, rounding ties, stage boundaries, canonical dyadic zero, and history cursor/reset ordering. Independent integer-reference tests cover accepted and rejected exponent ranges, ordinary price-scale blends, ties and subnormals. The first coverage assertion exposed deliberately conservative rejection for unrelated exponent scales; representative ordinary blends are now also tested, with an explicit acceptance floor. No approximate result is accepted.

Validation: net10.0 production and benchmark builds passed; 23 focused arithmetic/allocation tests and 48 independent Hilbert/phase comparison tests passed. Initial allocation checks exposed tier-transition sensitivity (8080/8136 bytes); all 23 passed with tiering disabled, then passed normally after increasing warmup to three reset/replays. Zero-allocation assertions and measured startup/reset coverage remain unchanged. Commands: `dotnet build src/OoplesFinance.StockIndicators.csproj -c Release -f net10.0 --no-restore`; benchmark build with `-f net10.0 --no-restore -p:BuildProjectReferences=false`; unit and comparison `dotnet test -c Release --no-restore -p:BuildProjectReferences=false` filtered respectively to HilbertRoundedKernelTests/CompactHilbertPhaseTests/CompactPhaseAdaptiveTests and the six affected Hilbert/phase comparison classes. Logs: `C:/Users/cheat/temp/si-hilbert-certified-*`. No new latency claim follows from these checks.


### Hilbert certified-kernel checkpoint and tighter certificates (2026-10-11)

One four-arm 10k cycle-period run on production `01164679` completed with 20 warmups and 15 requested measurement iterations. Raw report: `results/shared-rollout/hilbert-cpu/period-10k-certified.json.gz`; source/fixture/contention context is adjacent. Preflight CPU was 2%, completion 6%; process samples before, during and after found no competing dotnet build/test workload. This was sampled observation, not a continuous isolation guarantee.

| Arm | Mean ns | CI lower / upper ns | BDN allocation bytes |
|---|---:|---:|---:|
| Full | 16264460.58 | 16129162.28 / 16399758.88 | 646132 |
| LatestOnly | 16586580.61 | 16100995.70 / 17072165.52 | 166182 |
| Native serial | 966709.11 | 957141.48 / 976276.75 | 80236 |
| Native parallel | 1045051.82 | 1030252.20 / 1059851.45 | 442295 |

LatestOnly improved about 47.6% from the preceding 31.65 ms campaign but remains 17.2 times slower than native serial and uses 2.07 times its managed allocation. The inventory replaces only HtDcPeriod's current evidence; timing-qualified coverage remains 178/401 APIs, not a win count. The original report remains archived.

The subsequent arithmetic follow-up is NOT included in those timings. When residual sums are inexact, it now bounds all omitted terms outward and accepts only when the entire exact-result enclosure lies strictly inside the candidate's rounding cell. The original product exponent bounds prevent lost underflow residuals. Uncertain midpoints still fall back to exact arithmetic; exact tails preserve ties-to-even directly. Both final phase additions now use the existing rounded SumProduct primitive, retaining the prior stage boundaries and wide fallback. The ordinary blend regression now requires over 5900 of 6000 cases to certify, all checked against independent integer arithmetic, plus explicit midpoint rejection and separated-residual cases.

Production/benchmark net10.0 builds, 24 focused arithmetic/allocation tests, 48 independent Hilbert/phase comparison tests and 55 inventory checks passed. Commands follow the preceding batch with unchanged build-reference restrictions and test filters. Logs: `C:/Users/cheat/temp/si-hilbert-cell-*`.


### Borrowed Hilbert arithmetic operands (2026-10-11)

Hilbert FIR, smoothing, blend and homodyne helpers now take unchanged arithmetic inputs by readonly reference, including circular-history entries. TryDouble and its bitwise conversion helper are readonly, so those reads do not require defensive struct copies. Stage backing fields allow internal references while published internal properties continue to return value snapshots. Mutable exact fallback products explicitly copy operands before updating them. The shared JurikDyadic.SumProduct primitive uses the same ownership rule, benefiting its Hilbert and Jurik callers.

Adversarial review checked reference aliasing, fallback mutation, history writes after reads, and reset coverage. Ratio can promote its receiver, so the phase-control calculation retains its prior value-property snapshot. A new aliased-operand test covers compact, simple, subnormal and wide extended inputs; it checks unrounded operand differences as well as exact product-sum results, preventing hidden low-bit mutation. No rounding rules, output shape or allocation assertions changed. No timing campaign was run for this batch; the latest measured 16.59 ms LatestOnly result still identifies production 01164679.

Validation: final net10.0 production/benchmark builds, 31 focused arithmetic/allocation tests and 54 independent Hilbert/phase/Jurik comparisons passed. Production used `dotnet build src/OoplesFinance.StockIndicators.csproj -c Release -f net10.0 --no-restore`; benchmark build and test projects used `-p:BuildProjectReferences=false`. Unit filters added CompactProductArithmeticTests and CompactRatioArithmeticTests to the preceding Hilbert filters; comparison filters added JurikComparisonTests. Logs: `C:/Users/cheat/temp/si-hilbert-borrow-*`, with final build/unit logs explicitly suffixed `-final`.


### Compact window-relative Ulcer Index (2026-10-11)

The archived normalized-adapter report for Skender.GetUlcerIndex recorded 330348728 owned allocation bytes at 10000 bars. This historical prioritization evidence is not a current builder/native measurement. WindowUlcerIndex still used padded BigInteger prices, percentage drawdowns and squared sums inside every complete window.

The state now retains doubles and reuses JurikDyadic for exact differences, percentage division, rounded drawdown squares, accumulation and the final correctly rounded RMS. Percentages beyond binary64 retain the existing RocBankValue rounded significand and extended exponent before squaring. Peaks still restart at zero for each window; missing positive prefixes remain absent, storage grows lazily, and nonfinite inputs retain their existing rejection. No GPU behavior or public builder contract changed.

Adversarial review covered per-stage rounding, finite roots of overflowing individual percentages, signed-zero/missing-prefix behavior, extreme exponent spans, and maximum-period startup. Nine unit cases passed against the original integer-window formula, including reset and wide/subnormal inputs. Ordinary periods 1, 3, 14 and 50 each allocated zero bytes across 4000 updates after capacity warmup, including startup after reset. These state observations exclude construction/output storage and do not establish builder/native wins.

Production and benchmark net10.0 builds plus nine independent UlcerComparisonTests passed, covering lifecycle, builder chaining, native routes and extreme-value output behavior. Commands: production `dotnet build src/OoplesFinance.StockIndicators.csproj -c Release -f net10.0 --no-restore`; benchmark build with `-f net10.0 --no-restore -p:BuildProjectReferences=false`; unit/comparison `dotnet test -c Release --no-restore -p:BuildProjectReferences=false` filtered to CompactUlcerTests and UlcerComparisonTests. Logs: `C:/Users/cheat/temp/si-ulcer-compact-*`. No timing campaign ran for this batch; qualification coverage remains 178/401 APIs.


### Compact exact Connors return ranking (2026-10-11)

ConnorsStrengthSnapshot previously retained padded integer return numerators/denominators and allocated cross-products for every inclusive-window rank comparison. It now reuses ReturnOrderStatistic.ReturnKey, comparing current/previous ratios exactly (the common minus-one return offset cancels). Nonpositive previous prices remain excluded through nullable keys; the initial zero return remains an explicit 1/1 key. Inclusive window eviction, strict ties, integer-truncated percentile and all four output/startup conventions are unchanged. The scan no longer creates a per-row LINQ predicate closure.

The shared return-key comparator now uses existing JurikDyadic exact products/differences, retaining arbitrary-precision fallback for wide exponent spans. Its prior zero/negative-denominator normalization remains unchanged for builder, core, batch and streaming callers. Adversarial tests cover extreme and overflowing cross-products, distinct near-ties, equivalent fractions, eviction and repeated queries.

On the regression input (1000 closes: 100 + (i * 13 % 101) / 8, default periods), Calculate allocated 95488 bytes, including output records, both RSI states and lazily grown rank history, after three warmup calls. The focused comparator test allocated zero bytes over 4000 ordinary comparisons. These are current-thread allocation observations on a synthetic workload, not competitor timing or a matched comparison with the historical report. Output records and tree nodes still allocate where applicable. Direct builder/native qualification remains pending; catalog coverage remains 178/401.

Production/benchmark net10.0 builds, 253 focused unit cases and five Connors competitor comparisons passed. Production used `dotnet build src/OoplesFinance.StockIndicators.csproj -c Release -f net10.0 --no-restore`; benchmark build and test commands used `-p:BuildProjectReferences=false`. Unit filter: CompactConnorsTests, ReturnOrderStatisticTests, ConnorsNumericalTests; comparison filter: ConnorsComparisonTests. The numerical cases cover batch/builder/core/streaming routes, customer stages, preview/reset and input validation. Logs: `C:/Users/cheat/temp/si-connors-compact-*`. No timing campaign ran for this batch.


### Compact Dynamic Momentum moments and exact period selection (2026-10-11)

DynamicMomentumSnapshot now reuses JurikDyadic for rolling nullable moments, the exact variance radicand, its rounded root and the nullable deviation mean. The divisor remains present-count times configured deviation length. The volatility ratio divides the two already-rounded binary64 stages once; positive infinity retains the lower-limit rule. Selected RSI trajectories still use the complete original history and remain cached per selected period.

The snapshot's exact integer period rule differs from the existing convenience helper's tolerance convention. A separate SelectExact helper clamps through exact products, then verifies the rounded quotient's integer candidate and decrements if division rounded upward across a boundary. Positive Int32 clamps bound the candidate before conversion. Tests compare with the original integer division at neighboring binary64 values around period boundaries, subnormal/extreme ratios and maximum Int32 parameters. Existing nullable/wide/tiny reference tests and all output/presence mutation checks pass.

On 1000 closes of 100 + (i * 13 % 101) / 8, configured with both period limits equal to 5, the snapshot allocated 48928 bytes after three warmups, including nullable output/deviation arrays and the cached RSI trajectory. This single-period regression observation is not a default-configuration benchmark or a matched competitor measurement. Direct builder adaptation and native timing qualification remain pending; catalog coverage stays 178/401.

Production/benchmark net10.0 builds, two focused unit tests and six DynamicMomentumComparisonTests passed. Production used `dotnet build src/OoplesFinance.StockIndicators.csproj -c Release -f net10.0 --no-restore`; benchmark build/tests used `-p:BuildProjectReferences=false`. Unit filter: CompactDynamicMomentumTests. Logs: `C:/Users/cheat/temp/si-dynamic-compact-*`. No timing campaign ran for this batch.


### Connors strength through BuildAsync (2026-10-11)

`ConnorsStrength` now exposes the snapshot convention as a normal eight-output builder indicator: close RSI, streak RSI, percent rank and combined strength, each with its own presence output. Configure it with `ConfigureIndicators(new ConnorsStrength())` and use the existing BuildAsync endpoint. The snapshot facade and builder share one incremental state; the builder path does not materialize ConnorsStrengthValue records.

State retains the verified compact return-key ranking, inclusive window, initial zero return, nonpositive-denominator exclusions, delayed streak/combined publication and exact final mean. Storage grows lazily and reset clears both RSI states and rank history. The original batch recurrence with independent padded-integer return ordering is retained as the validation reference; the separate competitor reference continues to check strength arithmetic independently.

The competitor comparison now executes BuildAsync and checks every value/presence pair. The manifest and catalog map Skender.GetConnorsRsi to ConnorsStrength; archived measurements remain historical, and direct four-arm timing qualification is still pending. Coverage remains 178/401 APIs.

Validation: net10.0 production/benchmark builds, ten builder/arithmetic/allocation tests, five competitor comparison tests and 55 inventory checks passed. Builder tests cover Full/LatestOnly, close-input chaining, all eight columns after disposal, reset, lifecycle validation and maximum periods. The shared state allocated zero bytes over 1000 ordinary updates after capacity warmup, including startup after reset. Production used `dotnet build src/OoplesFinance.StockIndicators.csproj -c Release -f net10.0 --no-restore`; benchmark/test projects used `-p:BuildProjectReferences=false`. Unit filters: ConnorsStrengthBuilderTests and CompactConnorsTests; comparison filter: ConnorsComparisonTests; inventory: `python -m unittest discover -s benchmarks -p test_shared_rollout_inventory.py`. Logs: `C:/Users/cheat/temp/si-connors-builder-*`, with final benchmark/unit logs suffixed `-final`. No timing campaign ran.


### Direct Connors CPU comparison and shared-route registration (2026-10-11)

SharedConnorsBenchmarks retains all four native nullable outputs directly from Skender 2.7.3's eager tuple API List, avoiding quote conversion and measured normalization/copying. The native parallel arm joins result references after complete-prefix replay in up to eight chunks, including all temporary native work. Source inspection covered ConnorsRsi.Api.cs, ConnorsRsi.Series.cs and tuple sorting in the local Stock.Indicators checkout; setup verifies the actual package's outputs and eager result type. Independent native/owned references, dates, all values/masks, minimal startup and uneven prefix boundaries are checked outside measurement. The allocation probe and inventory parser recognize the fixture.

One four-arm run at 10000 bars used 20 warmups and 15 requested measurement iterations on production d2f1e196 / fixture 02f617e4. Preflight CPU was 4%, completion 8%; process samples before, during and after found no competing dotnet builds/tests. Raw report and source/context metadata: `results/shared-rollout/connors-cpu/initial-10k.json.gz` and adjacent context JSON.

| Arm | Mean ns | CI lower / upper ns | Allocated bytes |
|---|---:|---:|---:|
| Full | 146784482.69 | 145219861.86 / 148349103.52 | 1136682 |
| LatestOnly | 147742092.86 | 146645938.06 / 148838247.65 | 1139194 |
| Native serial | 2112779.91 | 2016747.47 / 2208812.35 | 3081824 |
| Native parallel | 9174206.06 | 8507527.39 / 9840884.73 | 13952993 |

LatestOnly allocated about 63% less than native serial but took about 70 times as long. Similar Full/LatestOnly allocations exposed a missing ConnorsStrength entry in SharedCpuStates: the new builder indicator used the general route. The registration is now added, with a regression asserting that LatestOnly avoids full bar storage. This routing correction is NOT included in the report above. The first attempt collected no measurements because BDN rejected an IEnumerable return signature; the fixture now declares the native List type without adding a copy. No second timing campaign was run after the routing correction.

The catalog retains other evidence and adds Connors qualification: 179/401 APIs timing-qualified, not wins. Remaining Connors latency costs still require optimization; neither the storage regression nor the allocation advantage establishes a latency win.

Verification: final production/benchmark net10.0 builds, eleven builder/arithmetic/allocation tests, ten fixture/reference comparison tests and 56 inventory checks passed. The new Full-versus-LatestOnly storage regression passed after registration. Builds use `-f net10.0 --no-restore` and benchmark/test projects use `-p:BuildProjectReferences=false`. Unit filters: ConnorsStrengthBuilderTests and CompactConnorsTests; comparison filters: SharedConnorsBenchmarkTests and ConnorsComparisonTests. Logs: `C:/Users/cheat/temp/si-connors-fixture-*` and `si-connors-routing-*`.


### Fixed-width exact return ordering (2026-10-11)

On modern runtimes, ReturnOrderStatistic.ReturnKey now decodes each retained price once with the existing ProductFactor representation. Comparisons multiply the unsigned significands into two UInt128 products, compare signs and top exponents, and align only when the top exponents coincide. Each binary64 cross-product needs at most 106 bits; equal top exponents bound any alignment to that same width. This avoids general arithmetic structs, normalization and arbitrary-precision subtraction in the repeated rank-comparison loop, including for extreme finite exponent spans. NETFRAMEWORK retains the prior exact fallback. The change is local to return ranking, not a new general dot-product backend.

Adversarial review checked negative denominators, the explicit zero-previous convention, signed zero, equal rational returns, subnormals, product overflow beyond binary64, exponent alignment and sign reversal. Tests include 2000 full-significand randomized comparisons across the finite exponent range against padded integer products, antisymmetry/self-equality, existing edge/tie cases, tree eviction and all Connors execution routes. A mixed ordinary/extreme-key workload allocates zero bytes over 4000 comparisons. Cached factors enlarge each retained key; no new end-to-end allocation or latency claim is made.

Production/benchmark net10.0 builds, 262 focused unit cases and ten fixture/reference comparisons passed. Production used `dotnet build src/OoplesFinance.StockIndicators.csproj -c Release -f net10.0 --no-restore`; benchmark/test projects used `-p:BuildProjectReferences=false`. Unit filters: CompactConnorsTests, ReturnOrderStatisticTests, ConnorsNumericalTests, ConnorsStrengthBuilderTests. Comparison filters: SharedConnorsBenchmarkTests and ConnorsComparisonTests. Logs: `C:/Users/cheat/temp/si-return-key-fixed-*`. No timing campaign ran; the saved 147.74 ms Connors baseline predates both shared-route registration and this comparator change. Coverage remains 179/401 APIs, not wins.


### Monotone return-order certificate (2026-10-11)

ReturnKey caches the rounded current/previous ratio once per retained key on modern runtimes. IEEE division is monotone, so strictly different rounded values prove the exact ordering. Equal rounded values never prove equality: they fall through to the fixed-width exact cross-product comparator. The hot public comparison stays small; exact comparison remains separate. A cached double adds bounded storage per key.

Adversarial coverage includes two distinct finite rational ratios that round to the same binary64 value, shared positive infinity after overflow, shared zero after underflow, opposite signs at underflow, and the existing full-significand/exponent integer-reference tests. Production/benchmark net10.0 builds, 263 focused unit cases and ten fixture/reference comparisons passed. Builds/tests retain the prior framework and BuildProjectReferences restrictions. Logs: `C:/Users/cheat/temp/si-return-key-monotone-*`. Timing remains a separate checkpoint; correctness and zero-allocation comparison tests do not establish a speedup.


### Connors combined routing/comparator timing checkpoint (2026-10-11)

One unchanged four-arm 10000-bar fixture run on production 4f12c9cc completed after shared CPU registration, fixed-width exact products and monotone ratio certificates. It used 20 warmups and 15 requested iterations. Preflight/completion CPU samples were 5%/7%, with no competing dotnet builds/tests observed; the run completed before the next process sample, so there is no during-run contention observation. Raw report: `results/shared-rollout/connors-cpu/certified-order-10k.json.gz`, with adjacent source/context metadata.

| Arm | Mean ns | CI lower / upper ns | Allocated bytes |
|---|---:|---:|---:|
| Full | 15731235.00 | 14885415.08 / 16577054.92 | 1138650 |
| LatestOnly | 14379008.33 | 14272908.78 / 14485107.88 | 658639 |
| Native serial | 2115466.05 | 2044725.30 / 2186206.80 | 3081968 |
| Native parallel | 8815172.73 | 8257947.59 / 9372397.87 | 13952775 |

LatestOnly latency fell about 90.3% from the initial 147.74 ms baseline, but remains 6.8 times slower than native serial. Allocation is about 78.6% lower than native serial; the Full/LatestOnly storage distinction is now visible. This measures the three changes together and does not attribute a separate speedup to each. Only Connors's current catalog entry is replaced; both raw reports remain archived and coverage remains 179/401 APIs, not wins. No further timing campaign ran.


### Dynamic Momentum shared builder integration (2026-10-11)

`DynamicMomentumStrength` now runs behind `BuildAsync()` and is registered in
`SharedCpuStates`. Full and LatestOnly retain value/presence outputs. The state
keeps one close-price history, a rolling deviation window and one lazily created
RSI calculator per encountered period. A reselected period catches up from its
last consumed price; no complete per-period output arrays are retained. History
is still O(bars), since a previously unseen period needs the original RSI seed.
The existing nullable snapshot facade remains unchanged.

Adversarial review covered calendar smoothing startup before/after deviation
startup, full-history seeding across period changes, reset of cached calculators,
lazy maximum periods, exact wide/tiny arithmetic, chained inputs and retained
columns after disposal. The competitor adapter now uses the builder; independent
pairwise-distance and RSI references remain in the comparison tests.

Validation (Release, net10.0):
- Production build: passed, zero warnings/errors.
- Benchmark build with `-p:BuildProjectReferences=false`: passed.
- Unit filter `DynamicMomentumStrengthBuilderTests|CompactDynamicMomentumTests|NullableStrength`: 13 passed.
- Competitor filter `DynamicMomentumComparisonTests`: 10 passed.
- `python -m unittest test_shared_rollout_inventory.py` from `benchmarks`: 56 passed.
- Warmed state replay including reset: zero bytes over 1,000 ordinary bars with
  selectable RSI periods 1..8. LatestOnly saves at least 40 bytes/bar versus Full
  on the 1,000-bar default-period regression. These are focused regressions,
  not competitor timing measurements. Existing benchmark/test warnings remain.

Logs: `C:/Users/cheat/temp/si-dynamic-builder-{production,benchmark-build,unit,comparisons}.log`.
No timing campaign was run for this batch. Catalog timing qualification remains
179/401 APIs; this integration does not establish a performance win or GPU support.


### Dynamic Momentum direct benchmark fixture (2026-10-11)

`SharedDynamicMomentumBenchmarks` now compares CPU `BuildAsync()` Full and
LatestOnly with Trady 3.2.8's native tuple API. Native serial returns the eager
`List<decimal?>` directly, without result conversion/copying. Native parallel
uses up to eight independent calculators, full input prefixes for original RSI
seeding, and public `Compute(startIndex, endIndex)` ranges to avoid producing
unassigned outputs. Prefix construction, indicator construction, internal native
work and output assembly remain inside the measured method. Input conversion
is setup-only for both sides. This parallel arm is our public-API composition,
not a claim that Trady supplies a built-in parallel DMI method.

Setup checks native serial/parallel against an independent decimal reference,
owned values and presence against the pairwise-distance/RSI reference, and
cross-library presence and values (absolute tolerance 1e-9) on the fixture.
Tests at counts 1, 7, 11, 33, 65 and 127 cover startup, period changes and uneven
worker ranges. All six fixture tests plus ten Dynamic Momentum comparisons
passed; benchmark build passed with existing warnings; 57 inventory checks
passed. No production rebuild was needed. Logs:
`C:/Users/cheat/temp/si-dynamic-fixture-{build,tests}.log`.

The fixture is registered in the allocation probe and qualification parser.
No new performance samples were collected; qualification remains 179/401.
Historical normalized measurements (not this fixture) report about 1.60 GB
allocated per native 1,000-bar call and 161.85 GB per 10,000-bar call. Plan the
first direct timing run around that cost rather than using routine long warmups.
The tuple result type and range behavior were verified against the pinned NuGet
package; source inspected in the saved Trady DynamicMomentumIndex and
AnalyzableBase files explains repeated volatility histories and per-output RSI.


### Deferred RSI output calculation during Dynamic Momentum replay (2026-10-11)

Dynamic Momentum now advances skipped RSI bars without computing their discarded
final ratios. The original gain/loss recurrence, exact rounding, seed history,
lag history and nullable poisoning remain active for every price. Only the last
bar requested from a selected period computes its output. Other callers publish
by default. No public configuration or BuildAsync contract changes.

Adversarial review confirmed that final ratio calculation cannot mutate the
readonly gain/loss values. Five formula cases compare sparsely published outputs
bit-for-bit against the independent rational reference across ordinary, tiny,
subnormal and wide prices, nullable seed/later gaps, and reset with different
publication schedules. Release net10.0 production/benchmark builds passed;
18 unit cases and 32 fixture/reference comparisons passed, including the warmed
zero-allocation state replay. Existing benchmark/test warnings remain.
Logs: `C:/Users/cheat/temp/si-dynamic-deferred-{production,benchmark-build,unit,comparisons}.log`.
No isolated latency benefit is claimed for this change.


### Dynamic Momentum direct CPU checkpoint, 1,000 bars (2026-10-11)

Production `a84f3926`, fixture `f812f96c`, defaults 5/10/14/30/5. One short
campaign: three warmups, five requested measured iterations. All four arms passed
setup's native/owned independent references, presence and cross-library checks.

| Arm | Mean ns | 99.9% CI lower / upper ns | Allocated bytes |
|---|---:|---:|---:|
| Full | 3,389,977.78 | 3,021,938.35 / 3,758,017.20 | 88,417 |
| LatestOnly | 1,521,032.81 | 1,459,014.30 / 1,583,051.33 | 39,724 |
| Native serial | 827,887,706.25 | 788,728,375.74 / 867,047,036.76 | 1,604,493,684 |
| Native parallel | 803,154,550.00 | 764,390,738.96 / 841,918,361.04 | 2,923,740,664 |

Full and LatestOnly both have nonoverlapping intervals below both native arms.
LatestOnly's mean is approximately 528 times faster than the fastest measured
native mean. Native serial/parallel intervals overlap, so the run does not prove
parallel is faster than serial. The result combines all prior implementation
changes and does not isolate the benefit of deferred output calculation.

Limitations: short run; native serial retained four samples after BDN removed
one outlier. BDN warned that Full's minimum observed iteration was 29.506 ms,
below its recommended 100 ms. CPU was 6% before and 8% after; no competing dotnet
build/test/run processes were observed at those samples. The next process sample
was after the 84-second run completed, so there is no during-run observation or
claim of continuous isolation. No 10,000-bar or GPU conclusion follows.

Raw JSON and run context are archived in
`results/shared-rollout/dynamic-momentum-cpu/deferred-output-1k*`.
Log: `C:/Users/cheat/temp/si-dynamic-a84f3926.log`.
The inventory parser accepted all four arms. Only this pair's current evidence
was added to the catalog; measured coverage is now 180/401 APIs, not a win count.


### Ulcer Index direct fixture and exact peak shortcut (2026-10-11)

The shared CPU state skips percentage arithmetic at a new/equal positive peak:
its drawdown is exactly zero. The zero-peak absence check precedes this shortcut.
Extended unpublished drawdowns, exact squares and final RMS rounding remain.

`SharedUlcerBenchmarks` compares BuildAsync Full/LatestOnly with Skender 2.7.3's
public tuple serial API and up to eight independent native workers. Each worker
has P-1 input overlap; full-prefix replay is unnecessary for window-local peaks.
Native eager lists retain dates and nullable UI values directly. Native sorting,
calculation, overlap outputs and output assembly are timed; tuple/chunk preparation
is setup-only. This parallel arm composes public native calls; it is not a claim
that Skender ships a parallel Ulcer API.

Setup checks native bits/dates/presence against the staged native reference,
owned bits/presence against the independent exact reference, and cross-library
agreement within 1e-9. Fixture counts 1, 13, 14, 15, 31 and 65 exercise startup,
full windows and uneven overlap. Production/benchmark Release net10.0 builds,
nine compact arithmetic/allocation cases, 15 fixture/reference cases and 58
inventory tests passed. Existing benchmark/test warnings remain. The four-arm
fixture is registered in the allocation probe and qualification parser.
Logs: `C:/Users/cheat/temp/si-ulcer-direct-{production,benchmark-build,unit,comparisons}.log`.


### Ulcer Index direct CPU checkpoint, 10,000 bars (2026-10-11)

Production/fixture `2ffd53fc`, period 14, three warmups and five requested iterations.

| Arm | Mean ns | 99.9% CI lower / upper ns | Allocated bytes |
|---|---:|---:|---:|
| Full | 15,948,168.75 | 11,362,814.63 / 20,533,522.87 | 644,628 |
| LatestOnly | 16,184,630.00 | 14,603,152.07 / 17,766,107.93 | 164,837 |
| Native serial | 1,692,545.00 | 1,595,554.10 / 1,789,535.90 | 921,023 |
| Native parallel | 444,929.98 | 430,077.96 / 459,782.01 | 1,011,806 |

LatestOnly allocates 83.7% less than the fastest native path but its mean latency
is 36.4 times higher. Both builder intervals are above both native intervals.
This is a loss; zero steady-state allocation does not establish a speed win.
The next optimization target is CPU execution, including repeated exact
percentage arithmetic and window-level parallel execution, preserving the
existing once-rounded percentage and exact sum-of-squares contract.

Short-run limitations: Full and NativeParallel each retained four samples after
one outlier removal. BDN warned about minimum iterations of 61.921 ms (Full) and
94.869 ms (LatestOnly). CPU was 16% before and 14% after; no competing dotnet
build/test/run processes were seen at those samples. The 16.6-second run finished
before the next process sample, so no during-run or continuous-isolation claim.
No isolated benefit is attributed to the positive-peak shortcut.

Raw report/context: `results/shared-rollout/ulcer-cpu/positive-peak-10k*`.
Log: `C:/Users/cheat/temp/si-ulcer-2ffd53fc.log`. The qualification parser accepted
all four arms. Only this pair was added; coverage is 181/401 APIs, not a win count.


### Ulcer rolling contribution cache (2026-10-11)

The shared Ulcer state now retains each price's last prefix peak and rounded
extended drawdown in a lazily grown ring. Unchanged peaks reuse their exact
contribution. Expiration removes the outgoing contribution; changed peaks remove
and replace their drawdown squares in the exact running sum. The final RMS still
rounds once. Windows with a nonpositive prefix remain absent but maintain valid
suffix contributions so expiration recovers correctly. Reset clears the sum,
ring position and entries while retaining capacity.

Storage increases from one double per retained price to a price/peak/extended
contribution entry, bounded by O(period). This avoids repeated division and
squaring at unchanged peaks; it is not an O(1) worst-case window algorithm.
A descending sequence can still change every prefix peak. No public API,
rounding contract or GPU behavior changes.

Adversarial review covered expiring maxima, plateaus, absent-prefix recovery,
overflowed unpublished percentages, tiny inputs, maximum periods and reversed
replay after reset. Production/benchmark Release net10.0 builds passed, along
with 12 compact arithmetic/allocation cases and 15 fixture/reference cases.
Ordinary warmed state replay still allocates zero bytes. Existing benchmark/test
warnings remain. Logs: `C:/Users/cheat/temp/si-ulcer-cache-{production,benchmark-build,unit,comparisons}.log`.
No new timing run was performed; the 2ffd53fc latency-loss checkpoint remains the
latest measured result. This cache change is not claimed as a measured win.


### Ulcer bounded CPU worker integration (2026-10-11)

Standalone Ulcer now uses the existing bounded-window executor and published
AiDotNet.Tensors worker scheduler. Each worker owns an exact cached state seeded
with P-1 captured bars, writes disjoint values/presence, and retains the existing
input-before-output validation and cancellation order. The default 8,192-bar
threshold, up-to-eight-worker limit, shared scheduling gate and serial fallback
for periods above 4,096 remain unchanged. Chained/mixed configurations keep their
existing routes. Required GPU still rejects unsupported Ulcer execution.

Adversarial review confirmed that seed outputs are not published, presence is
finite by construction, only the primary RMS needs overflow validation, and
exact cache eviction is independent of preceding history after P-1 seeds.
Production/benchmark Release net10.0 builds, 21 unit cases and 15 fixture/reference
cases passed. Tests compare one/eight workers, Full/LatestOnly, zero/one inputs,
periods 1/14/4096/4097/int.MaxValue, invalid captured tails versus earlier overflow,
cancellation, later-run recovery and owned data after disposal/source mutation.
Logs: `C:/Users/cheat/temp/si-ulcer-parallel-{production,benchmark-build,unit,comparisons}.log`.
Existing benchmark/test warnings remain. No scheduler policy or public API changed.


### Ulcer cached/parallel CPU checkpoint, 10,000 bars (2026-10-11)

Production `f7fa53c5`, unchanged fixture `2ffd53fc`, period14, ten warmups and ten
requested measured iterations. Full/LatestOnly/native serial retained ten;
native parallel retained eight. No minimum-iteration-time warning was emitted.

| Arm | Mean ns | 99.9% CI lower / upper ns | Allocated bytes |
|---|---:|---:|---:|
| Full | 1,173,533.13 | 1,109,888.23 / 1,237,178.02 | 653,627 |
| LatestOnly | 1,107,439.45 | 1,100,926.47 / 1,113,952.43 | 172,670 |
| Native serial | 1,791,964.38 | 1,634,578.73 / 1,949,350.02 | 921,116 |
| Native parallel | 462,756.05 | 456,512.26 / 468,999.85 | 1,011,809 |

LatestOnly latency fell about 93.2% from the initial 16.18ms checkpoint. Both
builder intervals are below native serial but above native parallel. LatestOnly
still loses by 2.4 times against the fastest measured native mean while allocating
82.9% less. Allocation is higher than the initial serial builder due to bounded
cached/worker storage. This measures the cache and parallel integration together;
no isolated attribution or overall Ulcer win is claimed.

CPU was 6% before, 9% in one during-run sample, and 11% after. No competing dotnet
build/test/run processes were observed at those samples; not continuous isolation.
All four setup validations passed. Raw/context are archived at
`results/shared-rollout/ulcer-cpu/cached-parallel-10k*`; log is
`C:/Users/cheat/temp/si-ulcer-f7fa53c5.log`. Only Ulcer current evidence was replaced,
retaining the original checkpoint. Coverage remains 181/401 APIs, not a win count.


### Ulcer sampled profile and borrowed ring entries (2026-10-11)

Added benchmark-only `--profile-ulcer <bars> <iterations>`, using the validated
four-arm fixture setup, 100 LatestOnly warmups and a disposing builder loop.
An unprivileged `dotnet-trace` dotnet-sampled-thread-time capture at production
`f7fa53c5` ran 10,000 bars / 2,000 iterations and exited successfully. Global
thread-time samples were 73.47% Monitor.Wait; treating that as arithmetic cost
would misdiagnose the idle worker pool.

Filtering sampled stacks containing UlcerKernel gives about 20,148.71 ms summed
across threads: State.Update has 42.89% exclusive, SqrtMean 20.86% exclusive /
24.70% inclusive, Drawdown 12.98% exclusive / 18.95% inclusive, and the kernel
adapter 6.47% exclusive. These include inlined callees; State.Update's samples
are not all dispatch or storage overhead. Setup and warmup remain in the trace,
while the kernel filter excludes unrelated idle/startup stacks. This is sampled
stack time, not a new latency result or proof of benefit from a later change.

The modern CPU state now borrows ring entries by reference for its scan, after
all List growth is complete. Helpers cannot resize or retain the span. The
NETFRAMEWORK path keeps indexed copies. No arithmetic or rounding changes.
Production/benchmark Release net10.0 builds, 21 unit cases and 15 fixture/reference
cases passed; existing benchmark/test warnings remain. No timing rerun: the
f7fa53c5 checkpoint remains authoritative and still loses to native parallel.

Trace and filtered summary: `results/shared-rollout/ulcer-cpu/cached-parallel-profile*`.
The summary records the exact capture command and trace/runner hashes. Convert
the decompressed trace with `dotnet-trace convert --format Speedscope` to inspect
all stacks, or use `dotnet-trace report <trace> topN --verbose` for global totals.
Logs: `C:/Users/cheat/temp/si-ulcer-trace-collect.log` and
`C:/Users/cheat/temp/si-ulcer-borrow-{production,benchmark-build,unit,comparisons}.log`.


### Ulcer prefix-only cache repair (2026-10-11)

On expiration, repair stops when a surviving prefix peak equals its cached peak:
all subsequent surviving peaks are then unchanged. The new last entry is always
computed separately. Initial sentinel peaks prevent premature stopping on the
first full window; period one has no surviving prefix. Presence is equivalent
to the first price being positive. Exact contributions and absent suffix recovery
retain their contracts; descending windows can still require O(period) repair.
Production/benchmark Release net10.0 builds, 22 unit cases and 15 fixture/reference
cases passed. The new test exhausts 1,024 five-price patterns at each of periods
2, 3 and 4 against the independent reference. Existing tests cover extreme values,
reset, zero-allocation replay and parallel boundaries. Logs:
`C:/Users/cheat/temp/si-ulcer-prefix-{production,benchmark-build,unit,comparisons}.log`.


### Ulcer prefix-repair checkpoint, 10,000 bars (2026-10-11)

Production `40c75e34`, unchanged fixture `2ffd53fc`, period14; ten warmups and ten
requested measured iterations (LatestOnly retained nine, other arms ten).

| Arm | Mean ns | 99.9% CI lower / upper ns | Allocated bytes |
|---|---:|---:|---:|
| Full | 984,848.83 | 978,148.68 / 991,548.98 | 653,627 |
| LatestOnly | 953,642.19 | 949,474.52 / 957,809.86 | 172,670 |
| Native serial | 1,950,154.06 | 1,856,617.43 / 2,043,690.70 | 921,067 |
| Native parallel | 508,480.39 | 501,987.70 / 514,973.08 | 1,011,808 |

LatestOnly improved about 13.9% from 1.107ms with unchanged allocation, but remains
1.9 times slower than native parallel. Native parallel also moved from 0.463ms to
0.508ms; not all ratio improvement is attributable to owned changes. This combines
borrowed ring access and prefix repair. Both builders beat serial but lose to
parallel with nonoverlapping intervals. No minimum-iteration warning was emitted.
CPU samples before/during/after were 12%/9%/8%; no competing dotnet build/test/run
processes were observed at those points, not continuous isolation.
Raw/context: `results/shared-rollout/ulcer-cpu/prefix-repair-10k*`;
log: `C:/Users/cheat/temp/si-ulcer-40c75e34.log`. All setup validations passed.
Only Ulcer current evidence was updated; previous checkpoints remain archived.
Coverage remains 181/401 APIs, not a win count.


### Relative Volatility shared builder integration (2026-10-11)

`RelativeVolatilityStrength` now executes through BuildAsync and SharedCpuStates;
the competitor adapter and mapping use that path. Two bounded legs retain exact
price changes, sample-deviation moments and smoothed deviations with JurikDyadic.
Ordinary arithmetic avoids padded BigInteger values. Previous close starts at
zero; available-history windows, singleton zero deviation and once-rounded stages
remain unchanged. Overflowed unpublished roots/means retain extended exponents
before the final bounded percentage. The snapshot API stays unchanged as a batch
reference; pairwise squared-distance/reference-root tests remain independent.

Adversarial review covered exact rather than rounded changes, squared exponent
scaling for overflowed roots, lazy maximum periods, and reset of all four queues.
Release net10.0 production/benchmark builds, nine unit cases, eight independent
comparison cases and 58 inventory checks passed. Ordinary 1,000-bar state replay
including reset allocates zero bytes after capacity warmup. Full/LatestOnly and
chained results survive disposal; LatestOnly saves at least 40 bytes/bar on the
1,000-bar regression. Existing benchmark/test warnings remain. Logs:
`C:/Users/cheat/temp/si-rvi-builder-{production,benchmark-build,unit,comparisons}.log`.
No native timing was run and no performance win is claimed. Direct fixture,
timing and GPU work remain; measured coverage stays 181/401 APIs.


### Relative Volatility direct CPU fixture (2026-10-11)

`SharedRelativeVolatilityBenchmarks` compares Full/LatestOnly BuildAsync against
QuanTAlib 1.0.0 Rvi serial and up-to-eight-worker public-API composition. All arms
retain the value column; native calls write directly into the retained array.
Input TValue preparation is setup-only, while state construction, native nested
allocations, overlap calculation and output storage are timed. Each worker gets
2P-1 preceding prices: P smoothed deviations need P changes each, and the oldest
change needs its preceding price. The synthetic first change expires before the
first assigned output. Rvi, Stddev and Sma source was inspected; runtime setup
checks exact serial/parallel parity against the independent native reference.

Setup also checks builder bits against the independent pairwise-distance/root
reference and cross-library agreement within 1e-9. Counts 1/13/14/27/28/65/127
exercise startup and uneven worker boundaries at period14. Benchmark build,
15 fixture/reference cases and 59 inventory checks passed, with existing warnings.
The fixture is registered in the allocation probe and qualification parser.
Logs: `C:/Users/cheat/temp/si-rvi-fixture-{build,tests}.log`.
No production change or timing campaign in this batch; coverage stays 181/401.


### Relative Volatility shared parallel CPU execution

RelativeVolatilityStrength now uses the existing bounded-window CPU executor for standalone and mixed scalar roots. Each worker replays 2P-1 preceding prices; its deviation/smoothing states retain the original period P. Replay windows above 4096 stay serial, and periods whose doubled window would overflow use the generic shared-state route. Scheduler thresholds are unchanged.

Adversarial review checked synthetic first-change expiry, mixed-root dispatch, replay-length versus state-period separation and overflow-safe routing. Production and benchmark builds passed; 31 focused unit cases and 16 independent comparison/fixture cases passed. Coverage includes 1/8 workers, both history modes, exact wide-value seams, periods 2/14/2048/2049 and near integer limits, empty/one inputs, captured invalid tails, cancellation, recovery and retained ownership. The direct fixture now checks 8,193 bars against its independent reference.

Commands: production net10.0 Release build; benchmark net10.0 Release build with BuildProjectReferences=false; focused unit filter SharedRelativeVolatilityWindowTests|RelativeVolatilityStrengthBuilderTests|SharedUlcerWindowTests; competitor filter RelativeVolatilityComparisonTests|SharedRelativeVolatilityBenchmarkTests. Existing framework/analyzer warnings remain in the test build. Timing is deferred while competing jobs remain active; no new timing qualification, speedup or GPU support is claimed.


### Median Adaptive shared builder and combined diagnostic checkpoint (`415d6ed4`)

`MedianAdaptiveAverage` replaces the comparison adapter's snapshot execution with shared CPU `BuildAsync()` state. Lazy history and reusable sorted storage eliminate per-candidate lists/sorts: one sort per mature bar, then deletion of the two oldest occurrences for each shorter suffix. Exact dyadic arithmetic preserves signed median comparisons, once-rounded smoothing, duplicate values and maximum-period startup. The original snapshot and independent count-based order-statistic oracle remain references. QuanTAlib 1.0.0 source (`Maaf.Calculation`) performs `TakeLast(length).OrderBy(...).ToList()` for every candidate. Native parallel requires full prefix replay because both candidate and filter retain recursive state; construction, replay and result writes are inside the measured methods.

Production/benchmark builds passed. Ten focused MedianAdaptiveAverageBuilderTests, 12 comparison/direct-fixture cases and 60 inventory checks passed. The first unit run had four diagnostic-string assertion failures; values passed, and assertions were corrected to each history mode's documented execution reason before the successful rerun. Warmed 1,000-update state replay after reset allocates zero bytes. Test builds retain existing framework/analyzer warnings.

The allocation-only four-arm probe at 1,000 bars returned identical totals across three samples (after ten warmups): Full 61,264 B; LatestOnly 12,576 B; native serial 3,466,992 B; native parallel 15,673,112 B. Allocation counts include all process threads, construction and retained results; setup and disposal are excluded. LatestOnly allocates 99.64% less than native serial on this fixture. This is not timing evidence. Raw output, parsed results and context are in `results/shared-rollout/median-adaptive-cpu/reused-buffers-allocation-1k*`.

One combined ten-warmup/ten-iteration timing campaign covered both new CPU fixtures at 10,000 bars:

| Indicator | Arm | Mean ms | Allocation B |
|---|---|---:|---:|
| Median Adaptive | Full | 12.871 | 565,724 |
| Median Adaptive | LatestOnly | 12.905 | 85,540 |
| Median Adaptive | Native serial | 23.028 | 34,437,299 |
| Median Adaptive | Native parallel | 38.802 | 154,793,940 |
| Relative Volatility | Full | 2.169 | 614,910 |
| Relative Volatility | LatestOnly | 3.277 | 133,890 |
| Relative Volatility | Native serial | 14.066 | 10,326,512 |
| Relative Volatility | Native parallel | 3.890 | 10,474,968 |

These are diagnostics, not newly qualified wins. CPU samples before/during/after were 50/43/90%; no competing dotnet build/test/run appeared in the first two samples, but an AiDotNet.Tensors build appeared after the run. Its creation time was unavailable when rechecked, so overlap is unknown. No continuous isolation is claimed. Relative Volatility intervals overlap (LatestOnly 1.498-5.056ms, native parallel 2.110-5.669ms) and it received minimum-iteration-time warnings. All arms retained ten samples. Reports and context are archived as `initial-10k-diagnostic*` in each indicator's CPU folder and validated with the inventory parser; neither report was merged into timing qualifications. Coverage remains 181/401, not a win count. GPU support and further performance work remain pending.


### Klinger compact shared builder (`d904dee4`)

`KlingerVolumeWithSignal` replaces the Klinger comparison's snapshot adapter with shared CPU `BuildAsync()` execution. Compact exact running state and three SMA-seeded EMA states publish oscillator, signal and independent presence outputs. State retains no bar history or intermediate trajectories. Exact HLC comparisons, cancelling/zero range branches, extended unpublished force/averages, output rounding and maximum-period startup match the batch reference. The existing generated `KlingerVolumeOscillator` API is unchanged. The public snapshot still rejects unrepresentable outputs; its internal reference path exposes signed infinities so the validation framework can verify finite prefixes and runtime rejection.

Reviewed Skender.Stock.Indicators **2.7.3** source: `GetKvo` eagerly converts quotes and calls `CalcKvo`; the latter allocates seven full-length intermediate arrays and result objects. The direct serial fixture retains that result collection without a normalization copy. Native workers call the public API on full prefixes because all three EMAs retain recursive state; prefix arrays/results, quote conversion and output writes remain measured. Setup compares both outputs and their independent masks against the owned rational and staged-native references, including startup and uneven worker boundaries.

Production/benchmark builds, eight focused `KlingerVolumeWithSignalBuilderTests`, 12 `KlingerComparisonTests|SharedKlingerBenchmarkTests` cases and 61 inventory checks passed. Tests cover Full/LatestOnly retention after disposal, wide/tiny inputs, exact force cancellation above binary64 range, maximum periods, invalid-input precedence, cancellation/recovery, reset and zero allocations across 1,000 ordinary warmed state updates. An initial lifecycle failure exposed the throwing-reference contract mismatch; the internal reference correction passed the rerun and preserves public snapshot overflow tests. Existing framework/analyzer warnings remain in test builds.

One allocation-only probe at 1,000 bars (defaults 34/55/13, ten warmups, three measured samples per arm) produced identical totals in every sample:

| Arm | Allocated bytes |
|---|---:|
| Builder Full | 84,776 |
| Builder LatestOnly | 36,088 |
| Native serial | 212,608 |
| Native parallel | 967,832 |

LatestOnly allocates **83.03% less than native serial**. These totals include all managed process threads and retained outputs, excluding setup/disposal. The probe preceded the validation-only correction; execution state and benchmark methods did not change. Raw output, parsed results and context are archived in `results/shared-rollout/klinger-cpu/compact-state-allocation-1k*`. No timing run was added while other repositories' tests were active. Timing qualification stays **181/401** and GPU support remains pending; these allocation figures are not performance-win qualifications.


### Single-stochastic Schaff cycle shared builder

`SingleStochasticCycle` replaces the Schaff comparison's snapshot adapter with shared CPU `BuildAsync()` state. It reuses the compact SMA-seeded dyadic EMA arithmetic extracted from Klinger without changing the recurrence. Two lazy monotonic queues replace repeated minimum/maximum scans of arbitrary-precision MACD history. Each candidate is inserted/removed once; equal candidates keep the newest position because only the extreme value matters. A three-element smoothing ring preserves the complete-window SMA and independent presence output. Extended unpublished MACD values never need a finite double proxy for comparisons.

Skender.Stock.Indicators 2.7.3 `src/s-z/Stc/Stc.Series.cs` was reviewed: it materializes MACD results, transformed quotes, stochastic results and final STC results. The builder fuses these stages into one state update. The original `SchaffTrendCycleSnapshot` remains an independent BigInteger batch reference, and the generated Schaff indicators retain their existing APIs.

Production net10.0 Release and benchmark builds passed; the benchmark/test builds retain existing framework/analyzer warnings. Eighteen focused unit cases (`SingleStochasticCycleBuilderTests|KlingerVolumeWithSignalBuilderTests`), 17 independent comparison/fixture cases (`SchaffCycleComparisonTests|KlingerComparisonTests|SharedKlingerBenchmarkTests`) and 61 inventory checks passed. Verification covers Full/LatestOnly value/presence retention after disposal, owned bars, exact startup, flat/tied windows, queue growth/wrap/eviction, reversed replay after reset, maximum periods, invalid inputs, cancellation and explicit GPU rejection. Independent wide/subnormal comparisons passed. Both cycle and Klinger state replays allocate zero bytes over 1,000 ordinary updates after reset/capacity warmup.

The manifest/catalog now map `Skender.GetStc` to `SingleStochasticCycle`; existing qualification evidence was preserved. No direct STC timing fixture or CPU/GPU win is claimed in this batch. Timing coverage remains 181/401; the full rollout is incomplete.


### Schaff direct native fixture and allocation checkpoint (`8af126b4`)

`SharedSchaffCycleBenchmarks` measures Full/LatestOnly `BuildAsync()` and direct Skender serial/parallel execution, with defaults cycle10/fast23/slow50. The pinned 2.7.3 tuple API eagerly sorts/materializes its input and executes STC; its result collection is retained directly. This values-only route avoids decimal quote conversion. Setup verifies the quote overload gives identical values/presence, verifies serial versus full-prefix parallel replay exactly, and checks independent owned/native references. This route choice is based on avoiding conversion work, not a claim of universal fastest-overload timing. Input preparation and reference checks are outside measurement; native constructor/collection work, prefix replay and result writes remain inside.

Benchmark build and 13 focused `SchaffCycleComparisonTests|SharedSchaffCycleBenchmarkTests` cases passed, including counts1/49/50/58/59/60/61/127 around independent EMA, stochastic and final smoothing startup. The 62 inventory checks passed. Existing production binaries from `f958a4da` were reused; no unchanged production rebuild or unit-suite rerun was needed.

At 1,000 bars, the allocation-only probe used ten warmups and three samples per arm, with identical totals in each sample:

| Arm | Allocated bytes |
|---|---:|
| Builder Full | 69,704 |
| Builder LatestOnly | 21,016 |
| Native serial (tuple) | 547,256 |
| Native parallel (tuple prefixes) | 2,466,832 |

LatestOnly allocates **96.16% less than native serial**. Counts include all managed threads and retained results, excluding setup/disposal. Parsed/raw evidence and source hashes are in `results/shared-rollout/schaff-cycle-cpu/compact-state-allocation-1k*`. Other repository tests were active and CPU was 87% at inspection, so no timing campaign was started. This does not establish a latency or GPU win; timing qualification remains 181/401.


### Certified endpoint-weighted GPU execution

`EndpointWeightedAverage` now executes through the published AiDotNet.Tensors 0.134.4 OpenCL runtime, reusing the existing weighted-average kernel, cached program and bounded workspace. This covers the `QuanTAlib.Epma` and `QuanTAlib.Mma` mappings, including fixed-weight and available-mean startup. Each device work item handles 64 outputs, seeds its preceding window, then maintains sum and signed weighted sum without a host-computed output series. Auto remains CPU; explicit GPU requests execute on-device or reject.

Newest-first coefficients are `2P-1-3*lag`. After publication, subtracting three times the window sum shifts all weights; evicting the oldest coefficient then adds `(P+1)*expired`. Every numerator/intermediate is bounded by `2P^2` times the certified input bound (period one is identity). This stricter certificate replaces the positive-weight triangular mass for endpoint requests only. Periods above 32,767 reject because the certificate's term count must fit Int32. Off-grid/wide inputs reject rather than silently using CPU; complete finite-input validation still precedes certificate rejection. The final quotient rounds once.

The net10.0 Release production build passed with zero warnings/errors. The focused unit command filtered `TensorsEndpointWeightedGpuExecutionTests|TensorsWeightedAverageGpuExecutionTests|TensorsTriangularGpuExecutionTests|TensorsRollingSumGpuExecutionTests`; **39 cases passed, zero skipped** on the available GPU. Coverage includes both history modes, both startup conventions, periods1/2/3/20/65/4096/32767, 64-output block boundaries, signed zeros, period-one subnormal identity, exact CPU bit comparisons, invalid-input precedence, unsupported periods/inputs, cancellation, recovery and retained owned bars after disposal. Existing WMA/triangular/sum kernels passed shared-dispatch regressions. Test builds retain preexisting framework/analyzer warnings.

No latency or allocation benchmark was run for the new GPU path while other repositories' test jobs were active. Device execution is verified, but comparison against the best native CPU path, other devices and graph fusion remain pending. Timing qualification remains 181/401 and the full rollout is incomplete.


### Endpoint GPU six-path allocation comparison (`c2f66129`)

`SharedGpuEndpointBenchmarks` reuses the existing independent Epma/Mma CPU/native checks and adds GPU Full/LatestOnly. The complete GPU `BuildAsync()` call includes input staging, device transfer, dispatch and readback. Setup requires actual GPU execution and exact parity with the independently validated CPU output. Native serial retains its direct value array; native parallel measures construction, P-1 seed replay and output writes. The period is20. The shared allocation probe now reports all six methods for these two APIs; its existing four-method cases are unchanged.

The benchmark build, 13 shared allocation-probe regression cases and 63 inventory checks passed. Both actual GPU allocation probes passed setup/reference/backend assertions and all six methods. At 1,000 bars, ten warmups followed by three samples per method produced identical values in every sample:

| Method | Epma bytes | Mma bytes |
|---|---:|---:|
| CPU Full | 66,424 | 66,424 |
| CPU LatestOnly | 17,736 | 17,736 |
| GPU Full | 59,744 | 59,744 |
| GPU LatestOnly | 11,056 | 11,056 |
| Native serial | 262,408 | 219,808 |
| Native parallel | 293,848 | 227,728 |

These are warmed-workspace **managed allocations**, including worker threads and retained results. They exclude setup, cached kernel/workspace creation and disposal after each sample; they do not measure total device/resident/native memory. Raw/parsed evidence and context are archived under `results/shared-rollout/endpoint-gpu/*-allocation-1k*`. CPU was71% during fixture inspection with other repository test jobs active, so no latency campaign was started. No GPU speedup or new timing qualification is claimed; coverage stays181/401. Only the selected four CPU/native methods of this fixture can be fed to the current timing-qualification parser; six-method GPU reports require separate comparison evidence.


### Certified integer-affine convolution GPU execution

`NormalizedConvolution` now uses the shared weighted GPU recurrence when its copied kernel is an exact Int32 arithmetic progression. First weight and step are scalar arguments, so coefficient sets do not create additional compiled-program keys or device coefficient buffers. Endpoint-weighted averages use this same parameterized recurrence. The source remains staged and validated on the host; output arithmetic executes on-device through AiDotNet.Tensors 0.134.4.

For period P, first coefficient F and step S, the certificate uses `P * max(1, abs(F), abs(F+S*P), abs(S))`. This bounds every weighted partial sum, the shifted window before eviction, the ordinary window sum and the `S*sum` operand. A division-before-multiplication check rejects bounds outside Int32 without overflowing the host calculation. Kernels outside exact integer progressions, oversized coefficient bounds or uncertified input grids reject explicitly. The exact signed prefix mass remains the divisor; zero mass uses available count while retaining signed weights. A one-element zero kernel yields zero, while nonzero one-element kernels preserve identity and canonical zero.

The net10.0 Release production build passed with zero warnings/errors. The focused filter `TensorsAffineConvolutionGpuExecutionTests|TensorsEndpointWeightedGpuExecutionTests|TensorsWeightedAverageGpuExecutionTests|TensorsTriangularGpuExecutionTests|TensorsRollingSumGpuExecutionTests` passed **53 cases with zero skips** on the available GPU. Coverage includes exact CPU bit comparisons, both history modes, 64-output block boundaries, ascending/descending/negative/zero weights, zero prefix/full mass, large coefficients, one-element subnormal identity, unsupported coefficients/inputs, validation precedence, cancellation and workspace recovery. All preexisting weighted-family device cases passed after the kernel signature change. Test builds retain existing framework/analyzer warnings.

This extends actual GPU execution for the affine subset of the QuanTAlib.Convolution mapping; it does not claim arbitrary convolution support or a GPU speedup. No new benchmark campaign was started under concurrent machine load. Timing qualification remains181/401 and broader GPU/graph rollout is incomplete.


### Affine convolution six-path GPU comparison (`4ee6d76c`)

`SharedGpuConvolutionBenchmarks` reuses independent weighted-window CPU/native references and verifies actual GPU execution with exact output parity. It measures complete CPU/GPU Full/LatestOnly builds against direct native serial and P-1-seeded native parallel calls. The fixture uses twenty newest-first coefficients1..20; coefficient arrays and indicator construction are inside measured methods, while source preparation and reference checks are setup-only.

The benchmark build, 13 shared allocation-probe regression cases and 64 inventory checks passed. The real six-path GPU probe completed all backend/reference assertions. At 1,000 bars after ten warmups, three managed-allocation samples yielded:

| Method | Allocated bytes |
|---|---:|
| CPU Full | 66,928 / 66,928 / 67,360 |
| CPU LatestOnly | 18,240 (all three) |
| GPU Full | 60,248 (all three) |
| GPU LatestOnly | 11,560 (all three) |
| Native serial | 220,272 (all three) |
| Native parallel | 231,440 (all three) |

The 432-byte CPU Full variation is preserved without attribution. These totals include all managed threads and retained results; they exclude cached kernel/workspace creation, setup and post-sample disposal, and do not measure total device/native memory. Raw/parsed evidence and exact revisions are under `results/shared-rollout/affine-convolution-gpu/allocation-1k*`. Other build/test processes remained active (25% CPU at inspection), so no timing run was started. Timing qualification remains181/401; device parity and managed-allocation savings do not prove a speedup.


### Retrospective regression builder integration

`RegressionWithFinalLine` routes the `Skender.GetSlope` comparison through `BuildAsync()`. The shared CPU path writes rolling statistics and explicit presence directly into run-owned columns, aliases the three identical full-window presence columns, and completes only the final fitted-line window after all raw inputs are validated. The existing historical fallback preserves selected sources and warmup coordinates; live sources reject this retrospective calculation. `RegressionSnapshot` remains unchanged.

Focused checks cover independent rational references, all ten outputs, repainting, flat windows, subnormal variance, incomplete/maximum periods, mixed roots, chaining, warmup, input-before-output validation, cancellation and ownership after disposal. This batch removes snapshot row objects but retains the existing exact regression arithmetic. It adds no timing or allocation measurement and establishes no speed win; the 181/401 timing-qualified count is unchanged.

Verification: net10.0 production and comparison builds passed; 13 focused unit cases, 8 regression comparison cases and 64 inventory checks passed, with no skipped tests.


### Compact shared regression moments

`WindowRegressionStatistics.State` now keeps one observed-history queue and reuses the existing exact dyadic accumulator for sums, squares, weighted moments and fitted coordinates. This removes the duplicate fit-window history and routine minimum-subnormal-unit BigInteger expansion for both rolling statistics and retrospective regression consumers. Wide exponent spans retain arbitrary-precision fallback; no rounded slope or intercept is reused to derive another output.

The net10.0 production/comparison builds, 15 focused unit cases and 12 independent regression/channel comparison cases passed with no skips. Differential tests matched the previous state bit-for-bit across resets, full/partial windows, random fractions, extreme magnitudes, signed zero and subnormals. A warmed 1,000-bar eighth-unit price fixture, including final-line reads, allocated zero managed bytes on the current thread. This excludes state construction, history growth and result columns, and does not establish total BuildAsync allocations or a latency win. No timing benchmark was rerun; qualification remains 181/401.


### Direct regression statistics allocation comparison

`SharedRegressionStatisticsBenchmarks` retains all five native outputs and presence for QuanTAlib.Slope and Skender.GetSlope. QuanTAlib uses packed rows and P-1 seed replay on the nonconstant fixture; Skender uses its public tuple collection, full prefixes for global intercepts, and only the final prefix's decimal line overlay. Setup checks independent centered references and serial/parallel parity outside measurement.

At 1,000 bars and period 20, all three warmed samples agreed:

| API | Builder Full bytes | Builder LatestOnly bytes | Native serial bytes | Native parallel bytes |
|---|---:|---:|---:|---:|
| QuanTAlib.Slope | 134,160 | 85,472 | 442,600 | 466,576 |
| Skender.GetSlope | 118,144 | 69,456 | 164,400 | 750,680 |

These process-wide managed allocations include construction and retained outputs; setup and post-sample disposal are excluded. Production revision e76348e0, fixture revision 30a3eae4, raw samples and limitations are archived in `results/shared-rollout/regression-statistics-cpu/`. The fixture build, 23 focused tests and 65 inventory checks passed with no skips. An AiDotNet build was active at pre-probe inspection; no timing benchmark was run and qualification remains 181/401.


### Retrospective regression channels through BuildAsync

`RegressionWithChannels` now supplies the Skender.GetStdDevChannels builder comparison. Complete blocks align to the final input length; appending bars can repaint all blocks. The shared writer uses compact regression moments and extended rounded widths, publishing directly into five owned columns for seven outputs (the three presence outputs share storage). The historical fallback handles selected sources and warmup. Null period fits the complete input and requires at least two bars; explicit periods preserve absent leading rows. Live sources reject retrospective execution. `RegressionChannelSnapshot` keeps its public behavior. Rolling `WindowRegressionStatistics` also shares its three identical presence columns.

The net10.0 production/comparison builds, 25 focused unit cases, 21 regression/channel/fixture comparison cases and 65 inventory checks passed with zero skips. Coverage includes independent references, block shifts, whole-input fits, all values/presence, ownership after disposal, validation order, cancellation inside block publication, warmup and selected sources. No allocation or timing probe was repeated for this batch; prior allocation checkpoints retain their recorded revisions. Timing qualification remains 181/401 and this is not a latency-win claim.


### Certified linear regression GPU outputs

`WindowLinearRegression` now runs endpoint, forecast, slope and oldest-candle intercept on the existing cached weighted GPU kernel. Each 64-row worker maintains exact sum and newest-first lag sum; the selected rational output is rounded once. Available-history startup and period-one zero slope are preserved. The 2P^2 term certificate bounds numerator products, signed totals and lag shifts through period 32767; slope denominators remain exactly representable integers. Unsupported periods, off-certificate inputs and Angle reject required GPU execution rather than silently using CPU. Auto remains CPU.

The net10.0 production build and 62 real-device regression/weighted-family cases passed with zero skips. Checks cover bitwise CPU parity, startup, worker boundaries, largest-period eviction, ownership after disposal, input-before-certificate validation, rejection/recovery and cancellation. No timing or allocation probe was run for this extension; no GPU speedup is claimed and timing qualification remains 181/401.


### Certified CPU linear regression recurrence

The finite builder now specializes WindowLinearRegression periods through 32767 using the same certified sum/lag recurrence as the GPU. Endpoint, forecast, slope, intercept and CPU angle avoid general exact-state updates while the input certificate holds. A failed certificate seeds the existing exact state from the retained chronological window, preserving arbitrary finite inputs and selected-output overflow behavior. The existing AiDotNet.Tensors bounded engine supplies parallel overlap replay and mixed-root execution; larger periods keep the original path.

The net10.0 production/comparison builds, 21 focused CPU/GPU cases and 10 independent regression comparisons passed with zero skips. Checks include late certificate failure after ring wrap, mixed parallel roots, large-period fallback, signed zero, unpublished overflow, raw validation priority, cancellation and owned results. A warmed 1,000-bar certified state replay allocated zero managed bytes, excluding construction and output storage.


A focused 10k-bar, period-20 LinearRegSlope run at revision 432e31fc used ten warmups and ten requested measurement iterations. Full averaged 79.995 us / 567,118 B; LatestOnly 47.413 us / 86,086 B; native serial 182.744 us / 79,912 B; native parallel 49.496 us / 80,292 B. LatestOnly's confidence interval [45.843, 48.983] us narrowly overlaps native parallel [48.923, 50.068] us: approximately matched, not a proven win. Full remains slower than native parallel. The previous saved LatestOnly checkpoint was 1,771.148 us on this workload, with native parallel 50.085 us; it is a historical comparison, not a simultaneous control.

CPU load was 13 percent before and 2 percent after; no competing build/test was observed at either sample. No continuous process trace was captured. Raw report, log, source hashes and limitations are in `results/shared-rollout/certified-regression-cpu/`. Only this API's 10k current entry was replaced, preserving its earlier 1k evidence and all other APIs. The timing-qualified count stays 181/401.


At revision bdc51f7e, standalone typed regression workers hold mutable value-type state directly and cache complete-window coefficients/denominators. This removes a state object per worker without adding coefficient fields to boxed mixed-batch states. The production/fixture builds, 21 CPU/GPU cases and 30 comparison/probe cases passed with zero skips.

A 10k-bar period-20 slope allocation-only probe returned identical values across three samples: Full 566,416 B, LatestOnly 85,392 B, native serial 79,904 B and native parallel 80,288 B. LatestOnly still allocates 5,104 B more than native parallel. Raw samples and context are archived beside the previous timing run in `results/shared-rollout/certified-regression-cpu/slope-value-allocation-10k*`. The probe and prior BDN report use different measurement harnesses; their byte difference is not an isolated before/after measurement. No timing rerun was performed, and the catalog retains the earlier timing checkpoint unchanged.

### GPU scalar regression allocation checkpoint (2026-10-12)

The endpoint, slope, intercept and forecast fixtures now compare CPU/GPU Full and LatestOnly against native serial/parallel. Setup verifies actual device execution and exact builder output bits, including startup against the independent integer oracle. Three warmed 1k/P20 samples per arm agreed: CPU Full 59,992 B, LatestOnly 11,304 B; GPU Full 59,792 B, LatestOnly 11,104 B; native serial 7,904 B, parallel 8,288 B. Native retains 981 completed-window values; builder also computes and retains startup values (1,000 total), and Full owns OHLCV. Cached GPU memory is excluded. These are managed allocations, not latency wins; qualification remains 181/401. Evidence: `results/shared-rollout/gpu-regression/`. Use `--alloc-shared-gpu TaLib.Functions.LinearRegSlope 1000 3` for explicit GPU sampling; ordinary regression allocation probes remain CPU-only.

### Shared execution setup allocation reduction (2026-10-12)

Shared values execution skips single-root deduplication, creates filtered nodes only for the row-wise route, fills output columns directly, reserves published-output capacity and avoids unused scratch buffers and snapshot closures. Three identical slope allocation samples show Full saves 728 B and LatestOnly saves 832 B against the same prior probes. At 10k, CPU LatestOnly uses 84,560 B versus native parallel 80,288 B (4,272 B remaining gap). At 1k, GPU LatestOnly uses 10,272 B versus native parallel 8,288 B. Evidence: `results/shared-rollout/shared-setup/`. No timing was run and qualification remains 181/401. Verification: production net10.0 build clean; 142 focused cases passed, one stale existing test still expected supported RollingPriceSum GPU execution to reject.

### Lazy legacy builder setup (2026-10-12)

The facade now creates legacy graph dictionaries and callback catalogs on first use, instead of on every typed build. Existing per-builder identity, callback fallback and named-source behavior are retained. One six-arm slope probe (1k/P20; three identical samples) saves another 928 B in every builder arm: CPU Full 58,336 B, LatestOnly 9,544 B; GPU Full 58,136 B, LatestOnly 9,344 B; native serial 7,904 B, parallel 8,288 B. Evidence: `results/shared-rollout/lazy-legacy-setup/`. Production net10.0 build is clean; 107 focused unit cases passed, zero skips. The previously reported stale GPU rejection test is unchanged and outside this selection. No timing run or coverage increase.

### Compact scalar LatestOnly result (2026-10-12)

Nonempty single-output shared runs now retain their output array and latest bar in one owner implementing both result and snapshot interfaces. This removes the output dictionary and separate snapshot allocation while preserving lookup errors, replay rejection, warmup and disposal lifetime. Three identical 1k/P20 slope samples show another 280 B saved for CPU/GPU LatestOnly: 9,264 B / 9,064 B respectively, versus native serial 7,904 B and parallel 8,288 B. Full allocation is unchanged. Evidence: `results/shared-rollout/scalar-latest-result/`. Production net10.0 build clean; 81 focused tests passed with zero skips. No timing run or qualification increase.

### Fisher shared-state optimization (2026-10-12)

`WindowFisherTransform` now reuses lazy rolling minimum/maximum deques instead of scanning a retained queue each update. Midpoint and range ratios use the existing compact exact dyadic arithmetic with wide fallback; smoothing and logarithm rounding are unchanged. Five focused state cases and eight independent volatility/Fisher comparisons passed, including startup, flat windows, expiration, resets, period one/int.MaxValue, extreme values and subnormals. A warmed 1,000-update ordinary midpoint sequence, including reset startup, allocated zero current-thread managed bytes. This is state-only evidence, not total builder allocation or a native speed win. Production net10.0 build passed with zero warnings/errors. Timing was deferred because AiDotNet.Tensors tests were running; direct Fisher native/builder timing remains unqualified and the catalog stays 181/401.

### Direct Fisher native/builder fixture (2026-10-12)

`SharedFisherBenchmarks` uses the published Skender 2.7.3 eager tuple API and builder close-input mode, retaining Fisher, trigger and presence. Exact recursive parallel replay includes every prefix and temporary result; setup validates native dates and bitwise serial/parallel agreement plus independent native/exact references and all builder columns. Three identical warmed 1k/P20 allocation samples measured Full 83,288 B, LatestOnly 34,496 B, native serial 124,448 B and native parallel 570,232 B. The 32 focused comparison/probe tests and 67 inventory tests passed. Evidence/source links: `results/shared-rollout/fisher-cpu/`. Timing remains deferred because another tensors test process was active; the catalog remains 181/401, with Fisher unqualified for timing.

### Compact path-ratio arithmetic and Fisher timing diagnostic (2026-10-12)

`WindowPathRatio`, shared by Skender CMO and Trady EfficiencyRatio, now stores compact exact rolling steps and path sums and rounds only the final ratio. The wide fallback and nullable endpoint/step rules are preserved. Six state/differential tests and 15 independent comparison cases passed; ordinary warmed 1,000-update state checks allocate zero managed bytes in both conventions, including reset startup. Production net10.0 build is clean. This is state-level allocation evidence, not total builder/native timing.

One 10k Fisher four-arm run was attempted on production `4b244e81`/fixture `a6ed67b6`. It measured LatestOnly 1.512 ms and native serial 1.367 ms, but a new AiDotNet.Tensors net471 build was active at completion (CPU 36%). Contention overlap cannot be ruled out, so the report is diagnostic only and excluded from qualification. The run was not repeated. Raw evidence/context: `results/shared-rollout/fisher-cpu/diagnostic-10k*`. Coverage stays 181/401; neither change establishes a native speed win.

### Path-ratio bounded CPU execution (2026-10-12)

Standalone `WindowPathRatio` now uses the shared bounded CPU engine, with P preceding prices for each worker to reconstruct exactly P changes. It publishes value/presence directly without per-worker output scratch arrays. The calculator retains one price queue and the outgoing predecessor instead of a second queue of exact step structs; nullable gaps and exact eviction remain unchanged. Period arithmetic is guarded, large replay windows remain serial, and int.MaxValue retains the prior state route. The existing 8,192-row scheduling threshold is unchanged. Production net10.0 build passed cleanly; 13 focused state/worker cases and 15 competitor comparisons passed. Tests cover one/eight worker budgets, both history modes, flat runs, limits around the 4,096-price parallel window, reset/nullable semantics, ignored-field validation, cancellation and recovery. No timing or builder-allocation sampling was repeated; no speed claim or qualification increase.

### Direct path-ratio fixtures and allocation evidence (2026-10-12)

`SharedPathRatioBenchmarks` compares builder Full/LatestOnly with Skender CMO and Trady EfficiencyRatio public tuple APIs. Serial arms retain native lists. Parallel arms seed P preceding prices and retain assigned native outputs, including local input/result costs; Trady uses its public output-range API. Setup checks independent references, nullable/presence contracts, Skender dates/bit patterns and exact Trady decimals. All 41 comparison/probe tests and 68 inventory checks passed. Three warmed 1k/P20 samples: CMO Full 67,432 B, LatestOnly 18,640 B, native serial 108,456 B, native parallel 135,880 B. Efficiency Full 67,432 B, LatestOnly 18,640-19,536 B, native serial 556,704 B, native parallel 554,136-554,472 B; all samples are retained. Source/context and raw evidence: `results/shared-rollout/path-ratio-cpu/`. These are allocation observations; no timing was run and qualification stays 181/401.

### Path-ratio 10k timing qualification (2026-10-12)

One eight-case timing batch on production `143fe7ad` / fixture `71af8461` completed with no competing build/test observed before, in two mid-run samples, or after. CPU was 6% before and 17% after; reusable MSBuild nodes and a dotnet --version process were present. Sampling is not a continuous exclusivity trace. Ten warmups and ten measurement iterations were requested; retained sample counts and 99.9% confidence intervals are in the raw report.

| API / 10k, P20 | Full mean us / bytes | LatestOnly mean us / bytes | Fastest tested native mean us / bytes | Result |
|---|---:|---:|---:|---|
| Skender.GetCmo | 414.20 / 653,108 | 372.74 / 171,906 | parallel 247.95 / 1,179,864 | Both builder modes lose latency |
| Trady.Indicator.EfficiencyRatio | 297.38 / 653,045 | 267.20 / 171,907 | parallel 2,099.99 / 5,529,251 | Both builder modes win with nonoverlapping intervals |

EfficiencyRatio LatestOnly is about 7.86x faster than the fastest tested native arm; CMO LatestOnly remains 1.50x slower despite lower allocation. This qualifies the two 10k workloads only; all previous catalog entries are preserved. Coverage rises to 183/401 APIs, not a win count. Raw report, process samples and context: `results/shared-rollout/path-ratio-cpu/cpu-native-10k*`. No rerun was performed.

### Guarded CMO percentage publication (2026-10-12)

The signed path-ratio convention now avoids integer promotion for multiplication by 100 only when both numerator and denominator are exactly representable binary64 values and a finite product passes an exact FMA residual check. Because the integer factor keeps the residual on the binary64 subnormal grid, a nonzero residual cannot round to zero. Inexact/overflowing products and wide operands use the previous exact path. Thirteen state/worker cases and 41 comparison/probe cases passed, including rounded products, overflowing products with finite ratios, subnormals, nullable gaps and worker seeds. Production net10.0 build is clean. The earlier CMO timing loss remains the saved checkpoint; this change is not retimed and adds no qualification or speed claim.

### Certified path-ratio GPU kernel (2026-10-12)

Required-GPU `WindowPathRatio` now executes CMO/efficiency value and presence columns in one cached OpenCL kernel through the existing AiDotNet.Tensors workspace. Each work item handles 64 output rows and seeds P preceding prices. The shared binary-grid certificate bounds path sums by 2P price terms and signed percentage numerators by 200; uncertified inputs reject after complete raw validation. All-missing windows (P >= count) execute on-device without arithmetic certification, including int.MaxValue periods. Kernel programs and workspace are reused across periods, and Auto remains CPU. Production net10.0 build passed cleanly; 56 focused CPU/GPU cases passed with zero skips, covering exact bits, flat/startup presence, 64-row boundaries, completed 4096-period windows, huge incomplete periods, malformed ignored fields, cancellation, rejection/recovery, retained history and existing weighted/regression/sum device paths. GPU allocation/latency is not measured yet; timing qualification stays 183/401.

### Path-ratio GPU allocation fixtures (2026-10-12)

The six-arm GPU fixture checks exact values and presence in both histories and requires the GPU backend. Benchmark build, 42 comparison/probe tests and 69 inventory tests passed. Three warmed 1k/P20 samples on AMD gfx1012 gave GPU Full 66,208 B and LatestOnly 17,416 B for both operations; CPU LatestOnly was 18,640 B. CMO native serial/parallel allocated 108,456/135,880 B. Efficiency native serial allocated 556,704 B and parallel 554,136-554,192 B; CPU Full varied 67,432-67,768 B. All samples and raw logs are archived in `results/shared-rollout/path-ratio-gpu/`. These count all managed threads and exclude cached device/workspace memory; no latency or new qualification claim (183/401).

### Certified CPU path-ratio recurrence (2026-10-12)

The bounded CPU builder kernel now reuses the SMA binary-grid certificate with the same 2P/200 term bounds as the path-ratio GPU kernel. Certified rows use a fixed price ring and double recurrence instead of per-row dyadic arithmetic. Failure promotes once by replaying retained raw prices in chronological order into the existing exact calculator; incomplete windows avoid state allocation. Scheduling and public contracts are unchanged. Production net10.0 build passed with zero warnings/errors; 21 state/worker tests and 15 independent comparison tests passed, including early/late certificate failure, ring wrap, worker boundaries, subnormals, extreme finite values, both conventions and histories. This implementation batch was not benchmarked; the earlier CMO loss and 183/401 timing coverage remain the recorded evidence.

### Certified CMO CPU/GPU timing (2026-10-12)

One six-arm 10k/P20 comparison at ff9e16d3 measured CPU Full 91.83 us / 645,542 B and LatestOnly 57.71 us / 164,406 B versus native serial 741.57 us / 1,081,723 B and native parallel 272.28 us / 1,179,863 B. Both CPU history modes clearly win by nonoverlapping reported confidence intervals; LatestOnly is 4.72x faster than the fastest tested native arm. GPU Full 583.75 us / 642,580 B and LatestOnly 553.11 us / 161,440 B lose to native parallel at this size. Auto remains CPU. Raw six-arm report, CPU qualification projection, source hashes and process samples: `results/shared-rollout/path-ratio-certified-cpu/`. CPU load was 5% before and 9% after; no competing build/test was observed before, during or after, but sampling is not continuous exclusivity proof. Ten warmups/iterations were requested. BDN warned that the shortest Full iteration was 44.182 ms and removed one GPU Full/two GPU LatestOnly outliers; raw samples are retained and no rerun was performed. Only the CMO 10k catalog entry changed; qualification stays 183/401.

### Certified Ulcer percentage contributions (2026-10-12)

WindowUlcerIndex now reuses the common-grid certificate with a 200-price-term bound to compute each changed percentage contribution with exact subtraction/multiplication and one binary64 division. Cached contributions, exact squared accumulation and final root rounding remain unchanged. Once a value rejects certification, the existing extended arithmetic handles subsequent contributions; reset restores certification. Production net10.0 build passed cleanly. All 24 state/worker tests and 15 independent comparison/fixture tests passed, including late rejection after ring wrap, scaled inputs, absent-prefix recovery, extreme values, subnormals, parallel boundaries and zero warmed state allocations. No allocation probe or timing rerun was performed. The saved Ulcer loss remains current evidence and timing coverage stays 183/401.

### Compact zero-seeded Laguerre stages and direct fixture (2026-10-12)

ZeroSeedLaguerreFilter now uses shared compact dyadic differences and one rounded SumProduct per stage, preserving extended exponent fallback and exact final weighted publication. FMA is used only for exactly representable operands. Eleven state tests compare the frozen previous algorithm across endpoints, subnormals, wide intermediates and resets; ordinary warmed updates allocate zero bytes. The four-arm direct QuanTAlib.Ltma fixture checks independent native/exact references, both history modes and full-prefix native parallel replay. Production build is clean; benchmark build, 46 comparison/probe cases and 70 inventory checks passed. Three warmed 1k samples: Full 58,272 B, LatestOnly 9,200 B, native serial 48,384 B, native parallel 191,064 B. Full still allocates more than native serial. Package-pinned source, contracts and raw evidence: `results/shared-rollout/zero-seed-laguerre-cpu/`. No latency run or qualification increase (183/401).

### Laguerre and triangular 10k qualification (2026-10-12)

One 16-case batch at 54441b95 qualifies three pending APIs, raising timing coverage to 186/401 (not a win count). Triangular LatestOnly took 66.57 us / 88,819 B versus QuanTAlib parallel 344.99 us / 2,643,560 B; Full 102.71 us / 570,221 B also won. Against TA-Lib the same builder took 68.03 us LatestOnly and 99.62 us Full, losing to native parallel 10.65 us / 80,329 B. Triangular GPU LatestOnly took 423.13-434.18 us and Full 489.35-496.49 us, losing to both parallel competitors. Laguerre LatestOnly took 3,144.85 us / 81,373 B and Full 3,316.97 us / 565,408 B versus native serial 353.83 us / 480,404 B (faster than native parallel 420.53 us). Reported confidence intervals do not overlap for these wins/losses. Raw reports, CPU qualification projections, source hashes and process samples: `results/shared-rollout/laguerre-triangular-10k/`. No competing build/test was seen before, during or at completion; CPU load was 14% before and 5% after, without continuous exclusivity proof. Ten warmups/iterations requested. BDN warned of a 12.404 ms minimum Laguerre Full iteration and removed triangular outliers detailed in context; raw data retained, no rerun. All unrelated catalog entries are preserved.

### Guarded Laguerre binary64 kernel (2026-10-12)

The Laguerre state now attempts four certified exact-difference/FMA updates before using compact dyadic stages. Its final weighted mean retains error-free addition residuals and outward-rounded bounds; only a quotient whose strict rounding cell contains the exact result is accepted. One adjacent quotient may be tested, while ties, cancellation outside the proof bounds and wide exponents use the existing exact path. No state is published until the whole fast update succeeds. Production net10.0 build is clean; 12 state tests (including 4,000 deterministic mean comparisons against exact accumulation) and 18 independent comparison/fixture cases passed. Zero warmed state allocation checks still pass. No timing rerun: the saved 3.14 ms LatestOnly loss remains current evidence; coverage stays 186/401.

### Compact and certified Holt-Winter stages (2026-10-12)

HoltWinterForecast now uses compact exact state with guarded complete-stage product sums. Error-free product/addition residuals and outward bounds certify a single final rounding; uncertain ties, subnormal half terms and wide values use the compact/extended fallback. Period-one identity, initial signed zero, resets and finite factors outside [0,1] retain their contracts. The initial compact-only implementation still allocated 5,712 B per 1,000 period-20 updates; the guarded stages removed these allocations without weakening the assertion. Final production/benchmark builds passed, as did 11 state tests and 21 independent comparison/fixture tests. The initial fixture/probe batch passed 50 cases and inventory passed 71 checks. The new four-arm fixture measures native state creation and full-prefix parallel replay. Three warmed 1k samples: Full 58,240 B, LatestOnly 9,168 B, native serial 48,376 B, native parallel 191,000 B. Full still allocates more than native serial. Package-pinned source and raw evidence: `results/shared-rollout/holt-winter-cpu/`. No latency run or qualification increase (186/401).

### Compact expanding EMA recurrences (2026-10-12)

ExpandingRegularizedEma now caches the exact denominator after the effective startup period stops changing and computes its numerator with compact dyadic arithmetic. ExpandingZeroLagEma uses the same compact arithmetic for its complete lag-adjusted ratio. Wide coefficients, maximum periods, seed/signed-zero behavior and complete-ratio rounding retain the established fallback contracts. Production and benchmark builds passed; 10 state tests (frozen prior algorithms plus zero warmed allocation), 58 comparison/probe cases and 72 inventory checks passed. The new four-arm fixture covers QuanTAlib.Rema and QuanTAlib.Zlema with independent native/exact references and full-prefix native parallel replay. Package-pinned source: `lib/averages/Rema.cs` and `Zlema.cs` at QuanTAlib commit a2af99711d22a2833af2eb18054f0049a3fb0224 (package 1.0.0). Timing is pending; qualification stays 186/401.

### Recursive/Ulcer combined diagnostic (2026-10-12)

A single 20-case 10k batch at 5450bd1c covered expanding Rema/Zlema, Holt-Winter, updated Laguerre and Ulcer. All setup validations and measurements completed, but an AiDotNet.Tensors build appeared in the completion process sample and CPU load reached 74% (6% immediately before timing). Overlap cannot be ruled out. All timing is retained as diagnostic only in `results/shared-rollout/recursive-diagnostic-10k/`; no catalog entry changed and qualification stays 186/401. No rerun. Managed LatestOnly allocations were Rema 81,336 B, Zlema 81,464 B, Holt-Winter 81,330 B, Laguerre 81,362 B and Ulcer 171,071 B. The diagnostic still indicates substantial recurrence costs despite lower allocations; it establishes no latency win. BDN iteration warnings/outliers, raw data and process samples are preserved.

### Preserved pivot columns against the cached baseline (2026-10-16)

The preserved 033e31ab pivot-column candidate completed the four builder measurements with no detected interference. The saved native measurements were reused on matching recorded hardware/runtime. All four current builder confidence intervals are below the saved fastest-native interval. These are cross-run comparisons, not same-run qualification; the two interrupted attempts are excluded.

| Bars | Builder history | Mean (us) | Managed bytes | Current / previous builder | Current / saved fastest native |
| ---: | --- | ---: | ---: | ---: | ---: |
| 1,000 | Full | 127.602 | 139,236 | 0.269 | 0.817 |
| 1,000 | LatestOnly | 119.624 | 90,487 | 0.259 | 0.766 |
| 10,000 | Full | 1,088.500 | 1,219,718 | 0.228 | 0.606 |
| 10,000 | LatestOnly | 1,061.522 | 738,594 | 0.225 | 0.591 |

Evidence: `results/shared-rollout/rolling-pivot-shared-cpu/reused-baseline-columns/` contains raw timing, cached-baseline comparisons, immutable candidate hashes and quiet-process receipts. No native or competitor timing was repeated.

### Preserved Rickshaw workers against the cached baseline (2026-10-16)

The four builder measurements completed without detected interference. The current means are 0.202-0.428 times the saved builder means. Only 10k LatestOnly has a confidence interval below the saved fastest-native interval; 1k Full remains slower, and the other two cases do not establish a latency win. The native baseline was reused, so comparisons are cross-run.

| Bars | Builder history | Mean (us) | Managed bytes | Current / saved fastest native |
| ---: | --- | ---: | ---: | ---: |
| 1,000 | Full | 34.323 | 61,237 | 1.448 |
| 1,000 | LatestOnly | 23.806 | 12,203 | 1.005 |
| 10,000 | Full | 210.944 | 565,628 | 0.994 |
| 10,000 | LatestOnly | 158.305 | 84,215 | 0.746 |

Evidence: `results/shared-rollout/rickshaw-shared-cpu/reused-baseline-workers/`. Preserve the 1k Full gap and the two inconclusive cases for further optimization; this result does not qualify every Rickshaw workload as faster than native.

### Preserved Asin workers against the cached baseline (2026-10-16)

The four builder measurements completed without detected interference. All four remain slower than the cached fastest native mean; none establishes a performance win. Native measurements were reused on matching recorded hardware/runtime, so these are cross-run comparisons.

| Bars | Builder history | Mean (us) | Managed bytes | Current / previous builder | Current / saved fastest native |
| ---: | --- | ---: | ---: | ---: | ---: |
| 1,000 | Full | 17.160 | 66,567 | 1.009 | 3.327 |
| 1,000 | LatestOnly | 11.957 | 17,971 | 0.913 | 2.318 |
| 10,000 | Full | 89.567 | 642,974 | 0.913 | 3.041 |
| 10,000 | LatestOnly | 45.931 | 161,988 | 1.192 | 1.559 |

Evidence: `results/shared-rollout/asin-shared-cpu/reused-baseline-workers/`. Recording accepts a missing final process command line only after matching the previously proven PID, creation time and monotonic CPU total; changed identities, unexpected commands and background CPU limits remain enforced. The completed measurement was recorded without rerunning it.

### Preserved SMA workers against the cached baseline (2026-10-16)

Four builder measurements completed without detected interference. All four remain slower than the saved fastest TALib native mean and none establishes a latency win. Identical builder code and inputs are timed once under the TALib operation label; cached native results are reused on matching recorded hardware/runtime. Comparisons are cross-run.

| Bars | Builder history | Mean (us) | Managed bytes | Current / previous builder | Current / saved fastest native |
| ---: | --- | ---: | ---: | ---: | ---: |
| 1,000 | Full | 9.615 | 57,741 | 1.134 | 3.884 |
| 1,000 | LatestOnly | 6.746 | 9,035 | 1.019 | 2.725 |
| 10,000 | Full | 66.570 | 562,618 | 0.890 | 5.757 |
| 10,000 | LatestOnly | 20.302 | 81,631 | 1.035 | 1.756 |

Evidence: `results/shared-rollout/sma-cpu/reused-baseline-workers/`. These gaps remain open; no competitor measurement was repeated.

### Skender RSI/ADX first direct-native baseline (2026-10-16)

All 16 native/builder cases completed in one run without detected interference. Native serial and full-prefix parallel results are now cached for reuse. Every builder case remains slower than the fastest native mean; no workload qualifies as a latency win. The interrupted first attempt is excluded.

| Operation | Bars | Builder history | Mean (us) | Managed bytes | Builder / fastest native |
| --- | ---: | --- | ---: | ---: | ---: |
| Skender.GetRsi | 1,000 | Full | 243.886 | 66,401 | 4.149 |
| Skender.GetRsi | 1,000 | LatestOnly | 225.026 | 17,643 | 3.828 |
| Skender.GetRsi | 10,000 | Full | 2,403.441 | 642,914 | 2.650 |
| Skender.GetRsi | 10,000 | LatestOnly | 2,379.979 | 161,784 | 2.624 |
| Skender.GetAdx | 1,000 | Full | 1,475.513 | 116,590 | 12.400 |
| Skender.GetAdx | 1,000 | LatestOnly | 1,715.316 | 67,834 | 14.415 |
| Skender.GetAdx | 10,000 | Full | 16,911.649 | 1,126,086 | 11.838 |
| Skender.GetAdx | 10,000 | LatestOnly | 16,881.285 | 644,989 | 11.817 |

Evidence: `results/shared-rollout/skender-strength/first-native-baseline/`. Comparisons are from the same run on the preserved 2e533ff2 candidate. RSI and ADX latency gaps remain open; retain these native baselines when measuring later candidates.
