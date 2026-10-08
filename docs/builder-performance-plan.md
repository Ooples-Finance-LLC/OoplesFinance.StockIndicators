# Builder performance: profile-led implementation plan

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
4. **Remeasure before designing more infrastructure.** Run the complete builder gates against all eligible competitors on the same runner per family. If dependency resolution or dispatch remains material, introduce a per-build execution plan then. Do not start with a global compiled-plan cache, framework rewrite, new public reuse API or GPU work on the evidence from single-indicator batch profiles.

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

Build affected target frameworks only where shared code or framework-conditional routing changes. Benchmark 1,000 and 10,000 observations through fresh builders against the full eligible inventory, retaining confidence intervals and allocation results. Include off-grid/adversarial numerical workloads without using pathological competitor runtimes to manufacture a speedup claim. Repeat PerfView only for unresolved residual costs. No library implementation changes are included in this profiling batch.
