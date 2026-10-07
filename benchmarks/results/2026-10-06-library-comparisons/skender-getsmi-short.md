```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetSmi** |  **4,847.4 μs** |  **4,274.3 μs** | **234.29 μs** |  **1.00** |    **0.06** |  **772.27 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetSmi |    693.5 μs |  1,205.3 μs |  66.07 μs |  0.14 |    0.01 |  216.68 KB |        0.28 |
|            |       |                |             |             |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetSmi** | **14,916.9 μs** | **17,684.3 μs** | **969.34 μs** |  **1.00** |    **0.08** | **8813.07 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetSmi |  2,153.7 μs |  9,761.9 μs | 535.08 μs |  0.14 |    0.03 | 2097.26 KB |        0.24 |
