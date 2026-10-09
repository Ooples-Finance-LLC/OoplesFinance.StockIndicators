# Builder performance: profile-led implementation plan

## Current focus: fused Asin/SMA pilot

The active implementation uses `Core/SmaCpuKernel.cs`: a reusable CPU loop with
value-type input and output operators. `FusedBarExecution` connects it to owned
bar ingestion and supports direct SMA/Asin plus `Asin.Of(Sma)`. The composed
path consumes each mean immediately and omits the intermediate SMA array when
SMA was not explicitly requested. The contiguous SMA core shares the arithmetic
and certificate; its existing guarded fallback is retained. This is scalar
loop fusion, not a SIMD implementation or a completed native-performance win.

The acceptance gate remains complete fresh-builder performance against TA-Lib
at 1,000 and 10,000 bars, with the existing numerical and ownership contracts.
Correctness or kernel-only wins do not authorize global rollout. Separate
arithmetic/ownership prototype losses do not rule out fused execution. Continue
work on the fused kernels and plan until this gate passes for both pilots before
extending the mechanism across the library. See the latest evidence in
[builder-ab-profile.md](builder-ab-profile.md).

## Earlier profiling and implementation sequence

This plan follows the corrected [complete builder comparisons](../benchmarks/results/eight-cpu-pilots/builder-comparisons.md). The goal remains to beat the fastest eligible native competitor for each of the eight pilot formulas through the normal public builder. These profiles diagnose costs; they do not establish new benchmark wins.

## Capture and limitations

Release net10.0, 10,000 common-grid observations, existing benchmark setup and exact same timed methods. The new `--profile-builder <pair> Builder|Native 15` entry point verifies outputs using `CpuBuilderBenchmarks.Setup`, warms the workload for two seconds, then repeats it for 15 seconds. Builder calls create and dispose a fresh complete run. Allocations use the process-wide GC allocation counter around the repeated loop; they include small harness overhead.

PerfView was run elevated with CPU/CLR GC events, 128 MB buffers and a 256 MB circular trace. Eight builder routes and the fastest native routes for the four clear losses were collected sequentially. Raw local evidence is in `C:/Users/cheat/temp/si-perfview/builder/`.

CPU exports must be filtered by the exact PID printed by `PROFILE START`, never the last process named `dotnet`: unrelated .NET builds were active on this host. Workload stack summaries select `CpuBuilderProfile.Run` and exclude `CpuBuilderBenchmarks.Setup`, retaining warmup and harness costs. Inclusive percentages overlap and must not be added. Inlining affects method attribution. These are sampled CPU shares, not elapsed-time shares.

Native runtime symbols remain partially unresolved: automatic tool policy rejected symbol-server environment configuration. Unknown frames are retained and are not labeled as allocation or GC. GC pause evidence comes separately from PerfView GCStats. The all-process stack-export command reports a NullReferenceException after writing its XML ZIP; the written ZIP/XML and exact workload PID/sample selection are validated independently, and this exporter limitation is retained in the evidence. Background activity and profiling overhead mean these loops are not substitutes for isolated BenchmarkDotNet acceptance measurements.

## Measured findings

Full extracted summaries and source/binary hashes are in [perfview-summary.json](../benchmarks/results/eight-cpu-pilots/builder/perfview-summary.json). All twelve traces contain more than 13,000 selected workload CPU samples. Approximate exclusive CPU shares below include unresolved frames in the denominator; they are not normalized to make managed code appear more expensive.

| Builder family | Allocated bytes/call | Input-domain check CPU | Other measured cost |
|---|---:|---:|---|
| Scaled true range | 3,718,102 | 15.7% | Source enumeration 6.5%; 48.1% unresolved leaves |
| Jurik | 3,722,204 | 10.1% | Certified recurrence step 20.0%; extrema deque 6.5% |
| Fractals | 4,997,541 | 11.9% | Fractal calculation 12.7% |
| Rolling pivots | 5,084,806 | 6.0% | Exact accumulator Add/Mean 39.5%; FillLevels 12.2% |
| Rickshaw | 3,720,535 | 14.9% | General-state Matches 10.7%; optimized grid state not used |
| Asin | 3,798,405 | 18.1% | Custom engine loop 6.3%; source enumeration 7.1% |
| SMA | 3,961,535 | 15.3% | SMA arithmetic 10.3% |
| Bullish short body | 3,720,990 | 11.5% | Percentile state update 18.0%; queue enumeration 9.0% |

SMA and Asin native allocations were 80,056 bytes/call; Rickshaw was 40,064, and pinned QuanTAlib scaled true range was 880,752. Native Asin has 88.1% unresolved leaf samples, so its detailed runtime attribution is incomplete. The SMA builder GC report records 3,750 collections and 1,652.888 ms cumulative pauses over the whole process, including setup/warmup; this is separate from CPU shares and cannot be added to them.

The profiles support removing shared overhead, but reject the claim that this alone solves all eight: rolling pivots spend substantial CPU in exact formulas, and Jurik spends substantial CPU in its certified recurrence. Preserve those numerical contracts and measure each remaining gap after the shared change.

## Reproducing a capture

After a Release build, run PerfView elevated with the benchmark DLL:

```text
PerfView.exe /AcceptEula /NoGui /NoNGenRundown /CircularMB:256 /BufferSizeMB:128 /DataFile:builder-sma.etl run "C:/Program Files/dotnet/dotnet.exe" "<benchmark-output>/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll" --profile-builder TaLib.Functions.Sma Builder 15
```

Use `Native` for the paired competitor. Read the process ID from the profile log before examining stacks or GC statistics. The command-line profiler duration applies after setup/warmup; a single slow native invocation can exceed it. Do not run the pathological Trady short-body native route merely to diagnose Ooples overhead.

## Bounded implementation order

1. **Input storage and finite-source traversal.** Retain one owned bar history for snapshots; materialize OHLCV columns only when the selected evaluator actually needs them. Avoid geometric growth where a safe count is available, and avoid copying already-owned columns into another container. Investigate a synchronous internal path for the library's own enumerable sources while keeping the public builder lifecycle and arbitrary asynchronous sources supported. Do not enumerate a caller's source twice or borrow mutable caller buffers silently.
2. **Validation work.** Consolidate only checks proven redundant for the same unchanged input. Preserve eager finite validation, indicator-specific domains, chained-close validation, output-slot/bar diagnostics and failure ordering. A configurable domain getter is customer code: caching or suppressing its calls is not automatically semantics-preserving. Optimize the common finite-domain check without weakening the domain contract.
3. **Pilot routing and arithmetic.** Rickshaw's builder currently constructs `RickshawManCandle.State`, whereas the modern kernel uses `RickshawGridState`. Reuse the proven fast state only with preserved fallback, preview and reset behavior. It eagerly allocates arrays by period, so direct substitution would regress large-period/short-input behavior; retain a lazy/general route or bound internal fast-path eligibility without changing accepted public periods. SMA's builder uses `MovingAverageCore.SimpleMovingAverage`, whose cancellation certificates, periodic rebuilding and exact fallback cost more than an ordinary running sum. Optimize these under the current contract; do not substitute TA-Lib's arithmetic or assume the separate SMA kernel is bitwise equivalent. Keep Asin's two outputs and undefined-value policy intact when considering a batch loop.
4. **Remeasure before designing more infrastructure.** Run the complete builder gates against all eligible competitors on the same runner per family. If dependency resolution or dispatch remains material, introduce a per-build execution plan then. Do not start with a global compiled-plan cache or framework rewrite on single-indicator profiles. The explicitly requested GPU pilot is bounded to SMA/Asin and must establish correctness and end-to-end benefit before rollout.

## Adversarial review of the earlier proposal

| Challenge | Required correction |
|---|---|
| “Builder is the entire problem.” | False as a working assumption: SMA arithmetic and Rickshaw's older state are observable costs. Optimize both routing and shared overhead. |
| “Cache everything at setup.” | Fresh-builder setup stays timed. Graph nodes, options, sources and user domains can be mutable; cross-run caching needs an explicit validity/lifetime design. |
| “Remove repeated validation.” | Prove identity of input and domain first. Source chaining changes close while retaining other fields. Invalid input must not mutate state before rejection. |
| “Use each existing fast kernel directly.” | Check exact outputs, warmup, presence flags, snapshot placement, output errors, huge periods, framework support, preview and ownership first. Rickshaw's eager period arrays are a concrete counterexample. |
| “Zero allocation for the builder.” | Fresh runs own results and retained bars. Reduce avoidable allocations; zero steady-state allocations is a distinct reusable workload and cannot replace the acceptance gate. |
| “All eight will win after one rewrite.” | Unsupported. TA-Lib's output-only loops expose less behavior and some use weaker numerical guarantees. Keep all losses visible and measure residual gaps. |
| “The 1/1024 input grid represents all inputs.” | It helps fair cross-library conversion but favors certified grid fast paths. Add ordinary off-grid and cancellation/large-range cases before generalizing performance claims. |
| “Batch results establish streaming performance.” | They do not. Shared state changes require preview/commit/reset checks; live latency/allocation needs separate measurements. |

## Verification for the first implementation batch

Review the complete diff before building. Run focused builder graph, source/warmup, live-lifecycle and validation contracts plus the eight pilot correctness checks. Add targeted regressions for single enumeration, cancellation, snapshots, independent run ownership, disposal, chained domain errors and any changed state route. Verify Rickshaw grid-to-general transitions and large-period short histories if that route changes. Keep exact assertions exact.

Build affected target frameworks only where shared code or framework-conditional routing changes. Benchmark 1,000 and 10,000 observations through fresh builders against the full eligible inventory, retaining confidence intervals and allocation results. Include off-grid/adversarial numerical workloads without using pathological competitor runtimes to manufacture a speedup claim. Repeat PerfView only for unresolved residual costs. The original profiling batch changed no library behavior. The first implementation batch is recorded below.


## First implementation batch

The finite builder now drains built-in enumerable sources synchronously in one pass, reserves history for arrays/lists with known counts, creates columns at their actual size, and transfers those private columns into `StockData`. The public `StockData` constructor continues to copy caller input. The common finite-domain check has a direct fast path; all validation calls and failure diagnostics remain. Rickshaw uses the existing exact grid state for windows up to 4,096, retaining the lazy general state for larger periods and the existing .NET Framework route. This is an internal eligibility threshold, not a public period limit. Legacy runtime construction and column materialization remain for the next measured iteration.

Adversarial review covered single enumeration, projection cancellation/disposal, mutable caller input, async-source equivalence, warmup snapshots, validation diagnostics, independent Rickshaw formulas, grid-to-general transitions, preview/reset, and huge periods on short input. Added regressions cover these risks and bound fresh-builder transform allocation below 1.5 MB at 10,000 bars.

Verification: 69 focused unit checks and 131 competitor-facing checks passed on net10.0; library builds passed net10.0, net8.0 and net461. Existing package support warnings remain. A warmed allocation probe used the same profiler harness with a one-second measurement loop after two seconds of warmup. Every setup correctness check passed. The shell's final log redirection failed after all eight probes completed; the following values are taken from their captured terminal output, not an output file. Host activity makes these unsuitable as timing claims.

| Family | Before bytes/call | After bytes/call |
|---|---:|---:|
| SMA | 3,961,535 | 1,292,556 |
| Asin | 3,798,405 | 1,129,236 |
| Rickshaw | 3,720,535 | 1,049,649 |
| Scaled true range | 3,718,102 | 1,049,226 |
| Jurik | 3,722,204 | 1,053,162 |
| Rolling pivots | 5,084,806 | 2,416,462 |
| Fractals | 4,997,541 | 2,328,837 |
| Bullish short body | 3,720,990 | 1,051,766 |

The complete eight-family CI benchmark campaign is required to judge throughput and remaining losses. This allocation reduction does not establish eight performance wins.


## Second implementation batch

The column bridge is now lazy. An empty legacy graph over the builder's privately owned, validated history still publishes its snapshot and runs notifications, without materializing OHLCV columns. Named sources, explicit subscriptions, keys, nonempty graphs and already-exposed mutable columns keep the normal evaluator/validation path. A later legacy `Build()` materializes the retained original history correctly.

Default finite checks are skipped only for unchanged raw bars with the builder's validation proof. Stable library domains are resolved once; customer `InputDomain` getters retain their original calls. Chained closes still receive their domain checks before state updates, and direct engine callers without validation proof still validate raw bars.

Adversarial review added checks for publication, delayed legacy use, mutable exposed columns, invalid named sources, customer domain-getter calls, invalid chained values, and untrusted direct engine use. The final review found an explicit unknown-subscription edge case; its normal evaluator error is now covered and preserved. Verification passed 107 focused unit checks, 131 competitor-facing checks, and a final 32-check affected subset after that guard. All three library frameworks built successfully after the final change.

The new eight-route allocation probe completed with successful setup checks. Approximate bytes/call: SMA 1,292,569; Asin 647,396; Rickshaw 567,829; scaled true range 566,872; Jurik 570,699; pivots 1,934,385; fractals 1,846,888; short body 569,333. The custom-only allocation regression now requires less than 800 KB for 10,000 bars. These are allocation diagnostics, not throughput wins. Source logs: `C:/Users/cheat/temp/si-perfview/builder-second-fix-allocations.log`.


## FP64 GPU pilot using AiDotNet.Tensors

The modern targets reference published AiDotNet.Tensors 0.134.4. The pilot reuses
its public OpenCL context, typed double buffers, compiler, kernel arguments, queue
and readback APIs. It does not depend on the unreleased CPU codegen PR or the
Float32-only high-level fused executor. Framework remains CPU-only.

`ConfigureExecution(IndicatorExecutionBackend.Gpu)` requires actual GPU work for
plain finite array-backed SMA, Asin, and Asin.Of(Sma) builder graphs. Unsupported
graphs, sources, devices or uncertified rolling sums throw. `LastExecution` reports
the actual device/backend on success and is cleared on failed BuildAsync calls.
Legacy Build does not support required GPU execution. Auto currently selects CPU;
a universal size threshold cannot be inferred from one device.

The GPU compiler specializes a single kernel per period/output graph, caching at
most 32 programs. SMA work items own 64-bar blocks and feed Asin directly in device
registers; an unrequested SMA series has no device or host output allocation.
Input ownership and finite validation precede upload; arithmetic stays binary64.
The exact-grid certificate bounds rolling sums, including temporary add-before-
evict sums. Windows above 4096 are rejected unless all bars are still warming up.
Kernel argument binding through readback is serialized to protect the shared
queue. Host cancellation is checked before launch and after blocking readback;
an already launched device kernel is not preempted. Context lifetime is process-wide.

This is a bounded OpenCL implementation, not all-device/all-indicator support.
No float narrowing, generic tensor graph migration, or global indicator rollout is
included. Hardware tests skip explicitly when a suitable FP64 device is absent.
