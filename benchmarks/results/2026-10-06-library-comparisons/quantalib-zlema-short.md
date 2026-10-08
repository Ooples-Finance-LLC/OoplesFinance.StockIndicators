```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |---------------- |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Zlema** | **2,493.5 μs** | **2,458.27 μs** | **134.75 μs** |  **1.00** |    **0.07** |   **264.8 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Zlema |   260.8 μs |    35.30 μs |   1.93 μs |  0.10 |    0.00 |   53.36 KB |        0.20 |
|            |       |                 |            |             |           |       |         |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Zlema** | **3,989.1 μs** | **1,552.04 μs** |  **85.07 μs** |  **1.00** |    **0.03** | **3712.88 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Zlema |   910.5 μs | 1,344.38 μs |  73.69 μs |  0.23 |    0.02 |  475.59 KB |        0.13 |
