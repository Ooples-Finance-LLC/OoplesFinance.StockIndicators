# Eight CPU pilot measurements

Local measurements on an AMD Ryzen 9 3950X, Windows 11 x64, .NET 10.0.12, BenchmarkDotNet 0.15.8. See [machine-readable measurements](measurements.json) for both sizes, actual retained sample counts, standard deviations, allocation measurements, and source hashes. See the [implementation blueprint](../../../docs/cpu-kernel-blueprint.md) for contracts and reproduction.

The baseline is `f720f254`. Ordinary pairs were measured serially with matching settings before and after: 16 invocations, eight warmups, five measured iterations. Trady short-day uses one invocation and three warmups/measurements; its unchanged competitor arm is reused from the original local baseline to avoid another 16-minute campaign. That slow baseline overlapped some implementation builds/tests and should be treated as an order-of-magnitude comparison. Other baseline measurements were repeated after implementation work stopped.

Times below are milliseconds per 10,000 bars. **Batch ratio = competitor / reusable batch**; above 1 favors the reusable kernel. The owning public API and caller-owned kernels are separate workloads. Asin and SMA owning routes were not changed; fluctuations there are measurement noise, not claimed improvements.

| Pair | Before public | After public | Competitor | CPU batch | CPU stream | Batch ratio |
|---|---:|---:|---:|---:|---:|---:|
| QuanTAlib.Jma | 299.282 | 12.386 | 1.980 | 12.374 | 12.268 | 0.16x |
| QuanTAlib.Atr | 14.465 | 1.797 | 0.472 | 0.114 | 0.222 | 4.13x |
| Skender.GetRollingPivots | 45.822 | 8.353 | 15.304 | 5.711 | 6.238 | 2.68x |
| Skender.GetFractal | 1.249 | 0.802 | 6.002 | 0.444 | 0.434 | 13.51x |
| TaLib.Candles.RickshawMan | 2.772 | 2.519 | 0.775 | 0.836 | 1.009 | 0.93x |
| TaLib.Functions.Asin | 2.133 | 2.295 | 0.219 | 0.145 | 0.148 | 1.51x |
| Trady.Candlestick.BullishShortDay | 4.718 | 3.385 | 92,723.128 | 0.920 | 0.993 | 100738.56x |
| Trady.Indicator.SimpleMovingAverage | 2.012 | 1.842 | 10.352 | 0.525 | 0.497 | 19.71x |

The dedicated allocation tests measured **zero bytes** for all eight reusable kernels on the 10,000-bar benchmark fixtures, including reset. In-process BenchmarkDotNet reports small harness allocations even for these zero-allocation paths; the raw values are retained rather than rewritten to zero. Extreme exponent spans can still allocate through exact fallback, and .NET Framework uses Jurik's fallback implementation.

Jurik is substantially faster and allocates far less than its original implementation, but **does not beat QuanTAlib**. Its exact per-stage contract requires more work than QuanTAlib's native double recurrence. Further work should reduce conversion/state-copy costs and expand certified fused stages; any approximate mode needs a separately specified error contract. Rickshaw also remains slightly slower than TA-Lib in this sample; further work should target exact threshold and rolling-window bookkeeping. A rounded-threshold shortcut was tested and discarded because it regressed performance. This PR does not claim eight universal wins or introduce an approximate default. Close timing differences, especially small fixtures, need a dedicated runner and longer campaigns before being treated as reliable rankings.

Verification: 158 focused contract tests passed, including independent references, mutation-oracle checks, wide/subnormal cases, lifecycle, delayed confirmation, and allocation gates. Four performance-evidence verifier tests passed. The library builds for net10.0, net8.0, and net461; runtime contract tests and timings here use net10.0. The new on-demand workflow builds once and runs the eight pairs on separate CI runners; normal PR contract discovery includes the new tests.
