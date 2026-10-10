```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=15  IterationTime=250ms  LaunchCount=1
UnrollFactor=1  WarmupCount=8

```
| Method                | Bars  | PairId               | Mean      | Error     | StdDev    | Gen0    | Gen1    | Allocated |
|---------------------- |------ |--------------------- |----------:|----------:|----------:|--------:|--------:|----------:|
| **OoplesBuilderBatch**    | **1000**  | **TaLib.Functions.Asin** |  **26.68 μs** |  **4.280 μs** |  **4.004 μs** |  **7.8803** |  **1.5323** |  **65.03 KB** |
| CompetitorNativeBatch | 1000  | TaLib.Functions.Asin |  15.75 μs |  2.334 μs |  2.184 μs |  0.9191 |       - |   7.87 KB |
| **OoplesBuilderBatch**    | **10000** | **TaLib.Functions.Asin** | **251.27 μs** | **42.834 μs** | **40.067 μs** | **75.8405** | **43.7842** | **628.01 KB** |
| CompetitorNativeBatch | 10000 | TaLib.Functions.Asin | 142.38 μs | 26.925 μs | 25.186 μs |  9.1837 |  1.5306 |  78.18 KB |
