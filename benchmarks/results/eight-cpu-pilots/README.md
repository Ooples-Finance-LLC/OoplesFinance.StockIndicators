# Eight CPU pilot measurements

All eight reusable batch kernels and all eight reusable streaming kernels have lower measured means than their paired competitors at both 1,000 and 10,000 bars. These are workload-specific local results, not universal rankings. Existing owning APIs are reported separately.

AMD Ryzen 9 3950X, Windows 11 x64, .NET 10.0.12, BenchmarkDotNet 0.15.8. The [machine-readable results](measurements.json) retain both sizes, sample counts, standard deviations, raw allocation diagnostics, and normalized source hashes. See the [blueprint](../../../docs/cpu-kernel-blueprint.md) for contracts and reproduction.

Times below are milliseconds per 10,000 bars. Ratios are competitor time divided by kernel time; above 1 favors our kernel.

| Pair | Previous public | Current public | Competitor | CPU batch | CPU stream | Batch win | Stream win |
|---|---:|---:|---:|---:|---:|---:|---:|
| QuanTAlib.Jma | 299.282 | 2.028 | 2.641 | 1.775 | 1.741 | 1.49x | 1.52x |
| QuanTAlib.Atr | 14.465 | 2.233 | 0.663 | 0.158 | 0.161 | 4.20x | 4.13x |
| Skender.GetRollingPivots | 45.822 | 7.683 | 12.189 | 5.901 | 6.167 | 2.07x | 1.98x |
| Skender.GetFractal | 1.249 | 1.190 | 7.989 | 0.524 | 0.535 | 15.24x | 14.93x |
| TaLib.Candles.RickshawMan | 2.772 | 3.568 | 1.137 | 0.751 | 0.755 | 1.51x | 1.51x |
| TaLib.Functions.Asin | 2.133 | 2.806 | 0.289 | 0.206 | 0.258 | 1.40x | 1.12x |
| Trady.Candlestick.BullishShortDay | 4.718 | 3.385 | 92,723.128 | 0.920 | 0.993 | 100738.56x | 93411.26x |
| Trady.Indicator.SimpleMovingAverage | 2.012 | 2.210 | 12.696 | 0.616 | 0.628 | 20.62x | 20.22x |

Seven ordinary pairs were measured serially after implementation, with automatic invocation calibration targeting 100 ms, eight warmups, and five measured iterations. Some retained sample counts are four after BenchmarkDotNet outlier filtering. Historical public baselines used 16 invocations with the same warmup/measurement counts; their different timing protocol is retained explicitly rather than presented as a matched new campaign.

The unchanged Trady short-day kernel and competitor measurements are reused from the previous campaign (one invocation, three warmups, three measurements). That very slow competitor campaign overlapped some builds/tests; treat its enormous ratio as an order-of-magnitude result. Asin and SMA owning implementations are unchanged; timing fluctuations there are not claimed implementation improvements. Close rankings need a dedicated runner and repeated measurements on the target hardware.

Dedicated thread-allocation tests require **zero steady-state bytes for all eight kernels**, including reset, on ordinary and actual 10,000-bar benchmark fixtures. BenchmarkDotNet's small in-process harness allocations remain unmodified in the raw evidence. Setup, owning output APIs, and extreme-input arbitrary-precision fallback may allocate.

PerfView identified Jurik's exact-number conversion/arithmetic and Rickshaw's rolling threshold state as the two losing paths. Jurik now retains rounded doubles, certifies fused expansion rounding, isolates wide fallback, and eliminates exact identity powers. Rickshaw uses exact integer-grid windows with lossless transfer to the general state on a grid miss. Neither optimization introduces a tolerance or an approximate default. See the blueprint for the profiler evidence and the correctness arguments.

Verification: 168 focused contract tests, including independent references, extreme exponents, exact threshold fixtures, preview/reset and allocation gates; four performance-evidence verifier tests. Library builds target net10.0, net8.0, and net461; runtime tests and these timings use net10.0. The manual performance workflow runs eight pairs in parallel after one build. Normal PR contract discovery includes the new tests.
