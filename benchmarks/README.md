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
dotnet build -c Release -p:AssemblyName=OoplesFinance.StockIndicators.Original -p:TargetFramework=net10.0 benchmarks/.baseline/src/OoplesFinance.StockIndicators.csproj
dotnet run -c Release --project benchmarks/OoplesFinance.StockIndicators.Benchmarks/OoplesFinance.StockIndicators.Benchmarks.csproj -- --filter *IndicatorBenchmarks*
```

Notes:
- The baseline worktree lives in `benchmarks/.baseline` and is ignored by git.  
- Benchmark categories are used to compare optimized vs baseline per indicator.
- Results are emitted to `BenchmarkDotNet.Artifacts/results` as Markdown and CSV.
- To override counts or lengths without editing code, set `OOPLES_BENCHMARK_COUNTS` or `OOPLES_BENCHMARK_LENGTHS` (comma/space separated).
- To change the dataset size in code, edit `Count` in `benchmarks/OoplesFinance.StockIndicators.Benchmarks/IndicatorBenchmarks.cs`.
