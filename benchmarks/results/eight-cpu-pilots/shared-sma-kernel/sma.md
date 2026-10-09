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
| **OoplesBuilderBatch**    | **1000**  | **TaLib.Functions.Sma** |  **15.816 μs** |  **5.5718 μs** |  **5.2118 μs** |  **6.9526** |  **1.1298** |   **57.3 KB** |
| CompetitorNativeBatch | 1000  | TaLib.Functions.Sma |   2.536 μs |  0.1119 μs |  0.0874 μs |  0.9569 |  0.0188 |   7.87 KB |
| **OoplesBuilderBatch**    | **10000** | **TaLib.Functions.Sma** | **117.850 μs** | **17.2085 μs** | **16.0968 μs** | **66.5434** | **22.6433** | **549.93 KB** |
| CompetitorNativeBatch | 10000 | TaLib.Functions.Sma |  25.868 μs |  1.1105 μs |  1.0388 μs |  9.4905 |  1.5984 |  78.18 KB |
