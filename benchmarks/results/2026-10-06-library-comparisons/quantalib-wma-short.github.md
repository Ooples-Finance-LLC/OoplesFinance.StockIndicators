```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId        | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |-------------- |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Wma** | **2,511.1 μs** |    **696.0 μs** |  **38.15 μs** |  **1.00** |    **0.02** |  **265.19 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Wma |   517.5 μs |    732.6 μs |  40.15 μs |  0.21 |    0.01 |  259.32 KB |        0.98 |
|            |       |               |            |             |           |       |         |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Wma** | **3,550.2 μs** | **12,162.1 μs** | **666.65 μs** |  **1.03** |    **0.25** | **3710.31 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Wma | 1,316.9 μs |  3,260.1 μs | 178.70 μs |  0.38 |    0.08 | 2570.07 KB |        0.69 |
