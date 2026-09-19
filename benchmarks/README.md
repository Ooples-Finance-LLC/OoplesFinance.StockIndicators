# Benchmarks

This project uses BenchmarkDotNet to measure indicator performance.

Run optimized benchmarks:
```
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.Benchmarks/OoplesFinance.StockIndicators.Benchmarks.csproj
```

Run a subset:
```
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.Benchmarks/OoplesFinance.StockIndicators.Benchmarks.csproj -- --filter *SMA*
```

Run streaming fanout throughput/latency:
```
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.Benchmarks/OoplesFinance.StockIndicators.Benchmarks.csproj -- --streaming-perf --ticks 100000
```
Use streaming fanout flags:
```
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.Benchmarks/OoplesFinance.StockIndicators.Benchmarks.csproj -- --streaming-perf --include-outputs
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.Benchmarks/OoplesFinance.StockIndicators.Benchmarks.csproj -- --streaming-perf --extended
```
Note: the default streaming perf run uses the core 10 indicators. `--extended` adds additional stateful indicators.

Compare optimized vs baseline (master by default):
```
.\benchmarks\setup-baseline.ps1 -Ref master
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.Benchmarks/OoplesFinance.StockIndicators.Benchmarks.csproj -- --filter *IndicatorBenchmarks*
```

Setup script options:
```
.\benchmarks\setup-baseline.ps1 -Ref <branch>     # Compare against a specific branch/tag
.\benchmarks\setup-baseline.ps1 -Framework net9.0 # Use a different target framework
.\benchmarks\setup-baseline.ps1 -NoBuild          # Only create worktree, skip build
```

Notes:
- The setup script creates the baseline worktree AND builds it with the correct assembly name.
- The baseline worktree lives in `benchmarks/.baseline` and is ignored by git.
- Benchmark categories are used to compare optimized vs baseline per indicator.
- Results are emitted to `BenchmarkDotNet.Artifacts/results` as Markdown and CSV.
- To override counts or lengths without editing code, set `OOPLES_BENCHMARK_COUNTS` or `OOPLES_BENCHMARK_LENGTHS` (comma/space separated).
- To change the dataset size in code, edit `Count` in `benchmarks/OoplesFinance.StockIndicators.Benchmarks/IndicatorBenchmarks.cs`.


---

## Head-to-head against other libraries

A second project, `benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks`, measures this library
against Skender.Stock.Indicators 2.7.3, TALib.NETCore 0.5.0, Trady.Analysis 3.2.8 and QuanTAlib 1.0.0 over
seven indicators. Nothing it references ships: the project is never packed and the library takes no reference
on any competitor package.

It has three commands that are not timings, and all three exist so a timing cannot be read out of context.

What each library ships, so a missing row is never mistaken for a slow one:
```
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks.csproj -- --coverage
```

What each library computes, so the timings are known to be over the same arithmetic:
```
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks.csproj -- --verify
```

Where a measured allocation actually goes, so the adapter is not reported as the indicator:
```
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks.csproj -- --alloc
```

The timings themselves:
```
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks.csproj -- --filter *BatchBenchmarks*
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks.csproj -- --filter *IncrementalBenchmarks*
```

### Reading the results honestly

Three things must be said next to the numbers, because the numbers do not say them.

**The incremental comparison is not close, and it is not a like-for-like race.** Skender, TA-Lib and Trady ship
no incremental path at all, so their column is a full recompute of the whole history per bar and grows with it;
only QuanTAlib has a real incremental arm. That is a genuine shipped-feature difference, which is what
`--coverage` is for, not a trick of the measurement.

**Our Stochastic row does less work than TA-Lib's and Skender's.** `--verify` shows a 17.26% spread, and its
control fixture names the reason: our `%K` is the raw fast %K, theirs is a %K smoothed over three bars. The
other six indicators agree to within 1.4e-11.

**There are two Ooples v2 arms, and the difference between them is the adapter.** `Ooples v2 builder` is
handed a `StockData`, which copies every column into a `List<double>`. `Ooples v2 columns` is handed the
arrays through `IndicatorDataSource.FromColumns`, the same pre-built input every competitor receives from the
`GlobalSetup`. At 10,000 bars `--alloc` puts the first at 1,049,184 B and the second at 8,536 B - and the
second is 8,536 B at 1,000 bars too, because nothing left in that path grows with the history. TA-Lib's
80,056 B is the one output array its caller has to allocate; its benchmark row reads 32 B only because that
array is reused from a `GlobalSetup`.

**Most of the remaining v2-builder allocation is not indicator work.** Every competitor gets its input pre-built in a
`GlobalSetup`, but the v1 and v2 arms must build a fresh `StockData` inside the measured method, because the
batch API writes results back into the instance it is handed. `--alloc` splits it: at 10,000 bars the v2 EMA
arm allocates 1,208,528 B, of which `NewStockData()` is 961,168 B. The adapter is charged to this library and
to nobody else.