```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)bands [24]** | **21,079.2 μs** |  **14,293.0 μs** |   **783.45 μs** |  **1.00** |    **0.05** |         **-** |         **-** |  **6823.06 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)bands [24] |    646.0 μs |   1,120.5 μs |    61.42 μs |  0.03 |    0.00 |         - |         - |   119.59 KB |        0.02 |
|            |       |                      |             |              |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)bands [24]** | **75,406.6 μs** | **170,704.1 μs** | **9,356.87 μs** | **1.010** |    **0.15** | **8000.0000** | **1000.0000** | **69476.65 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)bands [24] |    422.7 μs |   2,070.9 μs |   113.51 μs | 0.006 |    0.00 |         - |         - |  1130.29 KB |        0.02 |
