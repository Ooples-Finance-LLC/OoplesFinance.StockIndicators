# EMA allocation observations

The existing competitor allocation probe ran successfully on 2026-10-03 at source checkout `3f63d8db`. The harness was built using `BuildProjectReferences=false` and executed with `--alloc`. The loaded net10.0 library is byte-identical to the production DLL verified in package batch 751: SHA256 `87d88aeaa56f4f12eb44201689a87584a3d918c6aa02fa7a87962e4506d0a305`.

| Measured operation | 1,000 bars | 10,000 bars |
| --- | ---: | ---: |
| Copy five input arrays | 40,184 B | 400,184 B |
| Construct StockData | 49,024 B | 481,024 B |
| v2 EMA through copied StockData | 81,752 B | 729,752 B |
| v2 EMA through adopted columns | 81,752 B | 729,752 B |
| Adopted-column builder only | 6,824 B | 6,736 B |
| Adopted-column build and start | 81,552 B | 729,488 B |
| Warm array-pool rent and return | 0 B | 0 B |
| TA-Lib EMA with a newly allocated result buffer | 8,056 B | 80,056 B |

All five repeated adopted-column runs returned the same allocation count at each history size. These are warmed, current-thread allocation observations from `GC.GetAllocatedBytesForCurrentThread`, not elapsed-time measurements, total-process memory, or a complete competitor benchmark. The copied and adopted v2 paths allocated equally in this probe; no allocation advantage is claimed for adopted columns here. TA-Lib performs a narrower operation than constructing and running the v2 builder, so these counts do not isolate indicator arithmetic on equal terms.

The prior numerical comparison in [the upstream integration report](V2_UPSTREAM_747_COMPETITOR_COMPARISON.md) records formula and warmup differences. Full timing comparisons remain outstanding and must be run without the active heavy verification jobs. No production or benchmark implementation changed for this measurement.

Command after the successful harness build:

```text
dotnet benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll --alloc
```
