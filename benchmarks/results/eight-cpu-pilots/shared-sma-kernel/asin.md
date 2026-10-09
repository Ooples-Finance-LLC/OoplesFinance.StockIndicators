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
| **OoplesBuilderBatch**    | **1000**  | **TaLib.Functions.Asin** |  **22.71 μs** |  **4.335 μs** |  **4.055 μs** |  **7.8936** |  **1.5349** |  **65.07 KB** |
| CompetitorNativeBatch | 1000  | TaLib.Functions.Asin |  15.06 μs |  2.366 μs |  2.213 μs |  0.9588 |       - |   7.87 KB |
| **OoplesBuilderBatch**    | **10000** | **TaLib.Functions.Asin** | **269.63 μs** | **78.316 μs** | **73.256 μs** | **75.2688** | **41.8160** | **628.05 KB** |
| CompetitorNativeBatch | 10000 | TaLib.Functions.Asin | 133.05 μs | 25.501 μs | 23.854 μs |  8.9841 |  1.3822 |  78.18 KB |
