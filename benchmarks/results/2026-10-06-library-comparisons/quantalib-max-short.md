```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId        | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |-------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Max** | **2,125.1 μs** | **2,442.6 μs** | **133.89 μs** |  **1.00** |    **0.08** |  **319.49 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Max |   817.7 μs | 1,542.1 μs |  84.53 μs |  0.39 |    0.04 |  388.31 KB |        1.22 |
|            |       |               |            |            |           |       |         |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Max** | **2,378.8 μs** | **3,854.5 μs** | **211.28 μs** |  **1.01** |    **0.11** | **4260.04 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Max | 1,917.7 μs | 2,876.7 μs | 157.68 μs |  0.81 |    0.08 | 3882.54 KB |        0.91 |
