```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |---------------- |------------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetVwap** |  **4,956.8 μs** | **9,215.0 μs** | **505.10 μs** |  **1.01** |    **0.12** |  **639.71 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetVwap |    707.4 μs |   313.4 μs |  17.18 μs |  0.14 |    0.01 |  167.02 KB |        0.26 |
|            |       |                 |             |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetVwap** | **12,219.1 μs** | **8,265.4 μs** | **453.05 μs** |  **1.00** |    **0.05** | **8312.15 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetVwap |  1,789.1 μs | 4,860.1 μs | 266.40 μs |  0.15 |    0.02 | 1613.98 KB |        0.19 |
