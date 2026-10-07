```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean       | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |-----------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetFcb** |   **634.0 μs** |  **1,045.4 μs** |    **57.30 μs** |  **1.01** |    **0.11** |  **313.88 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetFcb | 1,282.0 μs |    844.8 μs |    46.31 μs |  2.03 |    0.17 |  255.46 KB |        0.81 |
|            |       |                |            |             |             |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetFcb** | **1,382.9 μs** |  **8,332.4 μs** |   **456.73 μs** |  **1.07** |    **0.40** | **3146.19 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetFcb | 8,050.6 μs | 23,640.1 μs | 1,295.80 μs |  6.20 |    1.74 |  2486.9 KB |        0.79 |
