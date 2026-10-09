# Builder A/B diagnosis

The baseline investigation covers the three losses in the eight-pilot CI campaign: Asin,
SMA and Rickshaw. It separates builder orchestration from our arithmetic and
compares the actual TA-Lib.NETCore 0.5.0 source. The baseline measurements below
use production commit `cf49cfe5`; the following implementation section records
the fixes made after that diagnosis.

## Measurement boundaries

`--profile-builder <pair> <arm> <seconds>` now supports four arms:

| Arm | Work inside each invocation |
|---|---|
| Builder | Fresh public builder, source wrapping, validation, owned history, computation, all outputs and disposal |
| Compute | Asin/Rickshaw: fresh instance of the same internal custom engine, indicator and state, validation and all output columns; SMA: the same `MovingAverageCore.SimpleMovingAverage` arithmetic with a fresh output array |
| Kernel | Fresh OHLCV kernel and owned output array, including full finite OHLCV validation; no close-only Asin shortcut |
| Native | Native TA-Lib API with a freshly allocated output array |

All arms use the same deterministic 10,000-bar fixture rounded to the same 1/1024
grid. Reflection binds typed diagnostic delegates once during setup, outside
measurement; there is no per-call reflection and no production API change.
Setup checks every Compute output against the builder, including presence and
warmup, and checks the kernel against its existing exact public reference.

These are ablations, not interchangeable contracts. The standalone custom engine
checks raw input both before processing and in its per-bar loop; the builder
validates owned history and can skip those engine checks. SMA's Compute arm omits
builder validation, graph evaluation, column construction and publication. Asin's
builder/engine have two columns (value/presence); its kernel/native have one.
Rickshaw's native output is `int[]`, ours is `double[]`. The SMA kernel uses an
exact accumulator while the builder/core use guarded floating-point arithmetic.
Do not label the difference between two arms as pure builder overhead.

## Unprofiled local A/B observations

Three fresh processes per arm, two-second warmup and two-second measurement,
alternating arm order. Windows, Ryzen 9 3950X, .NET 10.0.12. Times below are medians
and full observed ranges, not confidence intervals. The machine was not isolated;
the large timing variation rules out using these as acceptance speedup claims.
Use the same-runner BenchmarkDotNet campaign for competitor acceptance.

| Indicator | Arm | Median ms (range) | Median bytes/call |
|---|---|---:|---:|
| Asin | Builder | 0.558 (0.540–0.567) | 647,502 |
| Asin | Compute | 0.585 (0.559–0.670) | 160,880 |
| Asin | Kernel | 0.261 (0.254–0.266) | 80,048 |
| Asin | Native | 0.213 (0.145–0.252) | 80,056 |
| SMA | Builder | 0.776 (0.733–2.557) | 1,292,553 |
| SMA | Compute | 0.261 (0.241–0.359) | 80,056 |
| SMA | Kernel | 0.697 (0.667–0.881) | 80,280 |
| SMA | Native | 0.026 (0.025–0.060) | 80,056 |
| Rickshaw | Builder | 0.999 (0.900–1.033) | 567,891 |
| Rickshaw | Compute | 0.915 (0.912–1.313) | 81,040 |
| Rickshaw | Kernel | 0.756 (0.749–0.801) | 80,456 |
| Rickshaw | Native | 1.050 (1.017–1.374) | 40,064 |

All nine measured builder intervals had generation-2 collections; none of the
27 other intervals did. Counts come from `GC.CollectionCount`, and allocations
from process-wide `GC.GetTotalAllocatedBytes(precise: true)`, measured only around
the repetition loop. This establishes a real allocation/collection difference,
not how many milliseconds GC caused. The local Rickshaw ordering differs from
CI; do not replace the CI loss with a claimed win from this machine.

Source inspection explains a substantial part of the allocation gap:
`StockIndicatorBuilder.BuildAsync` stores an owned `List<Bar>`; 10,000 bars at
48 bytes each require a roughly 480 KB backing array, above the large-object
heap threshold. SMA also requests `CreateOwnedBatch`, which allocates six
10,000-element date/price/volume columns. Asin and Rickshaw defer those columns.
These sizes are calculated from the source and are not an allocation-stack
census. Any pooling or storage change must preserve snapshot ownership and
later legacy `Build()` calls; simply retaining a caller's mutable array is not
a valid optimization.

## Elevated PerfView evidence

ETW captures use the same executable and fixtures as the unprofiled measurements,
with two seconds of warmup and eight seconds of measured repetition per arm.
CPU summaries select the exact logged PID and `CpuBuilderProfile.Run` stacks,
excluding benchmark setup and the A/B constructor's reference checks. They include
warmup. Percentages describe selected foreground CPU samples, not elapsed time
or all GC worker CPU. Exports of completed captures overlapped later captures;
unprofiled timing runs finished before any collection/export began.
Method attribution includes any inlined callees: samples attributed to the
generic engine are not all proven dispatch overhead.

SMA's builder trace attributes 26.9% of selected samples exclusively to
`MovingAverageCore.SimpleMovingAverage`, 10.4% to `CreateOwnedBatch`, and 8.1% to
`BatchInputValidation.Validate`. Its Compute trace attributes 92.5% exclusively
to the same arithmetic. The SMA kernel instead spends 81.7% exclusively in its
own exact-accumulator evaluation. This independently supports the source finding
that both orchestration and arithmetic matter, and that swapping in the current
kernel is not an established optimization.

Asin's builder trace attributes 14.1% exclusively to the generic custom engine,
15.1% to `BuildAsync`, and 6.2% to the transform state. The standalone engine
attributes 9.3% exclusively to input validation, confirming that the validation
difference between the two arms is material. Unresolved native/runtime leaves
account for 53.6% of the builder trace and 88.5% of the native Asin trace; they
must not be relabeled as GC or any particular math function.

PerfView GCStats for the Asin builder process records 1,285 `AllocLarge`
collections and 1,304.9 ms of total GC pauses, versus 2 such collections and
85.2 ms for Compute. SMA's builder records 656 `AllocLarge` collections and
992.6 ms of pauses, versus 1 and 91.7 ms for Compute. These are whole-process
totals including setup, verification and warmup, with different repetition counts.
They establish large-allocation pressure, not a per-call pause-time difference.

Rickshaw's builder trace attributes 35.4% exclusively to the custom engine,
15.5% to grid conversion, 12.1% to ring-buffer append and 8.3% to checked
`Int128` multiplication. Only 13.4% of its selected leaf samples are unresolved.
The exact conversion/comparison machinery is therefore a concrete arithmetic
target alongside storage and engine traversal; TA-Lib's floating-point totals
are not a drop-in replacement for that contract.
TA-Lib's native Rickshaw trace spends 56.9% exclusively in `CandleRange`, 33.9%
in `RickshawManImpl` and 4.7% in candle-setting dictionary lookup. There is
optimization opportunity relative to that implementation; it is not a zero-cost
baseline. Rickshaw's builder GC report records 1,022 `AllocLarge` collections
and 822.2 ms of whole-process pauses, versus 1 and 50.9 ms for Compute.

## Pinned competitor source findings

The installed 0.5.0 package's NuGet repository metadata identifies commit
`0bc2086a6cafc5b4c17fd398e0c16895e4daf92c`. These are that exact revision's sources,
not the competitor's moving main branch:

- [Asin](https://github.com/hmG3/TA-Lib.NETCore/blob/0bc2086a6cafc5b4c17fd398e0c16895e4daf92c/src/TALib.NETCore/Functions/TA_Asin.cs): validates the input range, then writes `T.Asin(inReal[i])` in one loop. It does not copy OHLCV history, dispatch a state per bar, clear a scratch output, copy two columns, or publish a run. Our Asin state and engine both clear the output scratch; the engine also dispatches the state and copies its two outputs. A fused batch loop can target these costs while retaining presence and validation semantics.
- [SMA helper](https://github.com/hmG3/TA-Lib.NETCore/blob/0bc2086a6cafc5b4c17fd398e0c16895e4daf92c/src/TALib.NETCore/Functions/FunctionHelpers.cs#L269): initializes a scalar sum, then adds, subtracts and divides once per bar. Our core tracks roundoff, checks cancellation, periodically rebuilds the window sum, and can use an exact fallback. The direct-core measurement still trails TA-Lib, so eliminating builder overhead alone will not close the gap. Copying its recurrence would discard numerical protections; a faster certified path must preserve our published results.
- [Rickshaw](https://github.com/hmG3/TA-Lib.NETCore/blob/0bc2086a6cafc5b4c17fd398e0c16895e4daf92c/src/TALib.NETCore/Candles/TA_RickshawMan.cs): computes comparisons from scalar rolling totals and writes integer classifications. Our grid state converts prices into exact integer units, uses `Int128` comparisons, maintains two ring buffers and has a general exact fallback. The arrays and builder history are additional costs; the comparison arithmetic also differs. The fixture exercises grid-compatible input, so it does not establish fallback performance.

## Reproduction and review

The next optimization batch should address the measured boundaries:

1. Reduce owned-history allocation pressure and avoid materializing unrelated
   SMA columns while preserving input snapshots, builder reuse and output lifetime.
2. Specialize eligible batch execution to remove redundant scratch clearing,
   state dispatch and output copying without bypassing components, chaining,
   presence flags or output validation.
3. Evaluate certified SMA fast paths and Rickshaw conversion/comparison costs.
   Keep existing numerical fallbacks; do not replace them with TA-Lib's simpler
   arithmetic merely to improve a timing result.

After implementation, repeat the same A/B boundaries and the public-builder
BenchmarkDotNet acceptance matrix. Neither kernel-only wins nor lower allocation
alone satisfy the eight-indicator performance goal.

Build the benchmark project once in Release, then run:

```text
python scripts/profile-builder-ab.py <benchmark.dll> <output-directory>
PerfView.exe /AcceptEula /NoGui /DataFile:<trace.etl> run dotnet <benchmark.dll> --profile-builder TaLib.Functions.Asin Builder 8
```

Repeat the PerfView command for Compute, Kernel and Native and the other two
families. Collect sequentially, separately from unprofiled measurements. Inspect
the workload's exact logged PID, not the last process named `dotnet`.

Adversarial review: retain losing/noisy measurements; keep owned outputs inside
timing; distinguish same-core and different-kernel paths; exclude fixture setup,
reflection binding and reference verification from selected CPU stacks; never
attribute unresolved runtime samples to GC. Full-library, cross-platform or
streaming speedups are not established by this batch.

Validation completed: the Release harness build passed while reusing unchanged
production outputs; all 36 unprofiled invocations completed their setup checks;
all 12 elevated ETW captures and GCStats exports completed. All 12 CPU exports
were parsed and each contained more than 8,000 selected workload samples.
PerfView's all-process CPU export reports a post-write exception; readable XML
ZIP output and sample coverage were verified independently. The Python runner's
CLI help and diff whitespace checks passed. No production test suite was rerun
for this harness/documentation-only batch.

[Measurements, CPU/GC summaries, binary/source hashes and trace hashes](../benchmarks/results/eight-cpu-pilots/builder-ab/)
are committed. Raw ETL ZIPs, CPU stack exports, GCStats logs/CSVs and exact
collection scripts remain under `C:/Users/cheat/temp/si-perfview/builder-ab/`
(GCStats CSVs are in the PerfView cache paths recorded by those logs).

## Implementation following the diagnosis

- Owned history now uses chunks of at most 1,024 bars, avoiding a large-object
  backing array while retaining independent snapshots and later legacy builds.
  Warmup publication uses a read-only view of that owned history rather than
  copying the published bars into another large array. This changes allocation
  shape, not the requirement to own the input.
- Asin and the existing exact Rickshaw grid state can process those chunks
  directly after components have executed. The shortcut requires validated raw
  input and no chained source. General/chained validation, output validation,
  presence columns and Rickshaw's exact fallback remain in place.
- SMA checks whether every input lies on a common binary grid and every possible
  intermediate sum fits in 53 significant bits. If certified, additions and
  evictions are exact; removing roundoff tracking and periodic rebuilding cannot
  change the rounded result. The final division uses the same exact sum. The
  certificate conservatively excludes extreme exponents, subnormals, nonfinite
  inputs, overlap and overly wide grids. All other inputs retain the existing
  guarded implementation. Ordinary decimal prices need not qualify.

The A/B harness adds `Prepared`: prevalidated, independently owned history is
built during setup, then each call creates a fresh engine/state and all output
arrays using the actual batch shortcut. SMA's Prepared arm is its direct core,
like Compute. The original Compute arm remains unchanged in scope, so its
standalone validation cost is still visible. Both sets of complete outputs are
checked against the public builder before timing.

Verification: 125 focused regression checks and 111 competitor-facing checks
passed; Release library builds passed for net10.0, net8.0 and net461. The focused
checks include independent rational SMA rounding, late certificate rejection,
negative/signed-zero inputs, chunk boundaries, warmup ownership, chained Asin,
Rickshaw fallback, graph composition and legacy builder reuse. Adversarial review
also covered partial last chunks, source count hints, disposal, and the unchanged
general validation routes. Checked-conversion issues exposed by the initial run
were corrected before the successful rerun.

### Completed follow-up measurements

[Campaign 37861716471](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/37861716471)
completed all 56 measurements at implementation commit `04c583fa`. At 10,000 bars,
against the fastest measured eligible native route in each family:

| Family | Ooples builder ms | Native ms |
|---|---:|---:|
| Jurik / QuanTAlib | 0.9820 | 1.7046 |
| Scaled true range / QuanTAlib (not conventional ATR) | 0.2009 | 0.3917 |
| Rolling pivots / Skender | 4.2089 | 6.4007 |
| Fractals / Skender | 1.0761 | 6.3894 |
| Rickshaw / TA-Lib | 0.6640 | 0.6939 |
| Asin / TA-Lib | 0.2981 | 0.1294 |
| Bullish short body / Trady tuple | 0.9834 | 53358.9739 |
| SMA / TA-Lib | 0.4612 | 0.0219 |

All eight paired confidence intervals are separated at this size. Six favor the
builder, including a narrow Rickshaw win; Asin and SMA remain losses at about
2.30x and 21.07x slower. Trady short-body remains pathological; its ratio is not
a general performance claim. Different campaigns use different hosts, so the
changes between campaign means are not controlled before/after speedups.

All 45 local A/B invocations passed their setup/output checks. The nine measured
builder intervals had zero generation-2 collections, compared with collections
in every baseline builder interval. Median bytes/call remain approximately
647 KB / 1,292 KB / 568 KB for Asin / SMA / Rickshaw: history is still owned, but
its allocation shape changed. Local median Builder / Prepared / Native timings
were 0.513 / 0.191 / 0.139 ms for Asin, 0.580 / 0.077 / 0.026 ms for SMA, and
0.840 / 0.679 / 1.321 ms for Rickshaw. Retain the full ranges in the raw data;
these exploratory runs are not substitutes for the CI confidence intervals.

Two follow-up elevated PerfView captures completed, for Asin Builder and Prepared.
The builder's whole-process GC report records one `AllocLarge` collection and
479.1 ms of pauses, versus 1,285 and 1,304.9 ms in the earlier capture; repetition
counts differ. The remaining collection stopped before SMA started because C:
ran out of space. Regenerable cache files were removed only after verifying the
compressed originals' hashes; the elevation retry was canceled. No post-fix SMA
or Rickshaw ETW capture is claimed.

[Follow-up evidence](../benchmarks/results/eight-cpu-pilots/builder-followup/)
contains all 56 CI records with confidence intervals and host metadata, all 45
local runs, and both completed CPU/GC summaries. The later diagnostic-only Sonar
cleanup retains exact output equality and extracts verification from the harness
constructor; its Release build and all three Prepared setup/output replays passed.


## Close-only builder follow-up (c4370935)

The pinned competitor source above led to two additional changes. Direct-close
SMA/base graphs read one close column from the validated owned history instead
of materializing and validating six legacy columns. Registration, CSE, publication
and deferred lookup remain in the runtime. Mixed graphs, named sources, unknown
handles and already-exposed mutable columns retain the ordinary evaluator.
The sealed Asin batch path no longer rescans its output: it writes Math.Asin only
for [-1,1], zero for absent results, and a 0/1 presence column. Other states,
chained inputs and customer startup callbacks retain output validation.

SMA arithmetic is unchanged. TA-Lib's scalar rolling recurrence omits our
roundoff/cancellation safeguards; copying it unconditionally would change the
numerical contract. Both Asin implementations still use the same math primitive.

Validation: 94 focused unit tests and 115 competitor checks passed. New regressions
cover empty/short/chunked inputs, CSE, publication, snapshot disposal, deferred EMA
lookup, mixed graphs, mutable columns, builder reuse and an allocation ceiling.
Existing extreme-SMA and startup/output-contract tests also passed. Release builds
passed for net10.0, net8.0 and net461.

[CI campaign 37873758742](https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/37873758742)
measures production commit c4370935. Completed targeted results at 10,000 bars:

| Family | Public builder ms | Fastest native ms | Result |
|---|---:|---:|---|
| Asin / TA-Lib | 0.28705 | 0.13181 | Builder loses, 2.18x |
| SMA / TA-Lib | 0.22850 | 0.02297 | Builder loses, 9.95x |

These are complete fresh-builder lifecycles, with ownership and validation inside
timing. SMA was measured against all six eligible competitor routes. Different
campaign hosts prevent treating changes from the earlier campaign as controlled
before/after speedups. The full campaign's remaining families are separate from
these completed targeted results.

All 45 local alternating-order A/B runs completed. Median Builder / Prepared /
Native times were 0.2515 / 0.1216 / 0.1111 ms for Asin and 0.2304 / 0.0534 /
0.0263 ms for SMA. SMA builder allocation is 729,448 bytes/call versus 1,292,440
in the previous diagnostic, a 43.6% reduction; Asin is 647,097 bytes/call. No
measured builder interval collected Gen2. Local timings varied and are exploratory.

Four follow-up EventPipe captures (Asin/SMA, Builder/Prepared) were converted by
PerfView using NetperfToSpeedScope. The committed summary weights exported event
intervals beneath CpuBuilderProfile.Run, excludes fixture setup/construction, and
includes warmup. Synthetic time leaves are stripped for method attribution.
Many samples land in runtime hash-code, GC polling and monitor helpers; safe-point
bias and inlining prevent treating these percentages as exact function CPU costs
or GC pause percentages. These are sampled thread-time traces, not elevated ETW
CPU/GC captures. Original traces and PerfView exports are retained locally at
C:/Users/cheat/temp/si-builder-close-profile; committed hashes identify them.

[Machine-readable evidence](../benchmarks/results/eight-cpu-pilots/close-only-followup/)
contains all 45 local measurements, 28 targeted CI measurements with confidence
intervals and host metadata, and the four trace summaries. Both losses remain;
this batch does not establish eight wins.


## Feasibility gate: exact CPU prototypes

**Decision: neither candidate passes the full-builder feasibility gate. No
prototype was integrated into production.** The acceptance benchmark is unchanged.
The benchmark project contains the experiments and regression checks so this is
an executable negative result, not a forecast about a future architecture.

All figures below are microseconds for 10,000 inputs, compared within the same
local BenchmarkDotNet campaign. Arrays for reusable kernels are allocated in
setup on both sides. Owning pipelines allocate inside timing. The ownership
prototype retains all bars, validates every OHLCV numeric field and emits value
and presence arrays, but excludes builder graph/publication overhead.

| Experiment | Candidate | TA-Lib comparable arm | Finding |
|---|---:|---:|---|
| Asin unrolled, reusable | 91.03 | 100.29 | Small kernel lead |
| Asin four-worker, reusable | 33.86 | 100.29 | Kernel lead; scheduling allocates |
| Asin initial owned parallel pipeline | 208.98 | 109.16 | Loses before builder overhead |
| Asin optimized owned parallel pipeline | 165.14 | 112.37 | Still loses before builder overhead |
| SMA certified SIMD, reusable grid | 34.71 | 17.19 | Loses despite improving our 49.86-us core |
| SMA SIMD wrapper, reusable non-grid | 176.67 | 17.07 | Correct fallback, no meaningful gain |

The final Asin pipeline's 99.9% interval is 153.20–177.07 us versus native
109.66–115.08 us. Its optimized ingestion alone costs 61.27 us (54.24–68.29).
At 1,000 and 100,000 inputs the owning parallel pipeline also loses. The
four-worker math-only crossover therefore does not establish a builder crossover.
It consumes multiple CPU workers, unlike the scalar native arm; scheduler costs
and allocations remain timed. No zero-allocation claim is made for parallel work.

The SMA prototype proves all vector prefix intermediates fit exact binary sums,
using a stricter window-plus-eight-term certificate, then performs vector
addition/subtraction/division. Rejected data uses the unchanged production core.
Non-grid fixtures add deterministic fractional variation and assert certificate
rejection in setup. The original unrounded fixture happened to be certifiable;
its first purported non-grid results were discarded and that campaign is not
used for SMA conclusions. Reproducing the experiment must retain this assertion.

For ownership, the first prototype copies chunks and calls the existing finite
validator. The follow-up uses uninitialized reference-free Bar arrays, fully
copies each chunk before reading it, and inlines finite checks with the original
validator for errors. It validates the owned copy, avoiding a check/copy race.
This experiment applies only to plain arrays, not custom enumerators/projections.
The reported ingestion-only builder is today's implementation, not a theoretical
lower bound; timings from separate arms are not an additive performance model.

Validation covers independent integer window sums, vector tails, precision and
exponent boundaries, untouched buffers on certificate rejection, exact fallback,
Asin endpoints/signed zero/undefined values, parallel partitions, owned history,
field diagnostics, cancellation and parity with actual builder outputs. The pinned
TA-Lib rejects singleton input ranges, so that case checks the Math.Asin contract
instead; all timed sizes use supported native ranges.

[69 valid measurements and host metadata](../benchmarks/results/eight-cpu-pilots/feasibility/)
include the initial Asin arithmetic campaign, corrected SMA campaign, and both
ownership campaigns. Each uses five warmups and ten measured iterations with a
100-ms minimum iteration time. Profiling was off. Raw reports are retained at the
paths and hashes in metadata; no comparison across separate campaigns is claimed
as a controlled speedup. Production library code is unchanged by this experiment.

Reproduce from the Release competitor benchmark executable with:

```text
--filter '*FeasibilityBenchmarks*' --exporters json --artifacts feasibility
```

The broad filter now includes all three classes, including both ownership variants.
For just the optimized ownership candidate and its baseline:

```text
--filter '*AsinOwnershipFeasibilityBenchmarks*Direct*' '*AsinOwnershipFeasibilityBenchmarks.NativeOwned*' --exporters json --artifacts ownership
```

Release benchmark builds and 84 focused checks passed, including 21 prototype
regressions. The existing CI performance build now includes CpuFeasibilityTests in its
correctness gate. Infrastructure cleanup may still be valuable, but these results
do not justify claiming it will produce wins over TA-Lib. These experiments did
not integrate all stages of a fused execution plan, so their losses do not rule
out that design. The fused pilot is evaluated through the complete builder before
any global rollout.

## Shared array ingestion follow-up (October 9)

`Bars.From(Bar[])` now copies chunks directly into owned history and validates
the owned copy. This removes per-bar enumeration, projection and history-append
dispatch for plain arrays. Projected, custom and asynchronous sources retain
their existing paths. Cancellation, field diagnostics, snapshot ownership and
later appends remain covered. Indicator arithmetic is unchanged.

`ArrayBuilderBenchmarks` compares the complete array builder, the retained
identity-projection builder and native owned TA-Lib outputs in the same process.
The longer repeat used eight warmups and twenty measurements targeting 500 ms
per iteration. Means in microseconds:

| Family | Bars | Array builder | Projected builder | TA-Lib |
|---|---:|---:|---:|---:|
| Asin | 1,000 | 32.502 | 38.356 | 17.848 |
| Asin | 10,000 | 315.217 | 334.523 | 163.471 |
| SMA | 1,000 | 27.229 | 34.749 | 2.797 |
| SMA | 10,000 | 289.123 | 308.389 | 26.807 |

Array/projected confidence intervals separate at 1,000 bars and overlap at
10,000 bars. Both remain slower than TA-Lib. Unrelated host workloads affected
the initial run; the repeat also varies substantially. These measurements do
not establish a stable 10,000-bar improvement or constrain what a future shared
engine can achieve. Allocation remains essentially unchanged.

[Both runs and source hashes](../benchmarks/results/eight-cpu-pilots/array-ingestion/)
are retained. Verification passed 91 focused unit tests, 98 competitor-facing
checks covering the eight pilots, and Release builds for net10.0, net8.0 and
net461. The latter retains existing package-support warnings.

Reproduce with the Release benchmark executable:

```text
--filter '*ArrayBuilderBenchmarks*' --exporters json --iterationCount 20 --warmupCount 8 --iterationTime 500 --artifacts array-ingestion
```

## Fused execution pilot (October 9)

`FusedBarExecution` owns and validates each input while computing final Asin/SMA
outputs in the same traversal. Kernel dispatch occurs per chunk; the scalar hot
loop keeps arithmetic state and output references local. SMA accumulates a
conservative exactness certificate during ingestion, refines it against owned
history when necessary, and replaces all speculative results with the existing
numerical core if certification fails. No speculative value is published.

Plain typed-only runs publish directly from the plan without constructing a
legacy evaluator/runtime or copying the result arrays. Owned history remains
available for snapshots and later legacy builds. The pilot supports direct
Asin, one normalized SMA period, or both together, over array sources on modern
runtimes. Other sources/graphs and legacy-configured runs retain existing
execution; net461 retains its established arithmetic path.

Adversarial review covered cancellation, first-invalid-field diagnostics,
caller mutation, chunk boundaries, signed zero, undefined Asin values, exact
SMA rounding, late certificate rejection, overflowed speculation, huge periods,
duplicate outputs, disposal, deferred reads, and exposed mutable columns. It
also identified synchronous legacy callbacks which can change an indicator's
source or add indicators before typed results are read. Excluding legacy
configuration from pilot planning preserves those semantics; a regression uses
a local callback adapter and compares complete outputs with the unfused path.

Final verification: **134 focused unit tests and 119 competitor-facing checks
passed**, plus Release builds for net10.0, net8.0 and net461. The standard public
builder suite produced all eight required measurements and passed the report
validity checker. These are fresh complete builders, not kernel-only timings.

| Family | Bars | Fused public builder (us) | TA-Lib (us) | Verdict |
|---|---:|---:|---:|---|
| Asin | 1,000 | 26.675 | 15.755 | Loses |
| Asin | 10,000 | 251.265 | 142.379 | Loses |
| SMA | 1,000 | 12.230 | 2.620 | Loses |
| SMA | 10,000 | 100.596 | 24.211 | Loses |

**The performance acceptance gate has not passed; no global rollout is justified.**
The controlled array-versus-projection diagnostic preceding the final callback
guard measured lower fused means at 10,000 bars: Asin 223.923 versus 333.004 us,
SMA 111.612 versus 281.700 us. Those gains do not erase the native losses. Local
host variation also prevents treating different campaigns as controlled speedups.

[Raw reports, confidence intervals, allocations and source/binary hashes](../benchmarks/results/eight-cpu-pilots/fused-builder/)
retain the initial fused implementation, the block-kernel diagnostic and the
final standard builder comparisons. The next work remains optimization of these
two fused kernels and their execution plan under the same public-builder gate,
before extending fusion to other indicators. These results do not establish a
limit on what a further optimized fused implementation can achieve.


## Shared SMA CPU kernel and dependency fusion (2026-10-09)

`Core/SmaCpuKernel.cs` now owns the shared arithmetic and certification. Its
struct input/output operators fuse owned ingestion, validation, rolling SMA,
and downstream consumption. The contiguous SMA core uses the same arithmetic
without allocating a ring. The pilot accepts `Asin.Of(Sma)`; when SMA is not
explicitly configured, no intermediate SMA array is allocated or written on
the certified path. Failed certification rebuilds SMA and dependent Asin before
publication. The exact public streaming kernel and guarded core fallback retain
their contracts. This implementation is scalar loop fusion, not SIMD.

Adversarial checks cover exact rational windows, block boundaries, late rejection,
intermediate elision, period 1, oversized periods, output visibility and dependency
warmup. All 127 selected unit tests and 119 competitor tests passed; Release
builds passed on net10.0/net8.0/net461 (existing Framework package warnings).
Final Tier1 assembly inlines input, SMA and consumer operations; Math.Asin remains
a normal math call.

Final fresh-builder means at 10,000 bars:

| Workload | Fused builder | Comparator |
|---|---:|---:|
| SMA | 117.850 us | TA-Lib 25.868 us |
| Asin | 269.63 us | TA-Lib 133.05 us |
| SMA -> Asin | 226.03 us | Ordinary builder 662.46 us |

The composed builder allocates 628.34 KB versus 869.09 KB for ordinary execution.
Both individual pilots also lose at 1,000 bars. Composition improves within its
same-process comparison, but the native acceptance gate still fails. No global
rollout is justified. Host timings vary substantially; retain confidence intervals
and do not compare different campaigns as controlled before/after measurements.

[Reports, raw measurements, JIT output and hashes](../benchmarks/results/eight-cpu-pilots/shared-sma-kernel/)


## AiDotNet.Tensors FP64 GPU pilot (2026-10-09)

The published 0.134.4 package supplies OpenCL device discovery, double buffers,
program compilation, kernel dispatch and readback. StockIndicators generates
the financial kernel, including warmup, exact certified SMA recurrence and Asin
domain/presence handling. SMA -> Asin runs in one device kernel without an
intermediate SMA array when only the dependent outputs are requested.

```csharp
var sma = new Sma(20);
var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
asin.Of(sma);
var builder = new StockIndicatorBuilder()
    .ConfigureSource(Bars.From(bars))
    .ConfigureIndicators(asin)
    .ConfigureExecution(IndicatorExecutionBackend.Gpu);
using var run = await builder.BuildAsync(cancellationToken);
// Required GPU mode throws for unsupported devices, inputs or configurations.
Console.WriteLine(builder.LastExecution!.DeviceName);
```

This pilot is opt-in. Auto stays CPU-backed. The net461 target retains CPU
execution and does not reference Tensors. Only plain array-backed typed SMA/Asin
graphs are eligible; callbacks, projected/live sources and other indicators keep
their ordinary CPU route in Auto mode and reject required GPU mode.

Adversarial review covered exact-grid certification before upload, block-start
recurrence, signed zero, domain masks, output ownership, concurrent argument
binding, bounded compiled-program retention, cancellation and error diagnostics.
All 140 focused tests passed with no skips, including actual execution on a
Radeon RX 5500 XT (OpenCL name `gfx1012:xnack-`). SMA windows were compared bitwise
with an independent rational reference; Asin retained its 4e-15 relative budget.
Release builds passed on net10.0, net8.0 and net461; existing Framework dependency
warnings remain. Other devices/drivers have not been qualified by this run.

Exploratory BenchmarkDotNet run: 36 valid cases, five measurement iterations,
three warmups, sizes 1k/10k/100k/1m, required GPU execution checked in every run.
An unrelated Tensors testhost was active, so these are not isolated performance
acceptance results. First GPU setup took 1426.735 ms including initialization,
JIT and program preparation; driver disk caches were not cleared. Warm timings
include fresh ownership, validation, upload, device outputs and readback.

| One million bars | CPU builder | GPU builder | TA-Lib native |
|---|---:|---:|---:|
| Asin | 40.931 ms | 41.432 ms | 13.444 ms |
| SMA | 34.782 ms | 42.736 ms | 3.300 ms |
| SMA -> Asin | 35.856 ms | 47.444 ms | 15.850 ms |

No measured size establishes a GPU win. GPU managed allocation at 1m bars is
about 68.7 MiB for Asin/composition versus native Asin's 7.6 MiB; builder history
ownership and fresh buffer/transfer costs remain candidates for optimization.
These measurements do not isolate the cost of device arithmetic. A persistent
device graph or reusable execution session would change the API/ownership scope
and is not silently included in this batch.

[Raw measurements, intervals, host details, logs and source hashes](../benchmarks/results/eight-cpu-pilots/tensors-gpu/)


### Allocation and transfer follow-up

GPU execution now reuses a bounded internal workspace (at most 40 MiB of host
scratch plus device buffers). Size changes dispose the old workspace; oversized
runs use transient storage. Failures discard cached buffers. No published arrays
or retained history are pooled. Direct Asin presence flags are written during
input validation; composed flags skip device allocation/readback when finite
input bounds prove all means are in-domain. Other composed runs retain the
device mask. Output arrays skip redundant zeroing and are fully initialized
before publication. Large fused CPU/GPU histories use one owned array rather
than hundreds of separately allocated chunks, preserving existing chunk views.

All 145 focused tests passed with no skips. New regressions cover alternating
sizes, graph shapes and domains, stale-buffer exposure, post-disposal snapshots,
contiguous-history indexing, chunk views and warmup slices. Release builds passed
on all three target frameworks. All 36 benchmark cases completed. An unrelated
Tensors parity testhost started during measurement; timings remain exploratory
and are not a controlled before/after speedup claim.

| One million bars | CPU builder | GPU builder | TA-Lib native |
|---|---:|---:|---:|
| Asin | 25.872 ms | 36.717 ms | 10.232 ms |
| SMA | 18.006 ms | 37.371 ms | 2.710 ms |
| SMA -> Asin | 21.299 ms | 22.481 ms | 12.033 ms |

GPU managed allocation for Asin/composition is now about 61.0 MiB per fresh
1m-bar run; the former temporary 7.6 MiB close array is retained internally
instead of reallocated each call. Composition needs one output-value readback
on this in-domain workload, with no presence-mask readback. The native
performance gate still fails, so automatic GPU selection remains disabled.

[Follow-up reports, raw measurements, logs and hashes](../benchmarks/results/eight-cpu-pilots/tensors-gpu-reuse/)
