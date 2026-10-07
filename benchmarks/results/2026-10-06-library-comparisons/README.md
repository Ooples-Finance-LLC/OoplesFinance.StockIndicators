# Initial full-library comparison measurement

TA-Lib.NETCore 0.5.0 MinMax versus Ooples LowestLow + HighestHigh on the same close
stream, period 20, both complete output trajectories returned. Four normal
BenchmarkDotNet ShortRun cases completed in 41 seconds on the local Windows host.
This uses InProcessEmitToolchain, three warmup iterations and three measured
iterations, with complete-workload correctness checked in GlobalSetup. It is an
initial measurement, not a precise cross-machine performance claim.

The JSON retains full parameter names (the Markdown table truncates them), timing
samples, memory measurements and machine/runtime metadata. At 1,000 bars Ooples
averaged 365.7 microseconds versus TA-Lib's 10.4; at 10,000 bars 3.708 milliseconds
versus 136.6 microseconds. These compare public execution paths including result
allocation. Ooples builds a two-indicator runtime; TA-Lib has a combined MinMax
routine. Inputs are prepared outside timing. The difference is retained as an
observed result, not corrected through benchmark-only arithmetic.

Original measurement command (with COMPARISON_PAIR=TaLib.Functions.MinMax):

```sh
dotnet OoplesFinance.StockIndicators.CompetitorBenchmarks.dll --filter '*LibraryPairBenchmarks*' --inProcess --job short --exporters json --artifacts performance
```

The permanent workflow runs every implemented pair in up to 20 parallel shards
after correctness passes. Current runs use the built-in `LibraryTimingConfig`
without CLI toolchain/job overrides: one invocation per iteration, three warmups,
three measured iterations, and a bounded 30-minute in-process case timeout.
Earlier reports retain their original settings and samples. It checks both arms at both input sizes
and rejects missing or failed timing results. Remaining unimplemented API families
are listed explicitly in the committed manifest.

## Containment and Doji Star measurements

Harami, Harami Cross, Homing Pigeon, and Doji Star each completed both arms at
1,000 and 10,000 bars with default 10-bar thresholds. All sixteen cases passed the
performance evidence gate (three measured iterations each). The four runs executed
sequentially on the same host, taking 28–38 seconds each. Their new binary was
checked by 610 contract/inventory tests and complete independent trajectories for
all four pairs. Each benchmark setup also checks both actual timed delegates once.

| Pattern | Bars | Ooples mean (µs) | TA-Lib mean (µs) |
| --- | ---: | ---: | ---: |
| Harami | 1,000 | 266.43 | 56.32 |
| Harami | 10,000 | 2933.19 | 662.17 |
| HaramiCross | 1,000 | 254.21 | 62.42 |
| HaramiCross | 10,000 | 2826.71 | 657.02 |
| HomingPigeon | 1,000 | 258.55 | 43.51 |
| HomingPigeon | 10,000 | 3018.90 | 492.01 |
| DojiStar | 1,000 | 264.62 | 63.77 |
| DojiStar | 10,000 | 2785.11 | 640.41 |

These measurements include Ooples' public builder runtime and exact rolling
threshold comparisons. Both arms return the complete output series. The observed
performance differences are retained; these short runs are initial evidence, not
precision claims. The corresponding `*-short.json.gz` files preserve raw timing
samples and environment metadata; `*-short.github.md` files are the original
BenchmarkDotNet tables. Commands use the same options above, with COMPARISON_PAIR
set to each TaLib.Candles API name.

## Matching-close and sequence measurements

Matching Low, Stick Sandwich, Three Line Strike, and Three Outside each completed
both arms at both sizes. All sixteen timing cases passed the evidence gate. Runs
executed sequentially in 27–33 seconds each, with default five-bar adaptive
thresholds where applicable; Three Outside has no averaging period. Verification
included 656 contract/inventory tests and full trajectories for all four new pairs.

| Pattern | Bars | Ooples mean (µs) | TA-Lib mean (µs) |
| --- | ---: | ---: | ---: |
| MatchingLow | 1,000 | 206.80 | 28.36 |
| MatchingLow | 10,000 | 2196.68 | 294.85 |
| StickSandwich | 1,000 | 194.62 | 23.64 |
| StickSandwich | 10,000 | 2165.11 | 250.96 |
| ThreeLineStrike | 1,000 | 217.30 | 55.28 |
| ThreeLineStrike | 10,000 | 2347.83 | 549.05 |
| ThreeOutside | 1,000 | 178.61 | 4.76 |
| ThreeOutside | 10,000 | 2057.47 | 68.41 |

The matching JSON archives preserve raw samples and metadata. These public-path
measurements include output allocation and exact adaptive comparisons in Ooples;
results are initial ShortRun evidence under the same conditions described above.

## Moving-average measurements

SMA, WMA, and EMA each compare both arms at both sizes across all four packages.
All 48 cases passed the timing evidence gate with three measured iterations each.
The EMA batch passed 667 contract tests and full trajectories for all four pairs.
Trady EMA uses the first-value seed; the other three use an initial SMA.
Runs executed sequentially under the same ShortRun conditions described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Sma | 1,000 | 142.02 | 90.48 |
| QuanTAlib.Sma | 10,000 | 1662.25 | 940.74 |
| QuanTAlib.Wma | 1,000 | 196.90 | 102.38 |
| QuanTAlib.Wma | 10,000 | 2176.79 | 1023.24 |
| Skender.GetSma | 1,000 | 148.43 | 55.53 |
| Skender.GetSma | 10,000 | 1824.18 | 621.26 |
| Skender.GetWma | 1,000 | 206.15 | 67.28 |
| Skender.GetWma | 10,000 | 2356.60 | 790.41 |
| TaLib.Functions.Sma | 1,000 | 155.65 | 3.32 |
| TaLib.Functions.Sma | 10,000 | 1825.94 | 35.75 |
| TaLib.Functions.Wma | 1,000 | 209.09 | 3.16 |
| TaLib.Functions.Wma | 10,000 | 2316.44 | 29.15 |
| Trady.Indicator.SimpleMovingAverage | 1,000 | 148.83 | 758.02 |
| Trady.Indicator.SimpleMovingAverage | 10,000 | 1742.75 | 9920.85 |
| Trady.Indicator.WeightedMovingAverage | 1,000 | 198.52 | 2127.53 |
| Trady.Indicator.WeightedMovingAverage | 10,000 | 2240.09 | 24022.50 |
| QuanTAlib.Ema | 1,000 | 181.97 | 47.37 |
| QuanTAlib.Ema | 10,000 | 2114.25 | 474.58 |
| Skender.GetEma | 1,000 | 181.69 | 35.43 |
| Skender.GetEma | 10,000 | 2088.23 | 502.47 |
| TaLib.Functions.Ema | 1,000 | 185.63 | 3.81 |
| TaLib.Functions.Ema | 10,000 | 2121.37 | 37.34 |
| Trady.Indicator.ExponentialMovingAverage | 1,000 | 188.61 | 3525.25 |
| Trady.Indicator.ExponentialMovingAverage | 10,000 | 2119.68 | 255887.03 |

Raw reports preserve the measured differences, samples, and machine metadata.

## Momentum and return measurements

The five TA-Lib forms (Mom, Roc, RocP, RocR, and RocR100) each completed both
arms at both input sizes. All twenty timing cases passed the evidence gate.
Correctness included 680 contract tests and 11,896 full-trajectory values per
pair. Runs executed sequentially with the same ShortRun settings described above.

| Form | Bars | Ooples mean (µs) | TA-Lib mean (µs) |
| --- | ---: | ---: | ---: |
| Mom | 1,000 | 193.78 | 2.03 |
| Mom | 10,000 | 2209.99 | 21.81 |
| Roc | 1,000 | 471.60 | 2.52 |
| Roc | 10,000 | 5248.12 | 23.09 |
| RocP | 1,000 | 484.31 | 2.51 |
| RocP | 10,000 | 5797.58 | 22.79 |
| RocR | 1,000 | 435.04 | 2.80 |
| RocR | 10,000 | 5404.37 | 23.27 |
| RocR100 | 1,000 | 458.91 | 3.00 |
| RocR100 | 10,000 | 5104.96 | 23.95 |

These timings include the public execution paths and returned arrays. Raw
reports preserve samples, machine metadata, and observed differences.

## Trady directional momentum measurements

Momentum, gain/loss momentum, and the three generic difference families each
completed both arms at both sizes. All 24 timing cases passed the evidence gate.
Verification covered 695 contract tests, 11,896 full-trajectory values per pair,
and regression trajectories for the five existing TA-Lib change forms.
Benchmarks executed sequentially with the same ShortRun settings described above.

| Family | Bars | Ooples mean (µs) | Trady mean (µs) |
| --- | ---: | ---: | ---: |
| Momentum | 1,000 | 197.03 | 419.08 |
| Momentum | 10,000 | 2195.51 | 9203.50 |
| Difference | 1,000 | 187.37 | 462.42 |
| Difference | 10,000 | 2116.61 | 9512.42 |
| UpMomentum | 1,000 | 179.41 | 573.37 |
| UpMomentum | 10,000 | 2104.84 | 9605.68 |
| PositiveDifference | 1,000 | 178.00 | 552.57 |
| PositiveDifference | 10,000 | 2233.81 | 9648.12 |
| DownMomentum | 1,000 | 165.97 | 547.21 |
| DownMomentum | 10,000 | 2123.21 | 10027.15 |
| NegativeDifference | 1,000 | 167.52 | 536.34 |
| NegativeDifference | 10,000 | 2187.28 | 9867.35 |

Raw reports retain timing samples and machine metadata. Results include public
execution and output allocation, with no adjustment to observed differences.

## Trady nullable percentage-return measurements

Both rate-of-change APIs completed both arms at both sizes. All eight cases
passed the timing evidence gate. Verification included 703 contract tests and
11,779 present mature values per pair, with explicit missing-value mask checks.
Both arms preserve presence information; Ooples returns it from its public
IsDefined output. Runs used the same sequential ShortRun settings described above.

| Family | Bars | Ooples mean (µs) | Trady mean (µs) |
| --- | ---: | ---: | ---: |
| RateOfChange | 1,000 | 482.50 | 476.03 |
| RateOfChange | 10,000 | 5858.62 | 9301.20 |
| PercentageDifference | 1,000 | 480.64 | 479.81 |
| PercentageDifference | 10,000 | 6621.82 | 9974.97 |

Raw reports retain samples, runtime metadata, and measured differences.

## Skender ROC with optional averaging

All four timing cases passed the evidence gate in a 34-second ShortRun. Both
arms return momentum, ROC, and the 20-bar ROC average, with explicit presence
information. Verification included 715 tests and 34,996 full-trajectory values;
tests also exercise disabled averaging, multiple averaging periods, zero
denominators, and recovery after missing values leave the window.

| Bars | Ooples mean (µs) | Skender mean (µs) |
| ---: | ---: | ---: |
| 1,000 | 623.75 | 88.42 |
| 10,000 | 6626.84 | 938.74 |

Raw reports retain measured samples and runtime metadata under the conditions
described above. No adjustment is made to observed performance differences.

## Skender ROC RMS bands

All four timing cases passed the evidence gate in 33 seconds. Both arms return
ROC, EMA, and both RMS bands with explicit presence information. Timings use
lag 20, EMA period 3, and RMS window 3. Verification covered 725 contract tests,
an additional missing-seed regression, and 46,929 full-trajectory values.

| Bars | Ooples mean (µs) | Skender mean (µs) |
| ---: | ---: | ---: |
| 1,000 | 2434.58 | 91.78 |
| 10,000 | 23976.75 | 1781.99 |

The original reports retain raw samples and runtime metadata under the ShortRun
conditions above. No adjustment is made to measured differences.

## DEMA and TEMA initialization variants

All sixteen timing cases passed the evidence gate. Skender comparisons use a
shared SMA seed; TA-Lib comparisons seed each EMA stage separately. Each public
Ooples counterpart uses the corresponding formula and returns the complete series.
Verification included 739 tests and full independent trajectories for all four
pairs. The benchmark period is 20, with the same sequential ShortRun settings above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetDema | 1,000 | 283.98 | 38.13 |
| Skender.GetDema | 10,000 | 3066.00 | 480.65 |
| Skender.GetTema | 1,000 | 336.53 | 38.29 |
| Skender.GetTema | 10,000 | 3927.63 | 562.53 |
| TaLib.Functions.Dema | 1,000 | 281.91 | 9.32 |
| TaLib.Functions.Dema | 10,000 | 3381.31 | 87.47 |
| TaLib.Functions.Tema | 1,000 | 323.27 | 11.12 |
| TaLib.Functions.Tema | 10,000 | 3648.65 | 110.17 |

Raw reports retain samples, environment metadata, and measured differences.

## Triangular windows and rolling sums

All twelve timing cases passed the evidence gate. Triangular comparisons use an
exact 20-price window; sum uses the same full window. Verification covered 747
contract tests and 11,970 full-trajectory values per pair. Tests include odd/even
weights, extreme cancellation, subnormal rounding, and explicit period boundaries.
Runs used the same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Trima | 1,000 | 203.33 | 3.66 |
| TaLib.Functions.Trima | 10,000 | 2288.08 | 35.20 |
| QuanTAlib.Trima | 1,000 | 208.93 | 103.12 |
| QuanTAlib.Trima | 10,000 | 2457.31 | 1004.96 |
| TaLib.Functions.Sum | 1,000 | 182.66 | 2.90 |
| TaLib.Functions.Sum | 10,000 | 2071.18 | 25.15 |

Raw reports retain timing samples, machine metadata, and observed differences.

## On-balance volume

Both arms at both sizes passed the timing evidence gate for all three pairs.
The Skender pair returns OBV and its optional 20-bar average with presence flags.
TA-Lib and Trady start at first volume; Skender starts at zero. Correctness checks
covered 753 contract tests and 49,749 trajectory values including realistic varying
volumes, equal closes, negative initial prices, and zero volume. Timings use the
same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetObv | 1,000 | 267.15 | 94.16 |
| Skender.GetObv | 10,000 | 2941.49 | 989.62 |
| TaLib.Functions.Obv | 1,000 | 188.04 | 1.77 |
| TaLib.Functions.Obv | 10,000 | 2205.01 | 36.96 |
| Trady.Indicator.OnBalanceVolume | 1,000 | 188.55 | 534.69 |
| Trady.Indicator.OnBalanceVolume | 10,000 | 2513.54 | 9443.58 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Penetration candles

All eight timing cases passed the evidence gate. Verification covered 765
contract tests and 16,343 full-trajectory values for each new pair. Hand-checked
fixtures distinguish strict gaps, midpoint boundaries, prior/current long bodies,
configurable penetration, and subnormal/extreme prices. Trady PiercingLine is a
throwing stub and has no timed arm; it links to the verified TA-Lib counterpart.
Runs used the same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.DarkCloudCover | 1,000 | 201.62 | 26.05 |
| TaLib.Candles.DarkCloudCover | 10,000 | 2639.32 | 339.65 |
| TaLib.Candles.PiercingLine | 1,000 | 208.61 | 51.54 |
| TaLib.Candles.PiercingLine | 10,000 | 2356.40 | 599.67 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Strict Harami patterns

All twelve timing cases passed the evidence gate. Verification covered 777
contract tests and 51,256 full-trajectory values across three pairs. Tests pin
Trady directional object/generic open/close trends and ignored shadow arguments,
and the different plain-Harami semantics of directional tuple wrappers. Public
counterparts also verify strict boundaries, doji colors, shadow settings, and
extreme prices. Timings measure the object routes and use the same sequential
ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.BearishHarami | 1,000 | 152.74 | 689.87 |
| Trady.Candlestick.BearishHarami | 10,000 | 1896.50 | 9296.96 |
| Trady.Candlestick.BullishHarami | 1,000 | 149.09 | 692.94 |
| Trady.Candlestick.BullishHarami | 10,000 | 1868.86 | 9836.94 |
| Trady.Candlestick.Harami | 1,000 | 150.26 | 532.05 |
| Trady.Candlestick.Harami | 10,000 | 1797.05 | 9486.54 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Delayed Dark Cloud Cover

All four timing cases passed the evidence gate. Verification covered 787
contract tests and 17,171 full-trajectory values. Tests pin delayed output,
strict opening/closing inequalities, ignored long-body arguments, missing
downtrend confirmation, zero-delay failure, and object/generic/tuple equivalence.
Public exact midpoint comparisons also cover extreme and subnormal prices.
Timings use a 20-transition uptrend and three-bar delay with the same sequential
ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.DarkCloudCover | 1,000 | 148.11 | 617.62 |
| Trady.Candlestick.DarkCloudCover | 10,000 | 1776.53 | 7712.39 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Neck and thrusting candles

All twelve timing cases passed the evidence gate. Verification covered 804
contract tests and 17,288 full-trajectory values per pair. The 17 affected tests
passed again after strengthening the long-body equality check. Fixtures cover
strict gaps, inclusive tolerance/midpoint endpoints, white dojis, unequal windows,
excluded anchor ranges, and unrepresentable bodies/ranges and tiny prices.
Timings use default 10-body/5-range windows and the same sequential ShortRun
settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.InNeck | 1,000 | 223.47 | 50.68 |
| TaLib.Candles.InNeck | 10,000 | 2475.11 | 585.13 |
| TaLib.Candles.OnNeck | 1,000 | 223.16 | 51.58 |
| TaLib.Candles.OnNeck | 10,000 | 2409.53 | 547.87 |
| TaLib.Candles.Thrusting | 1,000 | 236.47 | 50.44 |
| TaLib.Candles.Thrusting | 10,000 | 2814.17 | 545.82 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Three-candle body patterns

All sixteen timing cases passed the evidence gate. Verification covered 857
contract tests and 18,421 full-trajectory values per pair. Thirty-one hand-calculated
fixtures distinguish strict/inclusive short-body thresholds, body gaps, containment,
colors, and lower-low rules. Additional checks cover exact ties, unequal windows,
extreme and subnormal values, and lifecycle. Trady UpsideGapTwoCrows is a throwing
stub and has no timed arm; its alternate counterpart is independently verified.
Timings use default 10-body windows and the same sequential ShortRun settings
described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.ThreeInside | 1,000 | 271.78 | 56.33 |
| TaLib.Candles.ThreeInside | 10,000 | 2877.43 | 662.62 |
| TaLib.Candles.TwoCrows | 1,000 | 214.84 | 25.88 |
| TaLib.Candles.TwoCrows | 10,000 | 2381.87 | 335.43 |
| TaLib.Candles.UniqueThreeRiver | 1,000 | 266.54 | 50.77 |
| TaLib.Candles.UniqueThreeRiver | 10,000 | 2858.70 | 640.85 |
| TaLib.Candles.UpsideGapTwoCrows | 1,000 | 268.75 | 46.79 |
| TaLib.Candles.UpsideGapTwoCrows | 10,000 | 2972.63 | 624.84 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Kicking and Rickshaw Man

All twelve timing cases passed the evidence gate. Verification covered 873
contract tests and 59,227 full-trajectory values across the three pairs. Fixtures
cover first-body selection on equal lengths, mirrored directions, full-range gap
ties, strict long-body and shadow thresholds, and inclusive doji/midpoint bounds.
Independent contracts also cover unequal periods, lifecycle, subnormals, and
intermediate body/range/midpoint overflow. Timings use default settings and the
same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.Kicking | 1,000 | 223.60 | 100.41 |
| TaLib.Candles.Kicking | 10,000 | 2458.68 | 1016.04 |
| TaLib.Candles.KickingByLength | 1,000 | 214.96 | 102.27 |
| TaLib.Candles.KickingByLength | 10,000 | 2304.06 | 1031.11 |
| TaLib.Candles.RickshawMan | 1,000 | 266.71 | 80.73 |
| TaLib.Candles.RickshawMan | 10,000 | 2923.81 | 809.18 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Matched candle lines

All twelve timing cases passed the evidence gate. Verification covered 916
contract tests and 62,059 full-trajectory values across three pairs. Twenty-nine
hand-calculated fixtures distinguish inclusive equal/near tolerances, strict body
and shadow thresholds, current versus prior historical windows, and gap direction.
Additional checks cover mirrored colors, dojis, unequal periods, lifecycle,
subnormal prices, and unrepresentable bodies/ranges. Timings use default settings
and the same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.Counterattack | 1,000 | 271.82 | 79.71 |
| TaLib.Candles.Counterattack | 10,000 | 2916.33 | 804.92 |
| TaLib.Candles.GapSideBySideWhiteLines | 1,000 | 241.55 | 45.24 |
| TaLib.Candles.GapSideBySideWhiteLines | 10,000 | 2599.53 | 531.71 |
| TaLib.Candles.SeparatingLines | 1,000 | 286.52 | 70.74 |
| TaLib.Candles.SeparatingLines | 10,000 | 3058.33 | 794.07 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Crow and soldier candles

All twelve timing cases passed the evidence gate. Verification covered 968
contract tests and 65,859 full-trajectory values across three pairs. Thirty-six
hand-calculated fixtures distinguish inclusive equal/near tolerances, strict body
and shadow thresholds, far-shrinkage thresholds, historical windows, and preceding candle colors.
Additional checks cover dojis, unequal periods, lifecycle,
subnormal prices, and unrepresentable bodies/ranges. Timings use default settings
and the same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.ThreeBlackCrows | 1,000 | 196.34 | 68.95 |
| TaLib.Candles.ThreeBlackCrows | 10,000 | 2224.82 | 682.39 |
| TaLib.Candles.IdenticalThreeCrows | 1,000 | 250.91 | 108.66 |
| TaLib.Candles.IdenticalThreeCrows | 10,000 | 2723.41 | 1195.33 |
| TaLib.Candles.ThreeWhiteSoldiers | 1,000 | 363.40 | 164.32 |
| TaLib.Candles.ThreeWhiteSoldiers | 10,000 | 3833.83 | 1576.80 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Morning and evening star reversals

All sixteen timing cases passed the evidence gate. Verification covered 1,052
contract tests and 98,032 full-trajectory values across four pairs. Sixty-six
hand-calculated fixtures distinguish inclusive middle-body thresholds, strict outer-body
thresholds, body gaps, historical windows, and strict penetration.
Additional checks cover five penetration settings, mirrored colors, dojis, unequal periods, lifecycle,
subnormal prices, and unrepresentable bodies/ranges. Timings use default settings
and the same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.MorningStar | 1,000 | 253.43 | 75.35 |
| TaLib.Candles.MorningStar | 10,000 | 2810.79 | 797.77 |
| TaLib.Candles.EveningStar | 1,000 | 251.88 | 74.16 |
| TaLib.Candles.EveningStar | 10,000 | 2749.43 | 796.82 |
| TaLib.Candles.MorningDojiStar | 1,000 | 298.07 | 79.74 |
| TaLib.Candles.MorningDojiStar | 10,000 | 3272.97 | 815.34 |
| TaLib.Candles.EveningDojiStar | 1,000 | 301.55 | 79.95 |
| TaLib.Candles.EveningDojiStar | 10,000 | 3650.29 | 825.75 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Gap continuation and reversal candles

All sixteen timing cases passed the evidence gate. Verification covered 1,143
contract tests and 110,653 full-trajectory values across four pairs. Seventy-six
hand-calculated fixtures distinguish inclusive doji thresholds, strict shadow/body gaps and
containment thresholds, historical windows, and strict penetration.
Four affected star pairs also passed 109,744 trajectory checks. Additional checks cover five penetration settings, mirrored colors, dojis, unequal periods, lifecycle,
subnormal prices, and unrepresentable bodies/ranges. Timings use default settings
and the same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.AbandonedBaby | 1,000 | 290.82 | 87.78 |
| TaLib.Candles.AbandonedBaby | 10,000 | 3179.55 | 850.97 |
| TaLib.Candles.Tristar | 1,000 | 229.89 | 33.82 |
| TaLib.Candles.Tristar | 10,000 | 2487.78 | 342.81 |
| TaLib.Candles.TasukiGap | 1,000 | 207.23 | 24.04 |
| TaLib.Candles.TasukiGap | 10,000 | 2551.02 | 291.57 |
| TaLib.Candles.UpDownSideGapThreeMethods | 1,000 | 187.37 | 5.60 |
| TaLib.Candles.UpDownSideGapThreeMethods | 10,000 | 1882.38 | 101.04 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Extended reversal candles

All twelve timing cases passed the evidence gate. The three new pairs passed
92,466 trajectory checks; five affected penetration pairs passed 154,610 checks.
Contract verification covered 1,240 distinct cases across the initial run and a
301-case targeted correction run. Eighty hand-calculated fixtures cover color
exceptions, strict gap endpoints, shadows, high/low movement, and full-range
engulfment. Six additional regressions pin exact-versus-rounded penetration
differences and prove that unexpected mismatches still fail verification.
Timings use the same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.Breakaway | 1,000 | 246.37 | 30.14 |
| TaLib.Candles.Breakaway | 10,000 | 2640.02 | 481.48 |
| TaLib.Candles.ConcealingBabySwallow | 1,000 | 203.41 | 63.32 |
| TaLib.Candles.ConcealingBabySwallow | 10,000 | 2182.42 | 705.28 |
| TaLib.Candles.LadderBottom | 1,000 | 200.20 | 23.60 |
| TaLib.Candles.LadderBottom | 10,000 | 2309.49 | 288.13 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Hikkake formations and confirmations

All eight timing cases passed the evidence gate. Both pairs passed 65,291 full-
trajectory checks; the full contract suite passed 1,295 tests. Forty-eight
hand-calculated mirrored sequences pin warmup priming, strict confirmation,
expiry after three bars, one-time consumption, new-formation priority, and near
tolerance boundaries. Additional checks cover period limits, lifecycle, overflow
of rolling ranges, and subnormal prices. Timings use the same sequential ShortRun
settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.Hikkake | 1,000 | 157.63 | 4.00 |
| TaLib.Candles.Hikkake | 10,000 | 1826.17 | 41.54 |
| TaLib.Candles.HikkakeModified | 1,000 | 180.58 | 21.87 |
| TaLib.Candles.HikkakeModified | 10,000 | 2108.33 | 290.62 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Five-candle continuation patterns

All eight timing cases passed the evidence gate. Both pairs passed 73,364 full-
trajectory checks; the full contract suite passed 1,404 tests. One hundred
hand-calculated fixtures cover strict body means, partial overlap, reaction
colors, continuation endpoints, final dojis, and penetration boundaries.
Mat Hold comparisons independently verify both exact and native-rounded
penetration contracts, including a pinned 0.1 boundary discrepancy. Tests also
cover six penetration settings, independent periods, lifecycle, overflowing
rolling bodies, and subnormal prices. Timings use the same sequential ShortRun
settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.MatHold | 1,000 | 287.62 | 107.28 |
| TaLib.Candles.MatHold | 10,000 | 3086.99 | 1118.52 |
| TaLib.Candles.RisingFallingThreeMethods | 1,000 | 288.24 | 141.62 |
| TaLib.Candles.RisingFallingThreeMethods | 10,000 | 3140.74 | 1373.02 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Exhaustion patterns

All twelve timing cases passed the evidence gate. The three pairs passed 124,608
full-trajectory checks; the complete contract suite passed 1,541 tests. Fixtures
cover deterioration branches, shoulder and shadow boundaries, independent
threshold periods, lifecycle, extreme and subnormal prices, and lazy storage.
Timings use the same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Candles.AdvanceBlock | 1,000 | 408.21 | 219.21 |
| TaLib.Candles.AdvanceBlock | 10,000 | 4375.82 | 2141.87 |
| TaLib.Candles.Stalled | 1,000 | 380.90 | 128.08 |
| TaLib.Candles.Stalled | 10,000 | 4190.65 | 1298.94 |
| TaLib.Candles.ThreeStarsInSouth | 1,000 | 357.69 | 101.07 |
| TaLib.Candles.ThreeStarsInSouth | 10,000 | 3759.23 | 1006.18 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Trend-qualified Tasuki gaps

All eight timing cases passed the evidence gate. The two pairs passed 85,480
full-trajectory checks; the complete contract suite passed 1,564 tests. Fixtures
cover asymmetric gap boundaries, strict trend endpoints, ignored size thresholds,
object/generic/tuple routes, lifecycle, extreme and subnormal prices, and lazy storage.
Timings use the same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.DownsideTasukiGap | 1,000 | 166.30 | 579.10 |
| Trady.Candlestick.DownsideTasukiGap | 10,000 | 1802.67 | 9907.46 |
| Trady.Candlestick.UpsideTasukiGap | 1,000 | 145.93 | 615.64 |
| Trady.Candlestick.UpsideTasukiGap | 10,000 | 1737.52 | 9155.97 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Trend-qualified abandoned babies

All eight timing cases passed the evidence gate. The two pairs passed 88,504
full-trajectory checks; the complete contract suite passed 1,595 tests. Fixtures
cover strict isolation gaps, percentile/body boundaries, unequal component windows,
object/generic/tuple routes, lifecycle, extreme and subnormal prices, and lazy storage.
Timings use the same sequential ShortRun settings described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.BearishAbandonedBaby | 1,000 | 476.84 | 717.99 |
| Trady.Candlestick.BearishAbandonedBaby | 10,000 | 5031.19 | 11334.05 |
| Trady.Candlestick.BullishAbandonedBaby | 1,000 | 452.49 | 770.97 |
| Trady.Candlestick.BullishAbandonedBaby | 10,000 | 4898.94 | 12686.64 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Trend-qualified stars

All twenty timing cases passed the evidence gate. Five API families passed
240,310 full-trajectory checks; the full contract suite passed 1,728 tests.
Independent references preserve the exact Ooples and rounded native midpoint
contracts, including a pinned boundary discrepancy. Zero midpoints return no
match in Ooples; native Trady can throw and is explicitly tested separately.
Timings use the bounded in-process ShortRun configuration described above.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.EveningDojiStar | 1,000 | 3430.97 | 3386.70 |
| Trady.Candlestick.EveningDojiStar | 10,000 | 5257.57 | 9166.77 |
| Trady.Candlestick.EveningStar | 1,000 | 4122.93 | 3333.37 |
| Trady.Candlestick.EveningStar | 10,000 | 8885.87 | 13114.60 |
| Trady.Candlestick.MoringinDojiStar | 1,000 | 3472.28 | 1639.37 |
| Trady.Candlestick.MoringinDojiStar | 10,000 | 5188.50 | 5848.90 |
| Trady.Candlestick.MorningDojiStar | 1,000 | 3372.40 | 3206.33 |
| Trady.Candlestick.MorningDojiStar | 10,000 | 5265.07 | 9940.80 |
| Trady.Candlestick.MorningStar | 1,000 | 4055.90 | 3253.17 |
| Trady.Candlestick.MorningStar | 10,000 | 24970.62 | 11406.27 |

## Variable-length Three Methods

All eight timing cases passed the evidence gate. The two pairs passed 92,730
full-trajectory checks; the complete contract suite passed 1,632 tests. Fixtures
cover variable-length anchors, rejection barriers, overlapping percentiles,
object/generic/tuple routes, lifecycle, extreme and subnormal prices, and lazy storage.
Timings use sequential ShortRun jobs with one invocation per iteration and
an unroll factor of one for both arms, three warmups, and three measured
iterations. This bounds slow native calls; raw confidence intervals retain
the uncertainty from the small sample. Earlier calibration runs were aborted
and are not included.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.FallingThreeMethods | 1,000 | 3935.70 | 856658.50 |
| Trady.Candlestick.FallingThreeMethods | 10,000 | 7405.23 | 102898915.70 |
| Trady.Candlestick.RisingThreeMethods | 1,000 | 3911.60 | 816318.60 |
| Trady.Candlestick.RisingThreeMethods | 10,000 | 7316.57 | 104328603.78 |

Raw reports retain timing samples, machine metadata, and observed differences.

## Price candles, window extrema, and balance of power

All 36 timing cases passed the evidence gate. The initial full contract run
passed 1761 tests and exposed three test-harness warmup-budget failures.
Those cases now check maximum periods directly on short streams; all 20 affected
contracts passed after correction, without rebuilding production.
Nine independently checked pairs cover Skender Doji/Marubozu, five TA-Lib
rolling extrema/index/midpoint APIs, and both balance-of-power definitions.
Skender candle comparisons include all eleven calculated outputs and nullable
price presence. Its smoothed balance of power retains missing windows containing
a zero-range candle. Timings use the bounded in-process ShortRun configuration.
The separately verified Ooples Stars/ShortShadow definitions have no executable
Trady equivalent, so no native timing or formula parity is claimed for them.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetDoji | 1,000 | 6516.57 | 2163.10 |
| Skender.GetDoji | 10,000 | 24482.03 | 7370.53 |
| Skender.GetMarubozu | 1,000 | 5867.57 | 2292.10 |
| Skender.GetMarubozu | 10,000 | 20528.53 | 8140.17 |
| Skender.GetBop | 1,000 | 3752.07 | 539.43 |
| Skender.GetBop | 10,000 | 23939.57 | 1114.60 |
| TaLib.Functions.Bop | 1,000 | 4105.87 | 28.63 |
| TaLib.Functions.Bop | 10,000 | 12590.45 | 52.53 |
| TaLib.Functions.MinIndex | 1,000 | 2231.33 | 87.23 |
| TaLib.Functions.MinIndex | 10,000 | 10276.27 | 147.27 |
| TaLib.Functions.MaxIndex | 1,000 | 2176.87 | 113.90 |
| TaLib.Functions.MaxIndex | 10,000 | 11059.08 | 159.87 |
| TaLib.Functions.MinMaxIndex | 1,000 | 3074.20 | 209.77 |
| TaLib.Functions.MinMaxIndex | 10,000 | 5603.13 | 313.23 |
| TaLib.Functions.MidPoint | 1,000 | 2431.90 | 174.40 |
| TaLib.Functions.MidPoint | 10,000 | 12168.47 | 884.47 |
| TaLib.Functions.MidPrice | 1,000 | 2429.17 | 196.73 |
| TaLib.Functions.MidPrice | 10,000 | 11517.37 | 1148.40 |

## Accumulation/distribution

All twelve timing cases passed the evidence gate. The full contract run passed
1,777 tests; all 17 focused checks passed after strengthening the independent
oracle. Three parallel trajectories verified 141,184 output values.
Timings use a common 1/1024 price grid that is exactly representable as both
decimal and binary64. The original unrounded 1,000/10,000-bar workloads remain
additional correctness fixtures, with separate input-conversion and native-rounding
references. The oracle preserves the existing comparison tolerance.
Skender includes all four outputs and optional-average presence. Trady uses a
first-volume seed and price changes on flat candles; undefined ratios leave
all subsequent results missing. TA-Lib uses the existing public Adl indicator.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetAdl | 1,000 | 7751.97 | 712.23 |
| Skender.GetAdl | 10,000 | 23069.03 | 1923.70 |
| TaLib.Functions.Ad | 1,000 | 11519.67 | 36.43 |
| TaLib.Functions.Ad | 10,000 | 28996.87 | 56.90 |
| Trady.Indicator.AccumulationDistributionLine | 1,000 | 6820.27 | 3168.50 |
| Trady.Indicator.AccumulationDistributionLine | 10,000 | 17983.78 | 12867.87 |

## Rolling median and percentile

All sixteen timing cases passed the evidence gate. All 31 focused percentile/ADL
contracts and two inventory checks passed. Four parallel trajectories verified
49,488 values. QuanTAlib startup averages are included; Trady startup is missing.
The percentile uses exact binary64 ranks and convex interpolation, while separate
references preserve native binary64 stages and decimal input conversion.
Tests cover endpoint/interior ranks, odd/even windows, duplicate expiration,
subnormal rounding, finite extreme-price interpolation, reset, lazy large periods,
Trady generic/tuple routes, and corruption of either comparison arm.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Median | 1,000 | 4651.47 | 2172.33 |
| QuanTAlib.Median | 10,000 | 9265.57 | 4125.80 |
| QuanTAlib.Percentile | 1,000 | 4926.80 | 2218.03 |
| QuanTAlib.Percentile | 10,000 | 14613.87 | 5690.53 |
| Trady.Indicator.Median | 1,000 | 4545.70 | 10373.07 |
| Trady.Indicator.Median | 10,000 | 25668.52 | 30373.37 |
| Trady.Indicator.Percentile | 1,000 | 4658.73 | 10069.33 |
| Trady.Indicator.Percentile | 10,000 | 12717.97 | 21338.40 |

## Wilder averages

All twelve timing cases passed the evidence gate. All 12 focused contracts and
inventory checks passed. Three parallel trajectories verified 37,512 values.
Skender and QuanTAlib Smma use a mean seed; QuanTAlib startup averages are included.
Trady ModifiedMovingAverage starts its recurrence from the first close. Separate
references verify binary64/decimal package rounding and exact convex Ooples arithmetic.
Tests pin seed transitions, period one, maximum periods without eager allocation,
extreme/subnormal preservation, lifecycle, Trady tuple/generic routes, and
wrong-seed, wrong-coefficient, and corrupted-competitor detection.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetSmma | 1,000 | 2369.07 | 475.23 |
| Skender.GetSmma | 10,000 | 5171.70 | 588.70 |
| QuanTAlib.Smma | 1,000 | 2363.23 | 224.87 |
| QuanTAlib.Smma | 10,000 | 8458.00 | 616.63 |
| Trady.Indicator.ModifiedMovingAverage | 1,000 | 2371.57 | 27056.97 |
| Trady.Indicator.ModifiedMovingAverage | 10,000 | 3371.18 | 243146.50 |

## Seeded average true range

All twelve timing cases passed the evidence gate. All 12 focused contracts and
inventory checks passed. Three parallel trajectories verified 60,153 values.
The first candle establishes previous close; the seed averages ranges 1..period.
Skender includes true range, ATR, and ATR percentage with all presence flags.
Zero close makes only its percentage absent; later nonzero closes recover.
Independent references preserve exact Ooples stages and native binary64/decimal
arithmetic. Regression tests cover an oversized unpublished range with a finite
average, subnormals, maximum periods, lifecycle, Trady tuple/generic routes,
period-one package differences, and corruption of every Skender output/presence.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetAtr | 1,000 | 6473.43 | 1022.37 |
| Skender.GetAtr | 10,000 | 21534.87 | 1475.07 |
| TaLib.Functions.Atr | 1,000 | 4820.37 | 46.77 |
| TaLib.Functions.Atr | 10,000 | 13258.37 | 175.17 |
| Trady.Indicator.AverageTrueRange | 1,000 | 4959.73 | 4150.43 |
| Trady.Indicator.AverageTrueRange | 10,000 | 18050.90 | 12525.40 |

## QuanTAlib ATR bar-route discrepancy

All four timing cases and 11 focused contract/inventory checks passed; the full
trajectory verified 12,748 values. QuanTAlib 1.0.0 Calc(TBar) leaves Input.IsNew
false and forwards it to its EMA, restoring zero EMA state on every bar. Its normal
bar stream returns (1/period)*trueRange instead of a smoothed ATR. Direct and
event-source regressions pin this behavior; the counterpart is explicitly named
ScaledTrueRange. Conventional seeded ATR remains available separately.
Independent references distinguish native stage rounding from exact Ooples scaling.
Overflow-before-scaling, subnormal rounding, reset, and corruption are tested.
These timings compare the observed scaled-range formula, not a corrected EMA.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Atr | 1,000 | 4690.63 | 291.30 |
| QuanTAlib.Atr | 10,000 | 14970.27 | 531.45 |

## Normalized seeded ATR

All four timing cases and 21 focused/affected contract and inventory checks passed.
The full trajectory verified 11,917 values. The Ooples counterpart consistently
publishes signed 100*ATR/close, with zero for zero close and startup. Native TA-Lib
period one instead returns raw range; later zero closes overwrite the first result
and leave the current slot untouched. Sentinel, prefix-stability, period-one and
corruption tests pin these differences using separate independent references.
Positive-price timing workloads exercise the same normalized formula. Extended
intermediate ranges and averages preserve finite percentages; subnormal ratios,
lifecycle and maximum periods are covered.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Natr | 1,000 | 5833.90 | 57.20 |
| TaLib.Functions.Natr | 10,000 | 16293.63 | 189.30 |

## Window regression readings

All twenty timing cases and 11 focused contract/inventory checks passed. Five
parallel trajectories verified 59,955 values. WindowLinearRegression selects
the existing exact fit engine's endpoint, one-step forecast, slope, or a new local
intercept at x=0 for the oldest candle. Existing global-intercept behavior remains
unchanged and is pinned alongside the other existing readings. Independent centered
rational and batch integer-grid references cover window expiration, coordinates,
subnormals, oversized unselected outputs, lazy maximum periods, lifecycle, and
corruption of both arms. Native period-one rejection is explicitly tested.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.LinearReg | 1,000 | 6144.13 | 54.33 |
| TaLib.Functions.LinearReg | 10,000 | 23055.00 | 471.17 |
| TaLib.Functions.LinearRegSlope | 1,000 | 5772.17 | 56.37 |
| TaLib.Functions.LinearRegSlope | 10,000 | 18271.95 | 410.30 |
| TaLib.Functions.LinearRegIntercept | 1,000 | 5831.43 | 50.13 |
| TaLib.Functions.LinearRegIntercept | 10,000 | 24948.57 | 441.38 |
| TaLib.Functions.Tsf | 1,000 | 5875.73 | 50.40 |
| TaLib.Functions.Tsf | 10,000 | 23631.83 | 508.23 |
| Skender.GetEpma | 1,000 | 6061.23 | 613.37 |
| Skender.GetEpma | 10,000 | 28566.27 | 1329.87 |

## Convolution and fixed endpoint weights

Endpoint timing rows and raw reports were refreshed after the rolling-moment update below.

All eight timing cases and 19 focused contract/inventory checks passed. Two
parallel trajectories verified 25,520 values. Kernels are newest first and
normalize available prefixes by their sum. Zero-sum kernels retain their signed
weights and divide by available count. Endpoint weights are fixed at the requested
period during startup, then equal the full-window regression endpoint. Exact
Ooples ratios and native coefficient/normalization stages have separate references.
Signed, zero, subnormal and overflowing intermediates, copied immutable weights,
startup orientation, expiration, lifecycle and corruption are covered.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Convolution | 1,000 | 14230.13 | 392.40 |
| QuanTAlib.Convolution | 10,000 | 20700.97 | 1027.83 |
| QuanTAlib.Epma | 1,000 | 8627.97 | 576.57 |
| QuanTAlib.Epma | 10,000 | 17387.27 | 1217.27 |

## Mean-startup endpoint and rolling moments

All 36 affected contract/inventory checks passed, including a complete 46,341-bar
regression: QuanTAlib Mma overflows its Int32 period*(period+1) denominator and
reverses the trend correction; Ooples retains the finite regression endpoint.
Two trajectories verified 25,508 values. All eight timing cases passed; Epma
reports replace its earlier measurements, adding four unique Mma cases overall.
Both endpoint startup conventions now use exact rolling moments instead of a
window scan. Mma publishes expanding means before the complete endpoint window;
Epma retains fixed requested-period weights. Independent weighted references,
startup, extremes, subnormals, maximum periods, lifecycle and corruption passed.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Mma | 1,000 | 6158.10 | 899.37 |
| QuanTAlib.Mma | 10,000 | 17445.58 | 1615.57 |
| QuanTAlib.Epma | 1,000 | 8627.97 | 576.57 |
| QuanTAlib.Epma | 10,000 | 17387.27 | 1217.27 |

## Earlier candle and price timing coverage

28 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 628 arms across 157 pairs;
28 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetTr | 1,000 | 1893.80 | 466.73 |
| Skender.GetTr | 10,000 | 9747.23 | 983.63 |
| TaLib.Candles.BeltHold | 1,000 | 2670.77 | 896.37 |
| TaLib.Candles.BeltHold | 10,000 | 25425.17 | 751.17 |
| TaLib.Candles.ClosingMarubozu | 1,000 | 2695.63 | 871.43 |
| TaLib.Candles.ClosingMarubozu | 10,000 | 22283.22 | 768.73 |
| TaLib.Candles.Doji | 1,000 | 2094.80 | 497.40 |
| TaLib.Candles.Doji | 10,000 | 12681.77 | 392.47 |
| TaLib.Candles.DragonflyDoji | 1,000 | 2273.63 | 740.00 |
| TaLib.Candles.DragonflyDoji | 10,000 | 13630.63 | 601.67 |
| TaLib.Candles.Engulfing | 1,000 | 2311.07 | 60.17 |
| TaLib.Candles.Engulfing | 10,000 | 10811.87 | 157.80 |
| TaLib.Candles.GravestoneDoji | 1,000 | 2282.43 | 735.50 |
| TaLib.Candles.GravestoneDoji | 10,000 | 14358.03 | 594.13 |
| TaLib.Candles.Hammer | 1,000 | 2607.50 | 1427.70 |
| TaLib.Candles.Hammer | 10,000 | 6530.53 | 1225.27 |
| TaLib.Candles.HangingMan | 1,000 | 2618.50 | 1411.43 |
| TaLib.Candles.HangingMan | 10,000 | 4691.00 | 1216.73 |
| TaLib.Candles.HighWave | 1,000 | 2759.63 | 979.07 |
| TaLib.Candles.HighWave | 10,000 | 5019.57 | 987.33 |
| TaLib.Candles.InvertedHammer | 1,000 | 2405.43 | 1330.73 |
| TaLib.Candles.InvertedHammer | 10,000 | 8351.93 | 990.10 |
| TaLib.Candles.LongLeggedDoji | 1,000 | 2315.33 | 780.77 |
| TaLib.Candles.LongLeggedDoji | 10,000 | 12624.13 | 611.47 |
| TaLib.Candles.LongLine | 1,000 | 2672.87 | 1014.63 |
| TaLib.Candles.LongLine | 10,000 | 4989.83 | 1021.48 |
| TaLib.Candles.Marubozu | 1,000 | 2737.50 | 896.23 |
| TaLib.Candles.Marubozu | 10,000 | 5316.33 | 771.97 |
| TaLib.Candles.ShootingStar | 1,000 | 2511.20 | 1224.47 |
| TaLib.Candles.ShootingStar | 10,000 | 7322.83 | 1085.53 |
| TaLib.Candles.ShortLine | 1,000 | 2662.30 | 1058.77 |
| TaLib.Candles.ShortLine | 10,000 | 22844.70 | 966.83 |
| TaLib.Candles.SpinningTop | 1,000 | 2691.97 | 599.57 |
| TaLib.Candles.SpinningTop | 10,000 | 4950.75 | 512.70 |
| TaLib.Candles.TakuriLine | 1,000 | 2291.50 | 1031.83 |
| TaLib.Candles.TakuriLine | 10,000 | 13327.30 | 755.92 |
| TaLib.Functions.AvgPrice | 1,000 | 1882.00 | 35.53 |
| TaLib.Functions.AvgPrice | 10,000 | 11034.07 | 275.47 |
| TaLib.Functions.Max | 1,000 | 2065.77 | 71.53 |
| TaLib.Functions.Max | 10,000 | 10153.57 | 108.57 |
| TaLib.Functions.MedPrice | 1,000 | 1851.37 | 24.97 |
| TaLib.Functions.MedPrice | 10,000 | 10055.07 | 288.33 |
| TaLib.Functions.Min | 1,000 | 1919.00 | 98.20 |
| TaLib.Functions.Min | 10,000 | 10328.03 | 130.50 |
| TaLib.Functions.TRange | 1,000 | 1812.40 | 32.00 |
| TaLib.Functions.TRange | 10,000 | 10083.03 | 296.27 |
| TaLib.Functions.TypPrice | 1,000 | 1889.93 | 28.87 |
| TaLib.Functions.TypPrice | 10,000 | 10463.27 | 275.37 |
| TaLib.Functions.WclPrice | 1,000 | 1929.20 | 36.40 |
| TaLib.Functions.WclPrice | 10,000 | 10160.90 | 271.57 |
| Trady.Candlestick.Bearish | 1,000 | 2093.17 | 1385.53 |
| Trady.Candlestick.Bearish | 10,000 | 9358.73 | 3749.37 |
| Trady.Candlestick.BearishEngulfingPattern | 1,000 | 2050.30 | 2647.43 |
| Trady.Candlestick.BearishEngulfingPattern | 10,000 | 5416.37 | 7988.70 |
| Trady.Candlestick.BearishLongDay | 1,000 | 2833.80 | 615244.65 |
| Trady.Candlestick.BearishLongDay | 10,000 | 4285.97 | 81169348.07 |

## Complete regression statistics and slope angle

All 20 affected contract and inventory checks passed. Three parallel trajectories
verified 123,103 values, including all five statistics and their presence flags,
Skender's retrospective Line overlay, QuanTAlib's retained flat-window R-squared,
and the scalar slope angle. Independent rational and decimal batch references,
subnormals, overflowing intermediates, lazy maximum periods, lifecycle, global
versus local coordinates, and corruption of both comparison arms are covered.

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 640 arms across 160 pairs;
28 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Slope | 1,000 | 28439.47 | 1030.60 |
| QuanTAlib.Slope | 10,000 | 122154.47 | 1606.40 |
| Skender.GetSlope | 1,000 | 28015.17 | 869.23 |
| Skender.GetSlope | 10,000 | 130335.00 | 1689.10 |
| TaLib.Functions.LinearRegAngle | 1,000 | 5688.27 | 61.03 |
| TaLib.Functions.LinearRegAngle | 10,000 | 18175.87 | 585.57 |

## Remaining trend and extrema timing coverage

21 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 724 arms across 181 pairs;
7 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.Bullish | 1,000 | 2113.37 | 1444.20 |
| Trady.Candlestick.Bullish | 10,000 | 11163.60 | 3605.95 |
| Trady.Candlestick.BullishEngulfingPattern | 1,000 | 2024.13 | 2802.73 |
| Trady.Candlestick.BullishEngulfingPattern | 10,000 | 5561.87 | 7395.97 |
| Trady.Candlestick.Doji | 1,000 | 2378.60 | 1544.63 |
| Trady.Candlestick.Doji | 10,000 | 7536.00 | 5253.13 |
| Trady.Candlestick.DownTrend | 1,000 | 2036.53 | 2035.05 |
| Trady.Candlestick.DownTrend | 10,000 | 11126.10 | 5121.70 |
| Trady.Candlestick.DragonflyDoji | 1,000 | 2913.50 | 1850.57 |
| Trady.Candlestick.DragonflyDoji | 10,000 | 3318.83 | 5281.00 |
| Trady.Candlestick.DragonifyDoji | 1,000 | 3062.30 | 1954.43 |
| Trady.Candlestick.DragonifyDoji | 10,000 | 18946.83 | 5560.33 |
| Trady.Candlestick.GravestoneDoji | 1,000 | 3080.50 | 1910.30 |
| Trady.Candlestick.GravestoneDoji | 10,000 | 5084.83 | 6798.60 |
| Trady.Candlestick.UpTrend | 1,000 | 2066.42 | 1970.60 |
| Trady.Candlestick.UpTrend | 10,000 | 6685.73 | 5074.00 |
| Trady.Indicator.Highest | 1,000 | 1996.43 | 2931.00 |
| Trady.Indicator.Highest | 10,000 | 10541.37 | 8812.03 |
| Trady.Indicator.HighestClose | 1,000 | 1921.83 | 2815.00 |
| Trady.Indicator.HighestClose | 10,000 | 4522.82 | 8853.60 |
| Trady.Indicator.HighestHigh | 1,000 | 1953.57 | 2801.90 |
| Trady.Indicator.HighestHigh | 10,000 | 10446.13 | 8781.10 |
| Trady.Indicator.HistoricalHighest | 1,000 | 2255.10 | 2667.92 |
| Trady.Indicator.HistoricalHighest | 10,000 | 35429.03 | 8065.83 |
| Trady.Indicator.HistoricalHighestClose | 1,000 | 2207.70 | 2344.67 |
| Trady.Indicator.HistoricalHighestClose | 10,000 | 31475.20 | 8803.63 |
| Trady.Indicator.HistoricalHighestHigh | 1,000 | 2303.33 | 2463.47 |
| Trady.Indicator.HistoricalHighestHigh | 10,000 | 31960.45 | 7677.48 |
| Trady.Indicator.HistoricalLowest | 1,000 | 2292.37 | 2578.87 |
| Trady.Indicator.HistoricalLowest | 10,000 | 35808.60 | 8286.62 |
| Trady.Indicator.HistoricalLowestClose | 1,000 | 2275.43 | 2380.57 |
| Trady.Indicator.HistoricalLowestClose | 10,000 | 34240.17 | 9051.78 |
| Trady.Indicator.HistoricalLowestLow | 1,000 | 2416.20 | 2615.10 |
| Trady.Indicator.HistoricalLowestLow | 10,000 | 31135.80 | 7790.13 |
| Trady.Indicator.Lowest | 1,000 | 2008.20 | 2855.20 |
| Trady.Indicator.Lowest | 10,000 | 10437.43 | 10023.17 |
| Trady.Indicator.LowestClose | 1,000 | 1998.63 | 2840.60 |
| Trady.Indicator.LowestClose | 10,000 | 5364.20 | 9236.50 |
| Trady.Indicator.LowestLow | 1,000 | 1886.25 | 2856.63 |
| Trady.Indicator.LowestLow | 10,000 | 3184.50 | 8829.05 |
| Trady.Indicator.TrueRange | 1,000 | 1847.90 | 2182.33 |
| Trady.Indicator.TrueRange | 10,000 | 9741.20 | 6823.50 |

## Bearish short-day timing coverage

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 728 arms across 182 pairs;
6 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.BearishShortDay | 1,000 | 2804.60 | 636719.50 |
| Trady.Candlestick.BearishShortDay | 10,000 | 4316.37 | 80054841.27 |

## Sample and population dispersion

All 22 focused contract, affected regression, and inventory checks passed. Eight
parallel trajectories verified 185,469 values, including 74,357 values from the
six new dispersion families and both regression routes affected by presence-flag
gate fixes. Independent rational and centered decimal references cover exact
moments, sample/population denominators, available-history startup, scaling before
rounding, subnormal normalization, extreme finite results, and lifecycle. A staged
binary64 reference pins TA-Lib's large-offset cancellation independently. Native
period boundaries, Trady object/generic/tuple routes, and corruption checks passed.

6 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 752 arms across 188 pairs;
6 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Stddev | 1,000 | 22059.03 | 2514.57 |
| QuanTAlib.Stddev | 10,000 | 134670.03 | 6855.93 |
| QuanTAlib.Variance | 1,000 | 14294.17 | 2468.60 |
| QuanTAlib.Variance | 10,000 | 70025.70 | 7410.00 |
| QuanTAlib.Zscore | 1,000 | 20159.43 | 2497.93 |
| QuanTAlib.Zscore | 10,000 | 120774.37 | 7469.75 |
| TaLib.Functions.StdDev | 1,000 | 21910.90 | 47.53 |
| TaLib.Functions.StdDev | 10,000 | 136988.53 | 326.20 |
| TaLib.Functions.Var | 1,000 | 14487.07 | 32.43 |
| TaLib.Functions.Var | 10,000 | 74191.80 | 277.97 |
| Trady.Indicator.StandardDeviation | 1,000 | 21359.83 | 7668.40 |
| Trady.Indicator.StandardDeviation | 10,000 | 143892.00 | 17116.65 |

## Bullish long-day timing coverage

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 756 arms across 189 pairs;
5 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.BullishLongDay | 1,000 | 2754.20 | 616722.73 |
| Trady.Candlestick.BullishLongDay | 10,000 | 3972.70 | 81820794.13 |

## Complete deviation statistics and optional smoothing

The 25 focused checks passed across the initial and targeted correction runs:
24 initially passed, then the maximum-period assertion was corrected to pin
Skender's actual OverflowException and passed with both inventory checks. Seven
parallel trajectories verified 121,605 values, including 47,248 from the new pair
and all six shared dispersion routes. The four outputs and presence flags,
disabled/identity/longer smoothing, exact flatness, decimal quote collapse,
subnormal Z-scores, extreme finite outputs, lazy maximum periods, lifecycle, and
corruption of both arms are covered. An independent square-root reference
certifies platform estimates against exact midpoint inequalities, with dedicated
subnormal tie, maximum finite, and overflow tests.

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 760 arms across 190 pairs;
5 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetStdDev | 1,000 | 65015.43 | 991.33 |
| Skender.GetStdDev | 10,000 | 234521.13 | 2162.47 |

## Bullish short day timings

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 764 arms across 191 pairs;
4 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.BullishShortDay | 1,000 | 2787.50 | 641900.10 |
| Trady.Candlestick.BullishShortDay | 10,000 | 4218.90 | 80944414.40 |

## Mean absolute and detailed errors

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 772 arms across 193 pairs;
4 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetSmaAnalysis | 1,000 | 96611.60 | 667.80 |
| Skender.GetSmaAnalysis | 10,000 | 417103.33 | 1648.55 |
| TaLib.Functions.AvgDev | 1,000 | 15217.50 | 51.57 |
| TaLib.Functions.AvgDev | 10,000 | 61401.20 | 502.00 |

## Long day timings

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 776 arms across 194 pairs;
3 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.LongDay | 1,000 | 3162.37 | 1290591.60 |
| Trady.Candlestick.LongDay | 10,000 | 5536.07 | 163924827.67 |

## Price windows and Aroon conventions

9 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 812 arms across 203 pairs;
3 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetAroon | 1,000 | 2789.33 | 794.97 |
| Skender.GetAroon | 10,000 | 6471.67 | 2445.32 |
| Skender.GetDonchian | 1,000 | 4001.63 | 1298.17 |
| Skender.GetDonchian | 10,000 | 27305.73 | 5649.70 |
| Skender.GetWilliamsR | 1,000 | 2719.13 | 607.23 |
| Skender.GetWilliamsR | 10,000 | 9902.87 | 2190.67 |
| TaLib.Functions.Aroon | 1,000 | 2692.23 | 277.10 |
| TaLib.Functions.Aroon | 10,000 | 5775.23 | 402.57 |
| TaLib.Functions.AroonOsc | 1,000 | 2950.10 | 237.80 |
| TaLib.Functions.AroonOsc | 10,000 | 4421.20 | 371.77 |
| TaLib.Functions.WillR | 1,000 | 2880.13 | 143.27 |
| TaLib.Functions.WillR | 10,000 | 6365.83 | 197.87 |
| Trady.Indicator.Aroon | 1,000 | 2698.30 | 11042.93 |
| Trady.Indicator.Aroon | 10,000 | 8225.77 | 30333.23 |
| Trady.Indicator.AroonOscillator | 1,000 | 3114.93 | 11076.80 |
| Trady.Indicator.AroonOscillator | 10,000 | 5841.83 | 31812.90 |
| Trady.Indicator.HighestHighLowestLowDifference | 1,000 | 2313.53 | 5106.13 |
| Trady.Indicator.HighestHighLowestLowDifference | 10,000 | 13801.53 | 18349.90 |

## Long lower shadow timing completion

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 816 arms across 204 pairs;
2 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.LongLowerShadow | 1,000 | 3135.35 | 1372932.13 |
| Trady.Candlestick.LongLowerShadow | 10,000 | 5442.30 | 164956922.47 |

## Weighted price and volume recurrence comparisons

7 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 844 arms across 211 pairs;
2 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetCmf | 1,000 | 8243.67 | 1230.57 |
| Skender.GetCmf | 10,000 | 23092.05 | 2292.93 |
| Skender.GetForceIndex | 1,000 | 4023.73 | 615.93 |
| Skender.GetForceIndex | 10,000 | 9197.53 | 1713.98 |
| Skender.GetVwap | 1,000 | 4956.80 | 707.40 |
| Skender.GetVwap | 10,000 | 12219.13 | 1789.13 |
| Skender.GetVwma | 1,000 | 4331.27 | 529.23 |
| Skender.GetVwma | 10,000 | 10956.63 | 1180.73 |
| Trady.Indicator.NegativeVolumeIndex | 1,000 | 3183.70 | 2769.03 |
| Trady.Indicator.NegativeVolumeIndex | 10,000 | 15182.80 | 11514.10 |
| Trady.Indicator.PositiveVolumeIndex | 1,000 | 3298.97 | 2827.07 |
| Trady.Indicator.PositiveVolumeIndex | 10,000 | 19610.87 | 11087.63 |
| Trady.Indicator.VolumeWeightedAveragePrice | 1,000 | 4903.07 | 84939.40 |
| Trady.Indicator.VolumeWeightedAveragePrice | 10,000 | 8729.57 | 7892441.70 |

## Reverse Wilder recurrence comparison

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 848 arms across 212 pairs;
2 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Rma | 1,000 | 2780.93 | 208.57 |
| QuanTAlib.Rma | 10,000 | 21138.47 | 608.77 |

## Long upper shadow timing completion

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 852 arms across 213 pairs;
1 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.LongUpperShadow | 1,000 | 3358.03 | 1317165.73 |
| Trady.Candlestick.LongUpperShadow | 10,000 | 4505.30 | 164398387.63 |

## Elementary arithmetic and price rounding comparisons

7 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 880 arms across 220 pairs;
1 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Add | 1,000 | 2490.47 | 99.40 |
| TaLib.Functions.Add | 10,000 | 15205.50 | 200.30 |
| TaLib.Functions.Ceil | 1,000 | 2647.90 | 99.10 |
| TaLib.Functions.Ceil | 10,000 | 10100.17 | 203.53 |
| TaLib.Functions.Div | 1,000 | 2473.00 | 101.60 |
| TaLib.Functions.Div | 10,000 | 10470.93 | 212.33 |
| TaLib.Functions.Floor | 1,000 | 2766.23 | 97.73 |
| TaLib.Functions.Floor | 10,000 | 11425.80 | 242.80 |
| TaLib.Functions.Mult | 1,000 | 2437.42 | 100.90 |
| TaLib.Functions.Mult | 10,000 | 10386.73 | 212.53 |
| TaLib.Functions.Sqrt | 1,000 | 2857.63 | 100.27 |
| TaLib.Functions.Sqrt | 10,000 | 3221.10 | 286.67 |
| TaLib.Functions.Sub | 1,000 | 2635.07 | 99.90 |
| TaLib.Functions.Sub | 10,000 | 10662.23 | 202.07 |

## Quan WMA startup timing refresh

The four Quan WMA timing arms have been replaced with measurements of
`FixedPeriodWma`, which includes every fixed-period startup value. Earlier
WMA rows above describe the superseded runner that skipped startup. This
refresh adds no API families or timing-arm count. Both arms and sizes passed
the permanent verifier with three real measurements per case.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Wma | 1,000 | 2511.07 | 517.47 |
| QuanTAlib.Wma | 10,000 | 3550.20 | 1316.93 |

## Fixed-period double WMA and rolling mode comparisons

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 888 arms across 222 pairs;
1 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Dwma | 1,000 | 2948.43 | 1003.30 |
| QuanTAlib.Dwma | 10,000 | 4977.70 | 2668.83 |
| QuanTAlib.Mode | 1,000 | 3009.27 | 10443.92 |
| QuanTAlib.Mode | 10,000 | 7427.05 | 23286.00 |

## ShortDay timing completion

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 892 arms across 223 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Candlestick.ShortDay | 1,000 | 3173.30 | 1360628.63 |
| Trady.Candlestick.ShortDay | 10,000 | 5457.83 | 164535003.27 |

## Transcendental transform timings

6 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 916 arms across 229 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Cosh | 1,000 | 2774.37 | 303.20 |
| TaLib.Functions.Cosh | 10,000 | 3437.97 | 804.33 |
| TaLib.Functions.Exp | 1,000 | 2428.53 | 289.30 |
| TaLib.Functions.Exp | 10,000 | 3244.57 | 257.20 |
| TaLib.Functions.Ln | 1,000 | 2454.07 | 319.20 |
| TaLib.Functions.Ln | 10,000 | 2526.82 | 751.23 |
| TaLib.Functions.Log10 | 1,000 | 2573.43 | 328.37 |
| TaLib.Functions.Log10 | 10,000 | 3440.92 | 859.87 |
| TaLib.Functions.Sinh | 1,000 | 2642.17 | 345.57 |
| TaLib.Functions.Sinh | 10,000 | 3466.98 | 869.20 |
| TaLib.Functions.Tanh | 1,000 | 2720.03 | 318.13 |
| TaLib.Functions.Tanh | 10,000 | 3709.17 | 992.82 |

## Zero-seeded Laguerre and Elder-ray timings

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 924 arms across 231 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Ltma | 1,000 | 13781.53 | 215.10 |
| QuanTAlib.Ltma | 10,000 | 45842.17 | 735.23 |
| Skender.GetElderRay | 1,000 | 2874.80 | 1194.07 |
| Skender.GetElderRay | 10,000 | 4285.33 | 2059.27 |

## Expanding regularized and zero-lag EMA timings

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 932 arms across 233 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Rema | 1,000 | 5311.27 | 220.50 |
| QuanTAlib.Rema | 10,000 | 9634.53 | 850.13 |
| QuanTAlib.Zlema | 1,000 | 2493.53 | 260.77 |
| QuanTAlib.Zlema | 10,000 | 3989.07 | 910.48 |

## Curvature composition timings

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 936 arms across 234 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Curvature | 1,000 | 26733.73 | 1526.90 |
| QuanTAlib.Curvature | 10,000 | 149888.43 | 3373.67 |

## Holt-Winter forecast timings

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 940 arms across 235 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Hwma | 1,000 | 18098.47 | 221.80 |
| QuanTAlib.Hwma | 10,000 | 54525.27 | 702.43 |

## Normalized window entropy timings

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 944 arms across 236 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Entropy | 1,000 | 3383.37 | 10726.53 |
| QuanTAlib.Entropy | 10,000 | 5395.35 | 27553.13 |

## Gaussian and sine kernel timings

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 952 arms across 238 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Gma | 1,000 | 17318.97 | 471.93 |
| QuanTAlib.Gma | 10,000 | 39859.00 | 1351.63 |
| QuanTAlib.Sinema | 1,000 | 16931.00 | 497.47 |
| QuanTAlib.Sinema | 10,000 | 46687.67 | 1280.90 |

## Arnaud Legoux startup comparisons

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 960 arms across 240 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Alma | 1,000 | 25248.40 | 1311.40 |
| QuanTAlib.Alma | 10,000 | 51132.47 | 4629.60 |
| Skender.GetAlma | 1,000 | 22006.50 | 484.30 |
| Skender.GetAlma | 10,000 | 47066.20 | 1165.63 |

## Hull period and startup conventions

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 972 arms across 243 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Hma | 1,000 | 3592.23 | 1315.07 |
| QuanTAlib.Hma | 10,000 | 5757.67 | 2244.53 |
| Skender.GetHma | 1,000 | 3511.83 | 1512.70 |
| Skender.GetHma | 10,000 | 7986.63 | 2918.43 |
| Trady.Indicator.HullMovingAverage | 1,000 | 3319.97 | 12845.37 |
| Trady.Indicator.HullMovingAverage | 10,000 | 7744.40 | 42392.67 |

## Nine-mode envelope correctness and default timings

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 976 arms across 244 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetMaEnvelopes | 1,000 | 3571.90 | 1086.20 |
| Skender.GetMaEnvelopes | 10,000 | 6982.77 | 1737.37 |

## Circular and inverse circular transforms

6 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1000 arms across 250 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Acos | 1,000 | 2776.38 | 322.90 |
| TaLib.Functions.Acos | 10,000 | 3434.28 | 772.70 |
| TaLib.Functions.Asin | 1,000 | 2687.83 | 337.80 |
| TaLib.Functions.Asin | 10,000 | 3023.27 | 302.55 |
| TaLib.Functions.Atan | 1,000 | 2681.47 | 330.97 |
| TaLib.Functions.Atan | 10,000 | 3228.97 | 887.63 |
| TaLib.Functions.Cos | 1,000 | 2615.23 | 335.53 |
| TaLib.Functions.Cos | 10,000 | 3052.27 | 408.43 |
| TaLib.Functions.Sin | 1,000 | 2506.47 | 280.20 |
| TaLib.Functions.Sin | 10,000 | 3046.87 | 902.92 |
| TaLib.Functions.Tan | 1,000 | 2504.07 | 337.03 |
| TaLib.Functions.Tan | 10,000 | 2529.00 | 875.40 |

## Awesome oscillator and normalized percentage

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1004 arms across 251 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetAwesome | 1,000 | 5121.77 | 605.17 |
| Skender.GetAwesome | 10,000 | 10278.05 | 1773.73 |

## Alligator delays and Gator distances

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1012 arms across 253 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetAlligator | 1,000 | 3200.20 | 1001.27 |
| Skender.GetAlligator | 10,000 | 7284.87 | 1774.67 |
| Skender.GetGator | 1,000 | 3919.23 | 1627.73 |
| Skender.GetGator | 10,000 | 21324.90 | 2852.93 |

## Heikin-Ashi complete candles

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1016 arms across 254 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetHeikinAshi | 1,000 | 2330.70 | 1097.13 |
| Skender.GetHeikinAshi | 10,000 | 19546.27 | 3136.43 |

## Rolling Chande momentum and efficiency ratio

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1024 arms across 256 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetCmo | 1,000 | 5541.20 | 565.77 |
| Skender.GetCmo | 10,000 | 8817.67 | 1643.57 |
| Trady.Indicator.EfficiencyRatio | 1,000 | 5621.93 | 5398.90 |
| Trady.Indicator.EfficiencyRatio | 10,000 | 10428.63 | 12076.90 |

## Wilder strength with scaled gain and loss states

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1036 arms across 259 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetRsi | 1,000 | 8244.10 | 545.00 |
| Skender.GetRsi | 10,000 | 21199.70 | 1073.33 |
| TaLib.Functions.Cmo | 1,000 | 8651.80 | 130.90 |
| TaLib.Functions.Cmo | 10,000 | 25001.50 | 299.93 |
| TaLib.Functions.Rsi | 1,000 | 8302.60 | 130.47 |
| TaLib.Functions.Rsi | 10,000 | 22704.27 | 235.77 |

## Nullable strength and lagged momentum

5 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1056 arms across 264 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Indicator.NetMomentumOscillator | 1,000 | 8754.17 | 7178.95 |
| Trady.Indicator.NetMomentumOscillator | 10,000 | 32417.23 | 36128.70 |
| Trady.Indicator.RelativeMomentum | 1,000 | 8331.33 | 7754.93 |
| Trady.Indicator.RelativeMomentum | 10,000 | 31307.80 | 37013.97 |
| Trady.Indicator.RelativeMomentumIndex | 1,000 | 8551.97 | 8462.83 |
| Trady.Indicator.RelativeMomentumIndex | 10,000 | 30227.07 | 41568.90 |
| Trady.Indicator.RelativeStrength | 1,000 | 8626.20 | 6932.20 |
| Trady.Indicator.RelativeStrength | 10,000 | 27359.70 | 27992.45 |
| Trady.Indicator.RelativeStrengthIndex | 1,000 | 8463.67 | 7664.70 |
| Trady.Indicator.RelativeStrengthIndex | 10,000 | 30373.93 | 38543.32 |

## Simple and exponential average differences

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1064 arms across 266 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Indicator.ExponentialMovingAverageOscillator | 1,000 | 2838.97 | 7161.13 |
| Trady.Indicator.ExponentialMovingAverageOscillator | 10,000 | 19453.70 | 40218.15 |
| Trady.Indicator.SimpleMovingAverageOscillator | 1,000 | 2854.30 | 5191.53 |
| Trady.Indicator.SimpleMovingAverageOscillator | 10,000 | 4783.50 | 19680.32 |

## Retrospective DPO and confirmed fractals

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1076 arms across 269 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetDpo | 1,000 | 536.35 | 1015.73 |
| Skender.GetDpo | 10,000 | 3570.80 | 2301.80 |
| Skender.GetFcb | 1,000 | 634.00 | 1281.97 |
| Skender.GetFcb | 10,000 | 1382.90 | 8050.55 |
| Skender.GetFractal | 1,000 | 560.90 | 1181.13 |
| Skender.GetFractal | 10,000 | 1201.47 | 6409.87 |

## Fifty-seeded stochastic KDJ and differences

7 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1104 arms across 276 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Indicator.RawStochasticsValue | 1,000 | 4003.83 | 5284.87 |
| Trady.Indicator.RawStochasticsValue | 10,000 | 11294.00 | 18648.17 |
| Trady.Indicator.Stochastics+Fast | 1,000 | 4935.10 | 7030.10 |
| Trady.Indicator.Stochastics+Fast | 10,000 | 23280.07 | 22122.37 |
| Trady.Indicator.Stochastics+Full | 1,000 | 4896.17 | 7636.87 |
| Trady.Indicator.Stochastics+Full | 10,000 | 23111.22 | 27785.47 |
| Trady.Indicator.Stochastics+Slow | 1,000 | 4742.60 | 9703.90 |
| Trady.Indicator.Stochastics+Slow | 10,000 | 14685.50 | 41860.90 |
| Trady.Indicator.StochasticsOscillator+Fast | 1,000 | 4546.73 | 7871.90 |
| Trady.Indicator.StochasticsOscillator+Fast | 10,000 | 11646.33 | 27749.70 |
| Trady.Indicator.StochasticsOscillator+Full | 1,000 | 4469.07 | 9902.27 |
| Trady.Indicator.StochasticsOscillator+Full | 10,000 | 11516.60 | 44828.90 |
| Trady.Indicator.StochasticsOscillator+Slow | 1,000 | 4434.70 | 8760.60 |
| Trady.Indicator.StochasticsOscillator+Slow | 10,000 | 12671.55 | 41359.67 |

## Window stochastic momentum and double-smoothed index

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1116 arms across 279 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetSmi | 1,000 | 4847.43 | 693.50 |
| Skender.GetSmi | 10,000 | 14916.87 | 2153.67 |
| Trady.Indicator.StochasticsMomentum | 1,000 | 3167.47 | 5630.23 |
| Trady.Indicator.StochasticsMomentum | 10,000 | 14111.67 | 19144.18 |
| Trady.Indicator.StochasticsMomentumIndex | 1,000 | 5209.37 | 13737.10 |
| Trady.Indicator.StochasticsMomentumIndex | 10,000 | 14395.37 | 57318.53 |

## Nullable and smoothed stochastic RSI

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1124 arms across 281 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetStochRsi | 1,000 | 10890.73 | 975.13 |
| Skender.GetStochRsi | 10,000 | 34361.73 | 3131.82 |
| Trady.Indicator.StochasticsRsiOscillator | 1,000 | 11045.33 | 18268.97 |
| Trady.Indicator.StochasticsRsiOscillator | 10,000 | 32922.03 | 61397.10 |

## Rolling and decaying close extrema

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1132 arms across 283 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Max | 1,000 | 2125.07 | 817.70 |
| QuanTAlib.Max | 10,000 | 2378.77 | 1917.70 |
| QuanTAlib.Min | 1,000 | 2151.63 | 829.10 |
| QuanTAlib.Min | 10,000 | 2781.73 | 1724.48 |

## Compensated double and triple exponential averages

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1140 arms across 285 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Dema | 1,000 | 9680.70 | 198.90 |
| QuanTAlib.Dema | 10,000 | 26897.37 | 690.60 |
| QuanTAlib.Tema | 1,000 | 12972.17 | 185.70 |
| QuanTAlib.Tema | 10,000 | 31104.63 | 696.10 |

## Mass-normalized quadruple EMA

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1144 arms across 286 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Qema | 1,000 | 81941.80 | 671.00 |
| QuanTAlib.Qema | 10,000 | 429951.50 | 2117.37 |

## Adjusted skewness and excess kurtosis

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1152 arms across 288 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Kurtosis | 1,000 | 49518.47 | 798.80 |
| QuanTAlib.Kurtosis | 10,000 | 420089.18 | 1599.07 |
| QuanTAlib.Skew | 1,000 | 64786.87 | 1610.30 |
| QuanTAlib.Skew | 10,000 | 383096.27 | 9300.37 |

## Correlation and paired population statistics

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1160 arms across 290 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetCorrelation | 1,000 | 73567.30 | 1545.07 |
| Skender.GetCorrelation | 10,000 | 350476.80 | 4495.17 |
| TaLib.Functions.Correl | 1,000 | 37711.73 | 139.83 |
| TaLib.Functions.Correl | 10,000 | 260893.23 | 257.85 |

## Return beta and directional beta statistics

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1168 arms across 292 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetBeta | 1,000 | 88305.83 | 3927.02 |
| Skender.GetBeta | 10,000 | 305384.60 | 11532.93 |
| TaLib.Functions.Beta | 1,000 | 31458.13 | 172.17 |
| TaLib.Functions.Beta | 10,000 | 130284.43 | 231.77 |

## Commodity channel index conventions

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1180 arms across 295 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetCci | 1,000 | 17368.90 | 419.17 |
| Skender.GetCci | 10,000 | 49659.93 | 1220.78 |
| TaLib.Functions.Cci | 1,000 | 17734.53 | 412.03 |
| TaLib.Functions.Cci | 10,000 | 54271.80 | 489.27 |
| Trady.Indicator.CommodityChannelIndex | 1,000 | 10245.97 | 63611.60 |
| Trady.Indicator.CommodityChannelIndex | 10,000 | 23459.30 | 138804.37 |

## Window-relative Ulcer Index

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1184 arms across 296 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetUlcerIndex | 1,000 | 80609.87 | 894.17 |
| Skender.GetUlcerIndex | 10,000 | 321705.47 | 5438.07 |

## True-interval Choppiness Index

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1188 arms across 297 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetChop | 1,000 | 10057.07 | 564.27 |
| Skender.GetChop | 10,000 | 34677.43 | 1465.57 |

## Directional Money Flow Index conventions

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1196 arms across 299 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetMfi | 1,000 | 12915.80 | 492.57 |
| Skender.GetMfi | 10,000 | 44869.63 | 1625.03 |
| TaLib.Functions.Mfi | 1,000 | 13495.67 | 154.60 |
| TaLib.Functions.Mfi | 10,000 | 40692.67 | 289.07 |

## Chandelier Exit selections and conventions

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1204 arms across 301 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetChandelier | 1,000 | 7636.10 | 699.23 |
| Skender.GetChandelier | 10,000 | 14954.80 | 1651.20 |
| Trady.Indicator.ChandelierExit | 1,000 | 8649.03 | 8028.23 |
| Trady.Indicator.ChandelierExit | 10,000 | 23465.93 | 31990.93 |

## Keltner and STARC envelope conventions

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1216 arms across 304 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetKeltner | 1,000 | 7634.83 | 1423.93 |
| Skender.GetKeltner | 10,000 | 18240.20 | 2483.70 |
| Skender.GetStarcBands | 1,000 | 6372.50 | 1150.53 |
| Skender.GetStarcBands | 10,000 | 15042.23 | 2526.43 |
| Trady.Indicator.KeltnerChannels | 1,000 | 6530.88 | 6123.80 |
| Trady.Indicator.KeltnerChannels | 10,000 | 22050.20 | 39193.63 |

## Vortex and Ultimate true-range ratios

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1228 arms across 307 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetUltimate | 1,000 | 22601.33 | 549.57 |
| Skender.GetUltimate | 10,000 | 153179.60 | 2295.47 |
| Skender.GetVortex | 1,000 | 16655.43 | 577.23 |
| Skender.GetVortex | 10,000 | 36899.63 | 1264.57 |
| TaLib.Functions.UltOsc | 1,000 | 28361.93 | 275.43 |
| TaLib.Functions.UltOsc | 10,000 | 158234.00 | 427.83 |

## Trady directional movement and early ADX

7 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1256 arms across 314 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Indicator.AverageDirectionalIndex | 1,000 | 27778.33 | 16249.30 |
| Trady.Indicator.AverageDirectionalIndex | 10,000 | 70386.52 | 69464.77 |
| Trady.Indicator.AverageDirectionalIndexRating | 1,000 | 28183.93 | 19060.23 |
| Trady.Indicator.AverageDirectionalIndexRating | 10,000 | 72599.73 | 65283.37 |
| Trady.Indicator.DirectionalMovementIndex | 1,000 | 27411.63 | 13955.37 |
| Trady.Indicator.DirectionalMovementIndex | 10,000 | 69123.67 | 56581.33 |
| Trady.Indicator.MinusDirectionalIndicator | 1,000 | 24049.37 | 7996.33 |
| Trady.Indicator.MinusDirectionalIndicator | 10,000 | 60606.67 | 31565.23 |
| Trady.Indicator.MinusDirectionalMovement | 1,000 | 5977.80 | 2499.90 |
| Trady.Indicator.MinusDirectionalMovement | 10,000 | 17131.30 | 7688.72 |
| Trady.Indicator.PlusDirectionalIndicator | 1,000 | 24186.30 | 8148.70 |
| Trady.Indicator.PlusDirectionalIndicator | 10,000 | 62214.13 | 31013.12 |
| Trady.Indicator.PlusDirectionalMovement | 1,000 | 5902.13 | 2328.07 |
| Trady.Indicator.PlusDirectionalMovement | 10,000 | 17046.03 | 5085.70 |

## Skender sum-seeded directional index

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1260 arms across 315 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetAdx | 1,000 | 36894.50 | 932.57 |
| Skender.GetAdx | 10,000 | 102662.90 | 1426.90 |

## TA directional seed suppression and rating

7 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1288 arms across 322 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Adx | 1,000 | 32269.77 | 188.60 |
| TaLib.Functions.Adx | 10,000 | 94387.27 | 371.97 |
| TaLib.Functions.Adxr | 1,000 | 33446.57 | 225.70 |
| TaLib.Functions.Adxr | 10,000 | 96892.83 | 510.73 |
| TaLib.Functions.Dx | 1,000 | 33466.90 | 233.37 |
| TaLib.Functions.Dx | 10,000 | 108110.40 | 389.38 |
| TaLib.Functions.MinusDI | 1,000 | 21311.00 | 195.07 |
| TaLib.Functions.MinusDI | 10,000 | 59147.97 | 268.17 |
| TaLib.Functions.MinusDM | 1,000 | 17333.73 | 136.47 |
| TaLib.Functions.MinusDM | 10,000 | 48306.80 | 229.60 |
| TaLib.Functions.PlusDI | 1,000 | 20683.43 | 183.07 |
| TaLib.Functions.PlusDI | 10,000 | 57016.47 | 321.53 |
| TaLib.Functions.PlusDM | 1,000 | 17698.13 | 134.83 |
| TaLib.Functions.PlusDM | 10,000 | 45824.13 | 289.17 |

## Tillson T3 seed conventions and exact polynomial

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1300 arms across 325 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.T3 | 1,000 | 22651.33 | 300.77 |
| QuanTAlib.T3 | 10,000 | 70400.73 | 932.23 |
| Skender.GetT3 | 1,000 | 22319.70 | 891.67 |
| Skender.GetT3 | 10,000 | 70069.50 | 1249.63 |
| TaLib.Functions.T3 | 1,000 | 21270.17 | 110.23 |
| TaLib.Functions.T3 | 10,000 | 68062.73 | 212.33 |

## TRIX seed signal and EMA settings

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1308 arms across 327 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetTrix | 1,000 | 14884.57 | 969.83 |
| Skender.GetTrix | 10,000 | 37649.23 | 1270.53 |
| TaLib.Functions.Trix | 1,000 | 14984.93 | 140.47 |
| TaLib.Functions.Trix | 10,000 | 36334.87 | 324.27 |

## Mean-seeded TSI and PMO timing evidence

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1316 arms across 329 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetPmo | 1,000 | 17569.90 | 978.33 |
| Skender.GetPmo | 10,000 | 45690.80 | 2053.13 |
| Skender.GetTsi | 1,000 | 20405.50 | 734.27 |
| Skender.GetTsi | 10,000 | 56276.22 | 888.33 |

## Price relative strength timing evidence

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1320 arms across 330 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetPrs | 1,000 | 21142.77 | 1226.03 |
| Skender.GetPrs | 10,000 | 83609.82 | 3173.10 |

## McGinley dynamic timing evidence

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1328 arms across 332 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Mgdi | 1,000 | 42208.83 | 300.73 |
| QuanTAlib.Mgdi | 10,000 | 256794.87 | 1176.00 |
| Skender.GetDynamic | 1,000 | 42571.33 | 576.17 |
| Skender.GetDynamic | 10,000 | 260319.67 | 1119.07 |

## Historical and realized volatility timing evidence

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1336 arms across 334 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Historical | 1,000 | 58673.40 | 2392.60 |
| QuanTAlib.Historical | 10,000 | 356973.43 | 6208.87 |
| QuanTAlib.Realized | 1,000 | 131547.93 | 709.77 |
| QuanTAlib.Realized | 10,000 | 358697.47 | 1489.20 |

## MACD and volume oscillator timing evidence

4 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1352 arms across 338 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetMacd | 1,000 | 19277.23 | 1175.50 |
| Skender.GetMacd | 10,000 | 46884.63 | 1262.40 |
| Skender.GetPvo | 1,000 | 20471.93 | 1040.30 |
| Skender.GetPvo | 10,000 | 61521.13 | 1245.73 |
| Trady.Indicator.MovingAverageConvergenceDivergence | 1,000 | 17371.77 | 7135.83 |
| Trady.Indicator.MovingAverageConvergenceDivergence | 10,000 | 55350.77 | 25926.70 |
| Trady.Indicator.MovingAverageConvergenceDivergenceHistogram | 1,000 | 15389.93 | 9333.90 |
| Trady.Indicator.MovingAverageConvergenceDivergenceHistogram | 10,000 | 44141.13 | 35812.73 |

## Aligned TA MACD timing evidence

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1360 arms across 340 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Macd | 1,000 | 16812.00 | 248.23 |
| TaLib.Functions.Macd | 10,000 | 41593.13 | 411.78 |
| TaLib.Functions.MacdFix | 1,000 | 28204.73 | 251.63 |
| TaLib.Functions.MacdFix | 10,000 | 105398.17 | 399.50 |

## Smoothed accumulation oscillator timings

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1368 arms across 342 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetChaikinOsc | 1,000 | 28180.87 | 1062.57 |
| Skender.GetChaikinOsc | 10,000 | 84411.30 | 2283.67 |
| TaLib.Functions.AdOsc | 1,000 | 24276.97 | 126.77 |
| TaLib.Functions.AdOsc | 10,000 | 79934.17 | 189.10 |

## Range acceleration band timings

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1372 arms across 343 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Accbands | 1,000 | 21079.20 | 646.00 |
| TaLib.Functions.Accbands | 10,000 | 75406.57 | 422.70 |

## Ichimoku snapshot timings

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1376 arms across 344 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetIchimoku | 1,000 | 1706.00 | 8238.63 |
| Skender.GetIchimoku | 10,000 | 6024.37 | 14665.13 |

## Extended Ichimoku cloud timings

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1380 arms across 345 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Indicator.IchimokuCloud | 1,000 | 1904.37 | 14326.90 |
| Trady.Indicator.IchimokuCloud | 10,000 | 6976.53 | 51047.63 |

## Seeded adaptive average timings

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1388 arms across 347 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Kama | 1,000 | 30566.60 | 1053.07 |
| QuanTAlib.Kama | 10,000 | 121064.02 | 2315.53 |
| Skender.GetKama | 1,000 | 29906.90 | 567.70 |
| Skender.GetKama | 10,000 | 124467.73 | 1339.97 |

## Trady adaptive average timings

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1392 arms across 348 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Indicator.KaufmanAdaptiveMovingAverage | 1,000 | 31868.83 | 31313.70 |
| Trady.Indicator.KaufmanAdaptiveMovingAverage | 10,000 | 123357.02 | 259876.77 |

## TA adaptive average timings

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1396 arms across 349 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Kama | 1,000 | 30412.53 | 166.00 |
| TaLib.Functions.Kama | 10,000 | 123549.27 | 237.07 |

## Seeded phase adaptive average timings

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1404 arms across 351 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Mama | 1,000 | 124906.77 | 1858.40 |
| QuanTAlib.Mama | 10,000 | 709278.23 | 4009.67 |
| Skender.GetMama | 1,000 | 128206.10 | 878.07 |
| Skender.GetMama | 10,000 | 745158.43 | 1797.67 |

## Delayed zero-seeded MAMA and FAMA

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1408 arms across 352 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Mama | 1,000 | 128480.07 | 603.15 |
| TaLib.Functions.Mama | 10,000 | 708003.83 | 1294.37 |

## Deviation-ratio adaptive average

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1412 arms across 353 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Vidya | 1,000 | 33977.83 | 8815.18 |
| QuanTAlib.Vidya | 10,000 | 184358.87 | 31807.33 |

## Complete-window deviation bands and width

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1424 arms across 356 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetBollingerBands | 1,000 | 71051.73 | 1168.33 |
| Skender.GetBollingerBands | 10,000 | 312451.25 | 1989.43 |
| Trady.Indicator.BollingerBands | 1,000 | 69799.00 | 9918.15 |
| Trady.Indicator.BollingerBands | 10,000 | 307453.33 | 25064.57 |
| Trady.Indicator.BollingerBandWidth | 1,000 | 69833.32 | 12770.43 |
| Trady.Indicator.BollingerBandWidth | 10,000 | 310621.90 | 28610.58 |

## Generic classical moving average routing

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1428 arms across 357 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Ma | 1,000 | 2689.87 | 95.27 |
| TaLib.Functions.Ma | 10,000 | 3985.97 | 146.23 |

## Classical absolute and percentage price oscillators

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1436 arms across 359 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Apo | 1,000 | 5423.30 | 376.83 |
| TaLib.Functions.Apo | 10,000 | 12291.13 | 270.13 |
| TaLib.Functions.Ppo | 1,000 | 8412.63 | 565.97 |
| TaLib.Functions.Ppo | 10,000 | 19630.02 | 300.07 |

## Classical deviation bands

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1440 arms across 360 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Bbands | 1,000 | 37453.70 | 240.23 |
| TaLib.Functions.Bbands | 10,000 | 181932.22 | 381.43 |

## Variable-period classical averages

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1444 arms across 361 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Mavp | 1,000 | 4293.97 | 1071.20 |
| TaLib.Functions.Mavp | 10,000 | 10631.47 | 739.60 |

## Classical stochastic RSI

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1448 arms across 362 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.StochRsi | 1,000 | 10685.77 | 316.23 |
| TaLib.Functions.StochRsi | 10,000 | 26202.70 | 631.10 |

## Window stochastic KDJ

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1452 arms across 363 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetStoch | 1,000 | 11687.90 | 825.40 |
| Skender.GetStoch | 10,000 | 45639.73 | 2002.83 |

## Classical MACD and wide averaging

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1456 arms across 364 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.MacdExt | 1,000 | 17555.50 | 268.23 |
| TaLib.Functions.MacdExt | 10,000 | 47364.43 | 360.40 |

## Refreshed shared classical averages

6 refreshed pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1456 arms across 364 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Apo | 1,000 | 11249.77 | 362.60 |
| TaLib.Functions.Apo | 10,000 | 27087.60 | 221.43 |
| TaLib.Functions.Bbands | 1,000 | 38472.03 | 259.43 |
| TaLib.Functions.Bbands | 10,000 | 195907.20 | 345.47 |
| TaLib.Functions.Ma | 1,000 | 4603.43 | 90.77 |
| TaLib.Functions.Ma | 10,000 | 10868.67 | 170.90 |
| TaLib.Functions.Mavp | 1,000 | 21459.40 | 905.53 |
| TaLib.Functions.Mavp | 10,000 | 63647.87 | 1226.67 |
| TaLib.Functions.Ppo | 1,000 | 14226.67 | 635.60 |
| TaLib.Functions.Ppo | 10,000 | 33894.53 | 289.17 |
| TaLib.Functions.StochRsi | 1,000 | 12504.47 | 323.17 |
| TaLib.Functions.StochRsi | 10,000 | 32205.33 | 634.83 |

## Classical fast and slow stochastic

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1464 arms across 366 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.Stoch | 1,000 | 9119.00 | 678.60 |
| TaLib.Functions.Stoch | 10,000 | 23816.45 | 494.17 |
| TaLib.Functions.StochF | 1,000 | 7329.30 | 672.27 |
| TaLib.Functions.StochF | 10,000 | 18973.63 | 450.07 |

## Hilbert dominant period and phasor

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1472 arms across 368 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.HtDcPeriod | 1,000 | 120462.33 | 452.87 |
| TaLib.Functions.HtDcPeriod | 10,000 | 640554.10 | 1136.20 |
| TaLib.Functions.HtPhasor | 1,000 | 159915.80 | 524.03 |
| TaLib.Functions.HtPhasor | 10,000 | 633299.53 | 1073.90 |

## Refreshed shared Hilbert adaptive averages

3 refreshed pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1472 arms across 368 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (Âµs) | Competitor mean (Âµs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Mama | 1,000 | 154826.80 | 1985.67 |
| QuanTAlib.Mama | 10,000 | 710920.93 | 3978.90 |
| Skender.GetMama | 1,000 | 136809.03 | 866.67 |
| Skender.GetMama | 10,000 | 751896.77 | 2145.10 |
| TaLib.Functions.Mama | 1,000 | 127635.20 | 610.70 |
| TaLib.Functions.Mama | 10,000 | 736597.17 | 1332.90 |

## Delayed Hilbert trendline

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1476 arms across 369 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.HtTrendline | 1,000 | 146765.10 | 649.47 |
| TaLib.Functions.HtTrendline | 10,000 | 663789.83 | 1452.30 |

## Delayed Hilbert cycle signals

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1488 arms across 372 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.HtDcPhase | 1,000 | 126881.03 | 2332.00 |
| TaLib.Functions.HtDcPhase | 10,000 | 1314149.95 | 5666.67 |
| TaLib.Functions.HtSine | 1,000 | 127409.87 | 1234.53 |
| TaLib.Functions.HtSine | 10,000 | 1315842.87 | 6192.60 |
| TaLib.Functions.HtTrendMode | 1,000 | 134181.07 | 1524.63 |
| TaLib.Functions.HtTrendMode | 10,000 | 1368953.98 | 6634.25 |

## Refreshed shared Hilbert trend mean

1 refreshed pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1488 arms across 372 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (Âµs) | Competitor mean (Âµs) |
| --- | ---: | ---: | ---: |
| TaLib.Functions.HtTrendline | 1,000 | 129829.43 | 675.33 |
| TaLib.Functions.HtTrendline | 10,000 | 684911.28 | 1378.90 |

## Early-start Hilbert trendline

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1492 arms across 373 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetHtTrendline | 1,000 | 94503.42 | 791.00 |
| Skender.GetHtTrendline | 10,000 | 694756.70 | 2179.97 |

## Range and filtered-deviation adaptive averages

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1500 arms across 375 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Dsma | 1,000 | 34782.70 | 513.23 |
| QuanTAlib.Dsma | 10,000 | 165971.17 | 1487.83 |
| QuanTAlib.Frama | 1,000 | 19870.40 | 873.63 |
| QuanTAlib.Frama | 10,000 | 95787.62 | 2732.12 |

## Seeded ATR trailing stop and SuperTrend

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1508 arms across 377 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetAtrStop | 1,000 | 18775.80 | 1067.43 |
| Skender.GetAtrStop | 10,000 | 56362.53 | 2690.20 |
| Skender.GetSuperTrend | 1,000 | 19010.88 | 1583.83 |
| Skender.GetSuperTrend | 10,000 | 90316.97 | 1994.00 |

## Retrospective volatility stop and window Fisher

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1516 arms across 379 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetFisherTransform | 1,000 | 11331.80 | 702.50 |
| Skender.GetFisherTransform | 10,000 | 35347.22 | 2135.27 |
| Skender.GetVolatilityStop | 1,000 | 12447.10 | 1434.47 |
| Skender.GetVolatilityStop | 10,000 | 61522.30 | 2862.83 |

## Retrospective regression channels

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1520 arms across 380 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetStdDevChannels | 1,000 | 29527.00 | 1100.82 |
| Skender.GetStdDevChannels | 10,000 | 135890.53 | 2530.03 |

## Rolling and calendar pivot levels

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1528 arms across 382 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetPivotPoints | 1,000 | 1105.92 | 1561.02 |
| Skender.GetPivotPoints | 10,000 | 2820.43 | 3800.73 |
| Skender.GetRollingPivots | 1,000 | 12765.30 | 2214.00 |
| Skender.GetRollingPivots | 10,000 | 48253.97 | 13104.47 |

## Single-stochastic Schaff trend cycle

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1532 arms across 383 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetStc | 1,000 | 12125.67 | 1196.83 |
| Skender.GetStc | 10,000 | 41520.42 | 3057.83 |

## Connors strength and exact return ranks

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1536 arms across 384 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetConnorsRsi | 1,000 | 268540.17 | 1125.52 |
| Skender.GetConnorsRsi | 10,000 | 2883144.65 | 6317.23 |

## Oldest-first Hilbert trendline convention

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1540 arms across 385 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Htit | 1,000 | 3102.47 | 1184.60 |
| QuanTAlib.Htit | 10,000 | 6351.20 | 3182.20 |

## Relative volatility with independent deviation oracles

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1544 arms across 386 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Rvi | 1,000 | 24547.13 | 6125.93 |
| QuanTAlib.Rvi | 10,000 | 140059.37 | 16624.60 |

## Median-adaptive signed-threshold convention

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1548 arms across 387 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Maaf | 1,000 | 16341.68 | 18136.53 |
| QuanTAlib.Maaf | 10,000 | 86429.45 | 33385.22 |

## Exact Fibonacci and windowed-sinc snapshots

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1556 arms across 389 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Afirma | 1,000 | 7949.00 | 1781.98 |
| QuanTAlib.Afirma | 10,000 | 42749.77 | 3506.88 |
| QuanTAlib.Fwma | 1,000 | 28292.18 | 787.12 |
| QuanTAlib.Fwma | 10,000 | 83192.25 | 2179.02 |

## Klinger volume and retrospective pivot trends

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1564 arms across 391 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetKvo | 1,000 | 26382.43 | 1072.95 |
| Skender.GetKvo | 10,000 | 71290.18 | 1584.98 |
| Skender.GetPivots | 1,000 | 1212.68 | 1435.37 |
| Skender.GetPivots | 10,000 | 8880.67 | 4027.27 |

## Four parabolic stop conventions

4 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1580 arms across 395 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetParabolicSar | 1,000 | 11819.65 | 732.73 |
| Skender.GetParabolicSar | 10,000 | 65425.60 | 1713.72 |
| TaLib.Functions.Sar | 1,000 | 12246.65 | 133.20 |
| TaLib.Functions.Sar | 10,000 | 67133.77 | 884.23 |
| TaLib.Functions.SarExt | 1,000 | 12142.53 | 149.20 |
| TaLib.Functions.SarExt | 10,000 | 57973.30 | 332.80 |
| Trady.Indicator.ParabolicStopAndReverse | 1,000 | 10700.55 | 2943.93 |
| Trady.Indicator.ParabolicStopAndReverse | 10,000 | 57750.22 | 9373.53 |

## Hurst ZigZag and Jurik snapshots

3 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1592 arms across 398 pairs;
3 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| QuanTAlib.Jma | 1,000 | 80350.83 | 974.92 |
| QuanTAlib.Jma | 10,000 | 372342.60 | 2904.80 |
| Skender.GetHurst | 1,000 | 1394.80 | 3366.98 |
| Skender.GetHurst | 10,000 | 3801.18 | 8288.50 |
| Skender.GetZigZag | 1,000 | 15605.90 | 1816.33 |
| Skender.GetZigZag | 10,000 | 69778.60 | 3554.42 |

## Fixed and final-ATR Renko charts

2 additional pairs passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1600 arms across 400 pairs;
1 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Skender.GetRenko | 1,000 | 2160.40 | 787.43 |
| Skender.GetRenko | 10,000 | 16082.70 | 2004.53 |
| Skender.GetRenkoAtr | 1,000 | 5100.83 | 1244.10 |
| Skender.GetRenkoAtr | 10,000 | 19552.00 | 2577.17 |

## Volatility-selected Dynamic Momentum Index

1 additional pair passed setup correctness and recorded both arms at
1,000 and 10,000 bars. Each arm has three measured iterations. The archived
subset now passes the permanent verifier with 1604 arms across 401 pairs;
0 currently paired families remain unmeasured. Short local timings retain
observed disadvantages and do not establish universal speedup claims.

| Pair | Bars | Ooples mean (µs) | Competitor mean (µs) |
| --- | ---: | ---: | ---: |
| Trady.Indicator.DynamicMomentumIndex | 1,000 | 79009.40 | 1280867.60 |
| Trady.Indicator.DynamicMomentumIndex | 10,000 | 718317.87 | 159169728.33 |
