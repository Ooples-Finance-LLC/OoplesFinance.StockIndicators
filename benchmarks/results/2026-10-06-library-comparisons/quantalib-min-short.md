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
| **Ooples**     | **1000**  | **QuanTAlib.Min** | **2,151.6 μs** |   **288.7 μs** |  **15.82 μs** |  **1.00** |    **0.01** |  **319.49 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Min |   829.1 μs | 1,310.8 μs |  71.85 μs |  0.39 |    0.03 |  387.98 KB |        1.21 |
|            |       |               |            |            |           |       |         |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Min** | **2,781.7 μs** | **6,204.8 μs** | **340.10 μs** |  **1.01** |    **0.15** | **4259.38 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Min | 1,724.5 μs | 3,103.8 μs | 170.13 μs |  0.63 |    0.09 | 3882.59 KB |        0.91 |
