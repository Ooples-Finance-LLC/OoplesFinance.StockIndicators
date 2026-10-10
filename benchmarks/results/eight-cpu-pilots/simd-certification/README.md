# SMA certification and ingestion follow-up

All measurements use the public LatestOnly `BuildAsync()` benchmark on 100,000
bars. `baseline-before` and `baseline-after` use the saved production binary from
commit `9637893a`. The first candidate has SIMD certification, compact bounded
SMA, and Asin's duplicate-copy removal. `final-matched` also has SIMD price-field
validation. `manifest.json` identifies the binaries and validation performed.

| Builder workload | Baseline before / after | First candidate / repeat | Final candidate |
|---|---:|---:|---:|
| Asin | 1.262 / 1.249 ms | 1.177 / 1.257 ms | 1.186 ms |
| Grid SMA | 0.700 / 0.730 ms | 0.557 / 0.566 ms | 0.544 ms |
| Decimal SMA | 0.836 / 0.834 ms | 0.616 / 0.595 ms | 0.569 ms |

The final matched TALib payload measures 1.365, 0.565 and 0.574 ms respectively.
Decimal SMA's confidence intervals overlap: treat it as parity, not a proven win.
Raw TALib still does less work and remains faster. Final timings use 500 ms target
iterations; earlier jobs use a 100 ms minimum. All use six warmups/ten iterations.
This is one host with sequential jobs; the improvement does not qualify a global
rollout or establish dominance over every input size and competitor. Decimal
numerical semantics remain different.

The JSON reports preserve samples, statistics and managed allocation; logs are
compressed. The 163 affected tests and 86 hardware-disabled kernel/validation
tests passed. Net8 library and net10 benchmark Release builds passed. Compressed
JIT listings show SIMD instructions in the certificate and validation loops.

Reproduce final timing with the existing benchmark DLL and filters for
`PilotCostBoundaryBenchmarks.TalibValues`, `TalibInPlaceLatestOnlyPayload`, and
`OoplesLatestOnlyBuilder`, passing `--warmupCount 6 --iterationCount 10
--iterationTime 500 --exporters json`. `compare.ps1` records the baseline-bracketing
procedure; its binary snapshots/local paths must be supplied before reuse. Do
not run builds or profiles concurrently with timing.
