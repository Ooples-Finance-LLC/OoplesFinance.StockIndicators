```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error         | StdDev       | Median       | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|--------------:|-------------:|-------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Angle [30]** |  **5,688.27 μs** |  **9,413.856 μs** |   **516.005 μs** |  **5,541.70 μs** |  **1.01** |    **0.11** |         **-** |         **-** |  **2541.52 KB** |       **1.000** |
| Competitor | 1000  | TaLib(...)Angle [30] |     61.03 μs |      5.267 μs |     0.289 μs |     61.20 μs |  0.01 |    0.00 |         - |         - |    19.91 KB |       0.008 |
|            |       |                      |              |               |              |              |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)Angle [30]** | **18,175.87 μs** | **53,786.978 μs** | **2,948.246 μs** | **17,372.60 μs** |  **1.02** |    **0.20** | **2000.0000** | **1000.0000** | **26587.92 KB** |       **1.000** |
| Competitor | 10000 | TaLib(...)Angle [30] |    585.57 μs |  6,281.688 μs |   344.321 μs |    399.20 μs |  0.03 |    0.02 |         - |         - |   160.25 KB |       0.006 |
