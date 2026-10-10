```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=15  IterationTime=250ms  LaunchCount=1
UnrollFactor=1  WarmupCount=8

```
| Method                | Bars  | PairId              | Mean       | Error      | StdDev     | Gen0    | Gen1    | Allocated |
|---------------------- |------ |-------------------- |-----------:|-----------:|-----------:|--------:|--------:|----------:|
| **OoplesBuilderBatch**    | **1000**  | **TaLib.Functions.Sma** |  **12.230 μs** |  **1.1853 μs** |  **1.1088 μs** |  **6.9897** |  **1.1649** |   **57.3 KB** |
| CompetitorNativeBatch | 1000  | TaLib.Functions.Sma |   2.620 μs |  0.1366 μs |  0.1278 μs |  0.9550 |  0.0193 |   7.87 KB |
| **OoplesBuilderBatch**    | **10000** | **TaLib.Functions.Sma** | **100.596 μs** | **18.2934 μs** | **17.1117 μs** | **66.3975** | **22.4316** | **549.93 KB** |
| CompetitorNativeBatch | 10000 | TaLib.Functions.Sma |  24.211 μs |  0.9824 μs |  0.8709 μs |  9.5040 |  1.5387 |  78.18 KB |
