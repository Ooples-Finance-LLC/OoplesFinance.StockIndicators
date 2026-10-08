# Eight CPU pilots: direct native comparisons

These results replace the earlier adapter-inclusive “all eight batch and streaming wins” claim. Timed competitor arms now call native APIs directly. Input preparation, correctness normalization and references are outside timing for both sides. See [raw statistics and contracts](measurements.json), [benchmark methods](../../OoplesFinance.StockIndicators.CompetitorBenchmarks/LibraryComparisons/CpuKernelBenchmarks.cs), [native calls](../../OoplesFinance.StockIndicators.CompetitorBenchmarks/LibraryComparisons/CpuNativeWorkload.cs), and the [reviewed blueprint](../../../docs/cpu-kernel-blueprint.md).

All rows below are milliseconds per 10,000 observations. Ratio = competitor / Ooples; above 1 favors Ooples. The JSON contains both sizes, variability, confidence intervals, sample counts, raw allocations and report hashes. Close differences are not proof of a stable advantage.

## Owning batch

Both sides create fresh state where required and return owned output. Ooples composes its new kernel APIs, not the legacy builder route. Native eager collections are returned directly without extra copies. Fractal results are placed at their center bars on both sides.

| Pair | Ooples | Native competitor | Ratio |
|---|---:|---:|---:|
| QuanTAlib.Jma | 1.693 | 2.076 | 1.23x |
| QuanTAlib.Atr | 0.188 | 0.655 | 3.49x |
| Skender.GetRollingPivots | 4.787 | 7.297 | 1.52x |
| Skender.GetFractal | 0.481 | 6.204 | 12.89x |
| TaLib.Candles.RickshawMan | 0.802 | 1.097 | 1.37x |
| TaLib.Functions.Asin | 0.146 | 0.171 | 1.17x |
| Trady.Candlestick.BullishShortDay | 0.922 | 112,621.225 | 122164.29x |
| Trady.Indicator.SimpleMovingAverage | 0.540 | 10.023 | 18.57x |

## Reusable batch

Caller-owned buffers on both sides. Asin uses the approved close-only IEEE span API; Rickshaw uses prepared OHLC inputs. Other pairs have no verified equivalent reusable fresh-batch arm and are not assigned wins.

| Pair | Ooples | Native competitor | Ratio |
|---|---:|---:|---:|
| TaLib.Candles.RickshawMan | 0.737 | 1.071 | 1.45x |
| TaLib.Functions.Asin | 0.121 | 0.165 | 1.36x |

## Streaming

Fresh state and output storage outside timing for both sides. Each operation is one full sequence of incremental updates. A fixed-invocation job prevents repeated sequences from accidentally continuing state; its operation count is checked in the report verifier. Batch-only competitors are not substituted.

| Pair | Ooples | Native competitor | Ratio |
|---|---:|---:|---:|
| QuanTAlib.Jma | 1.894 | 2.103 | 1.11x |
| QuanTAlib.Atr | 0.183 | 0.624 | 3.41x |

The closest streaming comparisons are at **1,000 observations** (milliseconds):

| Pair | Ooples | Native competitor | Ratio |
|---|---:|---:|---:|
| QuanTAlib.Jma | 0.189 | 0.196 | 1.04x |
| QuanTAlib.Atr | 0.051 | 0.053 | 1.03x |

These small streaming differences and the overlapping Asin confidence intervals do not establish a decisive advantage. Both sizes and all supported workloads are retained; no loss or near-tie is excluded to produce an eight-win headline.

## Interpretation and verification

The API workload boundaries are matched; the libraries still have native result-type and arithmetic differences. Skender/Trady return dated objects and use decimal arithmetic; Ooples returns flat doubles with its documented exact stages. Rickshaw uses packed native integers versus Ooples doubles. QuanTAlib's pinned ATR bar route is scaled true range, not a conventional smoothed ATR. These contracts are checked independently rather than assumed equivalent from indicator names.

The original bar-input Asin direct comparison was slower (0.191 ms reusable versus TA-Lib's 0.169 ms). The new close-only API matches TA-Lib's input and IEEE nonfinite behavior; it does not establish a speedup of the unchanged bar API. Earlier adapter-based reports and discarded multi-invocation streaming measurements are not mixed into these tables.

Trady short-day was measured again from scratch. A native 10,000-bar call takes roughly two minutes on this machine; it is a pathological library path, not a general competitor characteristic. One warmup overlapped a build/test; measured samples ran after that work completed. Ordinary samples were collected serially. No prior Trady timing was reused.

Validation: 185 affected contract/API tests and seven evidence-verifier tests; final build results are recorded in the PR. Tests cover independent references, exact arithmetic fallbacks, lifecycle, delayed placement, direct eager ownership, native streaming initialization, IEEE Asin, buffer aliasing and rejection, and allocation gates. Existing bar kernels and the new close-only API have isolated zero-allocation checks. Raw BenchmarkDotNet harness allocations are retained.

Adversarial review findings and fixes are recorded in the blueprint. The manual CI workflow builds once, runs the affected checks, and measures eight pairs on independent runners with capability-aware evidence validation.
