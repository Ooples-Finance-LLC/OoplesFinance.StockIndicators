# Reusable CPU indicator blueprint

The eight pilots retain the library's numerical definitions while separating arithmetic from builder, result-object, and buffer ownership costs. The [measured results](../benchmarks/results/eight-cpu-pilots/README.md) include both wins and remaining gaps. Existing public APIs remain available. `IndicatorKernels` provides an explicit reusable route for applications that already own their input and output storage.

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

All input OHLCV values must be finite. Short output buffers and nonfinite batch inputs are rejected before consuming any bars. A numerical overflow can leave an earlier batch prefix committed; callers should reset before retrying a failed batch. Output slots beyond the requested rows are untouched. Undefined results use `double.NaN`, with the layouts below.

| Factory | Output layout and timing | Reusable technique |
|---|---|---|
| `Asin()` | Arcsine of close; NaN outside [-1,1] | Direct arithmetic into caller storage |
| `ScaledTrueRange(divisor)` | Unsmoothed range divided by divisor, including first bar | Ordered extrema, certified exact subtraction and division, compact exact fallback |
| `RollingPivots(period, offset, style)` | PP, S1, S2, S3, S4, R1, R2, R3, R4; unavailable levels are NaN | Monotonic extrema, retained history, exact integer-weighted formulas |
| `Fractal(leftSpan, rightSpan, useClose)` | Bear, Bull confirmed **for the center rightSpan bars ago** | Monotonic queues retain exact ties; retrospective placement belongs to the caller |
| `RickshawMan(dojiPeriod, nearPeriod)` | 100 or zero | Reserved exact-range histories and rolling threshold sums |
| `BullishShortBody(period, percentile)` | One or zero | Reserved percentile window; stop rank counting once its outcome is certain |
| `Sma(period)` | Zero before a complete window, then the exact mean | Reserved ring and compact exact rolling sum |
| `Jurik(period, phase, volatilityPeriod)` | One value per observation, preserving rounded recurrence stages | Monotonic extrema, reserved histories, fixed-width exact dyadics with wide fallback |

Scaled true range matches the pinned QuanTAlib ATR bar route, which is not a conventional smoothed ATR. Fractals expose confirmation time explicitly; they cannot publish an unconfirmed center as a live value. The full competitor catalog documents these semantic differences and independently verifies each side's formula.

## Numerical and allocation contract

No approximate mode or error tolerance is introduced. Pivot formulas retain exact decimal-rational coefficients and single final rounding. Candle thresholds retain exact comparisons. Jurik retains exact differences, binary64 coefficient values, per-stage rounding, and its extended upper exponent behavior.

True range illustrates a reusable certificate: hardware division is used only when an error-free subtraction proves its numerator is exact. Otherwise compact exact arithmetic handles the complete ratio. No nearby distinct values are treated as ties.

On .NET 8 and .NET 10, Jurik uses correctly rounded fused multiply-add when both operands are exactly representable doubles. A blend additionally requires an error-free TwoSum certificate for its subtraction. Other ordinary stages use signed 128-bit dyadics while their magnitude fits 127 bits. Before every shift, addition, product, or ratio it checks whether the exact operation fits. Otherwise it delegates to arbitrary precision. Recurrence rounding is ties-to-even, including subnormal results. Wider stages use the established extended-exponent fallback. .NET Framework 4.6.1 uses the exact fallback throughout.

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

Use a clean artifact directory for each run. The verifier requires each selected arm at both 1,000 and 10,000 bars, real measurements, allocation diagnostics, and full untruncated pair identifiers. A successful process exit alone is insufficient.

Ordinary pairs use 16 invocations, eight warmups, and five measured iterations to reduce tier-transition distortion. Trady short-day uses one invocation with three warmups and three measured iterations because one 10,000-bar call can take over a minute. Setup independently checks a bounded fixture and checks the complete kernel trajectory against the public route. Comprehensive competitor formula/isolation checks remain in the existing correctness campaign.
