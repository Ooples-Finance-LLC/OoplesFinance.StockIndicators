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
