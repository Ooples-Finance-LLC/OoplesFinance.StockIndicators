# Reusable CPU indicator blueprint

The kernel timings below are diagnostic evidence, not proof of builder performance or superiority over the strongest competitor. The [public builder suite](../benchmarks/results/eight-cpu-pilots/builder-comparisons.md) includes the complete builder lifecycle and all eligible competitors from the inventory for these eight formulas.

## Acceptance criteria for subsequent indicators

- Measure the normal public builder path, including configuration, execution, materialized results, and disposal. A faster kernel is useful evidence, but does not establish a faster library API.
- Inventory every matching competitor before timing. Preserve losing results and use the fastest eligible native route as the target; do not select a slower library to claim a win.
- Run all candidates for a family on the same runner with identical inputs and parameters. Record differences in numerical definitions, warmup, result shape, and retrospective placement. A similarly named but different formula cannot establish a win for the requested indicator.
- Keep input preparation and correctness normalization outside timing for both sides. Include required native setup and output ownership; never add unnecessary adapter work to the competitor.
- Verify independent formulas, output presence, and lifecycle behavior before profiling the complete builder workload. Inspect its allocations and CPU profile before deciding whether to optimize arithmetic, graph setup, validation, or copies.
- Publish sample counts, allocations, confidence intervals, source revision, and every included route. Treat overlapping intervals as inconclusive and restrict claims to the measured workload and versions.

The eight pilots retain the library's numerical definitions while separating arithmetic from builder, result-object, and buffer ownership costs. The [measured results](../benchmarks/results/eight-cpu-pilots/README.md) distinguish owning batch, reusable batch, and supported streaming comparisons. The earlier claim of eight batch and streaming wins used allocating correctness adapters and is superseded. Existing public APIs remain available. `IndicatorKernels` provides an explicit reusable route for applications that already own their input and output storage.

```csharp
using OoplesFinance.StockIndicators.Indicators;

var kernel = IndicatorKernels.Jurik(period: 20);
var output = new double[bars.Length * kernel.OutputCount]; // setup
kernel.Process(bars, output);                            // batch
kernel.Reset();
Span<double> next = stackalloc double[1];
kernel.Preview(bars[0], next); // does not consume the observation
kernel.Update(bars[0], next);  // commits it
```

`Process` continues the current state, so chunking a source has the same result as processing it in one batch. Reset before an independent batch. Instances are owned by one consumer and are not thread safe. Reset retains window capacity. These factories reserve their windows up front; unlike snapshot APIs, very large requested windows require correspondingly large setup storage.

Bar kernels require all OHLCV input values to be finite. The close-only Asin overload follows the IEEE contract described below. Short output buffers and nonfinite batch inputs are rejected before consuming any bars. A numerical overflow can leave an earlier batch prefix committed; callers should reset before retrying a failed batch. Output slots beyond the requested rows are untouched. Undefined results use `double.NaN`, with the layouts below.

| Factory | Output layout and timing | Reusable technique |
|---|---|---|
| `Asin()` / `Asin(closes, output)` | Arcsine of close; NaN outside [-1,1] | Bar kernel or direct close-span computation; the latter matches TA-Lib's input shape |
| `ScaledTrueRange(divisor)` | Unsmoothed range divided by divisor, including first bar | Ordered extrema, certified exact subtraction and division, compact exact fallback |
| `RollingPivots(period, offset, style)` | PP, S1, S2, S3, S4, R1, R2, R3, R4; unavailable levels are NaN | Monotonic extrema, retained history, exact integer-weighted formulas |
| `Fractal(leftSpan, rightSpan, useClose)` | Bear, Bull confirmed **for the center rightSpan bars ago** | Monotonic queues retain exact ties; retrospective placement belongs to the caller |
| `RickshawMan(dojiPeriod, nearPeriod)` | 100 or zero | Exact integer-grid rings and Int128 thresholds; lossless general-state fallback |
| `BullishShortBody(period, percentile)` | One or zero | Reserved percentile window; stop rank counting once its outcome is certain |
| `Sma(period)` | Zero before a complete window, then the exact mean | Reserved ring and compact exact rolling sum |
| `Jurik(period, phase, volatilityPeriod)` | One value per observation, preserving rounded recurrence stages | Monotonic extrema, certified fused recurrence, lazy exact dyadics and wide fallback |

Scaled true range matches the pinned QuanTAlib ATR bar route, which is not a conventional smoothed ATR. Fractals expose confirmation time explicitly; they cannot publish an unconfirmed center as a live value. The full competitor catalog documents these semantic differences and independently verifies each side's formula.

## Numerical and allocation contract

No approximate mode or error tolerance is introduced. Pivot formulas retain exact decimal-rational coefficients and single final rounding. Candle thresholds retain exact comparisons. Jurik retains exact differences, binary64 coefficient values, per-stage rounding, and its extended upper exponent behavior.

True range illustrates a reusable certificate: hardware division is used only when an error-free subtraction proves its numerator is exact. Otherwise compact exact arithmetic handles the complete ratio. No nearby distinct values are treated as ties.

On .NET 8 and .NET 10, Jurik retains rounded stages as doubles, avoiding repeated integer decoding. Error-free TwoSum and FMA residuals express exact differences and products as short expansions. Each residual-error bound is rounded outward with `BitIncrement`; the fast result is accepted only when the bound lies strictly inside both neighboring half-ULP gaps. Exact midpoint ambiguity, unsafe product exponents, overflow and subnormal boundaries fall back to signed 128-bit dyadics or arbitrary precision. Product exponent guards ensure FMA residuals do not silently underflow. No tolerance is used. The ordinary path is separate from wide arithmetic so it avoids that path's large temporary state; powers with an exact exponent of one use the identity result. Wider stages retain the established extended-exponent rounding. .NET Framework 4.6.1 uses exact fallback arithmetic.

Rickshaw chooses an exact binary grid with exponent headroom, accepts only lossless conversions whose signed magnitudes are below 2^62, and stores range rings as integers. Range differences fit signed 64 bits; period-weighted thresholds and rolling sums fit Int128 even at the largest accepted integer periods. A grid miss transfers both retained histories, in order and without floating-point rounding, to the general exact state. A preview can evaluate this fallback without switching committed state. Reset retains capacity. These guarantees are covered by threshold fixtures and mature-window transition tests, including extreme/subnormal and inverted candles.

Zero allocation is a **verified steady-state workload property**, not a promise for every finite binary64 input. Compact sums can also fall back for very wide exponent spans. The tests require zero bytes on ordinary fixtures and the actual 10,000-bar benchmark fixtures for all eight kernels on .NET 10. Setup, newly owned results from the public APIs, and arbitrary-precision fallback are outside that guarantee. BenchmarkDotNet's in-process memory diagnostics can include harness allocations; the dedicated thread-allocation gates isolate kernel work.

## Template for the next indicator

1. Identify the numerical contract, every rounding boundary, warmup, missing-value representation, and when an output becomes knowable. Inspect the pinned competitor implementation and retain its independent reference.
2. Reuse an existing arithmetic state where possible. Replace repeated window scans or temporary objects with bounded storage. Preserve exact ties and overflow behavior. A floating-point approximation cannot certify its own correctness.
3. Add a factory with explicit output ordering. Use the same recurrence for `Process`, `Update`, and `Preview`; only commit history after computing the prospective result. Preserve existing public routes and their observable error handling.
4. Test independent mathematical references, boundaries, cancellation, overflow, exact ties, delayed outputs, repeated previews with different candidate bars, reset/reuse, chunked batches, invalid-input atomicity, and actual allocations. Include affected shared callers.
5. Measure the public route, reusable batch, reusable streaming, and the competitor separately. Verify timed outputs outside timing. Publish losses as well as wins, including numerical-contract differences. Do not label a faster new buffer API as a speedup of an unchanged owning API.

GPU dependencies and kernels are outside these CPU pilots. A later crossover study should use the same fixtures, numerical contracts, and result layouts, including transfer and launch costs for single-series and multi-series workloads.

## Competitor source findings

The sample was selected deterministically from the complete 10,000-bar report with `random.Random(20261008)`, taking two indicators from each library's sorted rows. The measured package versions remain QuanTAlib 1.0.0, Skender 2.7.3, TA-Lib.NETCore 0.5.0, and Trady 3.2.8.

| Pair | Source finding and implication |
|---|---|
| QuanTAlib JMA | Double recurrence state and circular buffers avoid the large exact integers formerly used at every Ooples stage. Fixed-width exact arithmetic removes ordinary allocations, but does not make its arithmetic cost equivalent to native double operations. |
| QuanTAlib ATR | Lightweight double state; the pinned bar route emits scaled range. Removing builder overhead and using a compact exact difference addresses separate costs. |
| Skender rolling pivots | Rescans each preceding window. Monotonic extrema improve the algorithm as the period grows, independently of buffer ownership. |
| Skender fractal | Scans neighboring bars. Ooples already used monotonic extrema; replacing linked nodes with array storage targets allocation rather than inventing a new formula. |
| TA-Lib Asin | Direct input/output spans and a transcendental call per value. The reusable Ooples route removes builder and result materialization overhead. |
| TA-Lib Rickshaw Man | Rolling range totals and direct output spans. Reserved Ooples windows retain exact threshold comparisons without repeated setup. |
| Trady bullish short day | Inspected source builds percentile calculations per index. Its enormous cumulative allocation is a pathological baseline, not evidence that every competitor has this problem. |
| Trady SMA | Inspected source uses `Skip/Take/Average` with decimal inputs. A retained rolling exact sum avoids repeatedly enumerating windows. |

Source identities: QuanTAlib `a2af99711d22a2833af2eb18054f0049a3fb0224`, Skender `fd739aa4b592f68d5a68d14c602319c027bb0538`, and TA-Lib.NETCore `0bc2086a6cafc5b4c17fd398e0c16895e4daf92c` match package repository metadata. Trady source was inspected at `08de02a68dd69fce3f7fc1dd916419117da7457e`; its package has no source commit metadata, so that source identity is not authenticated against the binary. Timings and parity checks use the actual pinned package binaries. None of these sampled implementations uses a GPU.

## Repeatable verification and measurement

The regular competitor workflow discovers `CpuKernelTests` and distributes them with the existing contract tests across up to 20 CI shards. Performance runs are on demand through **CPU kernel pilot performance**, with one build and eight parallel pair jobs. They do not add a quadratic Trady performance campaign to every PR.

For a local paired run in PowerShell:

```powershell
dotnet build benchmarks/OoplesFinance.StockIndicators.CompetitorTests -c Release -p:GeneratePackageOnBuild=false
dotnet test benchmarks/OoplesFinance.StockIndicators.CompetitorTests -c Release --no-build --filter FullyQualifiedName~CpuKernelTests
$env:COMPARISON_PAIR = 'QuanTAlib.Jma'
dotnet benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll --filter '*CpuKernelBenchmarks*' --exporters json --artifacts performance
python scripts/verify-cpu-kernel-performance.py performance $env:COMPARISON_PAIR
```

Use a clean artifact directory for each run. The verifier requires the supported matched arms at both 1,000 and 10,000 bars, real measurements, allocation diagnostics, and full untruncated pair identifiers. It rejects legacy adapter-based methods and unsupported substitutions. A successful process exit alone is insufficient.

Ordinary pairs automatically calibrate invocation counts with a 100 ms iteration target, eight warmups, and five measured iterations. This replaces the original fixed 16-invocation configuration, which produced very short, noisy measurements for fast kernels. Trady short-day uses one invocation with three warmups and three measured iterations because one 10,000-bar call can take over a minute. Setup checks independent formulas on a bounded fixture, direct competitor outputs against the complete native reference, and complete kernel/owning trajectories against the public route. Successful validation is cached only after completion within the benchmark process; it does not cache timed results. Comprehensive competitor formula/isolation checks remain in the existing correctness campaign.


## PerfView findings behind the final optimizations

Microsoft-signed PerfView 3.2.8 collected four elevated native ETW traces for the initial PR implementation: Jurik and Rickshaw, each with Ooples batch and its competitor. Each process ran the verified 10,000-bar workload for two seconds of warmup and 15 seconds of measurement. Exports were filtered to the measured dotnet process and checked against its logged PID. The percentages below are **exclusive** samples (inclusive percentages must not be added).

| Trace | Selected exclusive samples | Resulting change |
|---|---|---|
| Jurik Ooples | Next 24.37%; dyadic Add 15.49%, SumProduct 9.50%, Multiply 8.91%, Blend 6.68%, Subtract 5.45%, RoundedDouble 4.96% | Keep rounded values as doubles; certify fused expansion results; move wide temporaries off the common path |
| Jurik QuanTAlib | Calculation 35.61%; several native frames unresolved | Compare the actual paired runtime; do not attribute unresolved frames to guessed functions |
| Rickshaw Ooples | Update 33.16%; Matches 26.78%; accumulator Add 8.39%; AddWeighted 7.30% | Exact integer-grid rings and direct Int128 predicates |
| Rickshaw TA-Lib | CandleRange 56.05%; comparison adapter 28.59%; dictionary lookup 7.11% | Preserve the competitor workload, including its existing API/adapter costs |

A subsequent managed sampled-thread trace of an intermediate Jurik implementation identified remaining dispatch/temporary-state cost. Separating the wide path and certifying nonrepresentable differences removed it without changing stage rounding. Those traces describe the historical adapter-based harness. Current acceptance uses the direct native BenchmarkDotNet measurements, not those profiler-instrumented timings. Whole ETW traces stay local because they include unrelated system processes.

The committed profiling harness reproduces one verified arm:

```powershell
# Run PerfView from an elevated shell; paths are examples.
PerfView.exe /AcceptEula /NoGui /NoNGenRundown /DataFile:Jma.etl run dotnet.exe benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll --profile-cpu-pilot QuanTAlib.Jma OoplesOwnedBatch 15
PerfView.exe /AcceptEula /NoGui UserCommand SaveCPUStacksAsCsv Jma.etl.zip dotnet 10 GreatestMSec
```

Use `CompetitorOwnedBatch` for the paired arm and `TaLib.Candles.RickshawMan` for the candle pair. Verify the exported process PID against `PROFILE START`; selecting only the last process named dotnet can accidentally select a concurrent build. The profiling harness also supports `OoplesReusableBatch` and `CompetitorReusableBatch` for supported TA-Lib pairs. Streaming timings use the BenchmarkDotNet lifecycle described below.


## Matched benchmark boundaries and adversarial review

The timed methods in `CpuKernelBenchmarks` call `CpuNativeWorkload` directly. None calls `ComparisonPair.Competitor`, `ComparisonSeries`, reference arithmetic, reflection, nullable projection, presence-mask construction, or dictionary packaging. Those belong exclusively to setup and verification.

| Workload | Ooples | Competitor | Available pairs |
|---|---|---|---|
| Owning batch | Fresh kernel and owned flat output; Asin directly allocates its close-span output | Fresh native state where needed and the native owned output collection/buffer | All eight |
| Reusable batch | Prepared buffers and reset kernel; direct close-span Asin | Direct TA-Lib span call into prepared native buffers | Asin and Rickshaw |
| Streaming | Fresh state/storage outside timing, then every incremental update stored | Fresh QuanTAlib state/storage outside timing, then every incremental update stored | JMA and ATR |

Owning Ooples measurements compose the new kernel APIs; they are not the legacy builder route. Skender and Trady return eager native collections, which are returned directly without another `ToArray`. Tests assert materialization and independent ownership. Native result types retain their own costs: Skender/Trady include dated result objects, Rickshaw returns packed integers, and Ooples uses flat doubles. These API comparisons do not isolate identical machine-level arithmetic or promise identical precision.

Streaming uses a separate job with exactly one invocation per iteration, 64 independent sequences per invocation, and `OperationsPerInvoke=64`. The evidence verifier rejects calibrated multi-invocation streaming results. Iteration setup constructs both sides' state and output storage outside timing. Every result is retained. The reported operation is one complete 1,000- or 10,000-update sequence, not one update. This measures updates from fresh state, not an indefinitely warmed live stream. We do not use QuanTAlib JMA's incomplete history reset or label a batch-only API as streaming.

Input preparation is outside timing for both sides, including QuanTAlib TValue objects. Decimal-native libraries receive shared prices on a 1/1024 grid, exactly representable as both double and decimal. Period is 20, JMA phase is zero and volatility period is 10, Rickshaw periods are 10/5, and fractal left/right spans are 20/20. Owning fractal output is shifted to its center bar inside timing, matching the competitor's placement; incremental kernels retain confirmation-time placement. Owning warmup/null/NaN conventions and numerical rounding differences remain explicitly documented in the pair references.

The close-span Asin API follows IEEE `Math.Asin` behavior, including NaN for infinities and NaN inputs, matching TA-Lib. It validates buffer length before writes, allows exact in-place operation, rejects partial overlap, and leaves extra output slots untouched. The original bar API additionally validates all OHLCV fields. The direct bar comparison lost to TA-Lib; the close-only API was explicitly approved to match TA-Lib's input shape. Both interfaces remain tested; changing the benchmark input contract must never be presented as speeding up the old bar API.

Adversarial review covered all PR production changes and the benchmark lifecycle. Resolved findings were asymmetric allocation/setup, timed correctness normalization, unsupported streaming substitutions, incomplete native reset, redundant native-result copying, decimal input rounding, fractal placement, validation-cache failure handling, and acceptance of old benchmark evidence. The review also checked exact-rounding guards, Rickshaw grid bounds/fallback transfer, monotonic-deque ties, preview/reset/chunking, overflow rejection, and result observability. Regression tests cover the affected behavior; this is an internal review, not an independent external audit.
