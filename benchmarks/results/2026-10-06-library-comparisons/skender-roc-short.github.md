```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetRoc** |   **623.75 μs** |   **490.51 μs** | **26.887 μs** |  **1.00** |    **0.05** |   **75.1953** |  **23.4375** |        **-** |  **618.63 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetRoc |    88.42 μs |   126.93 μs |  6.958 μs |  0.14 |    0.01 |   18.1885 |   3.2959 |        - |  149.23 KB |        0.24 |
|            |       |                |             |             |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Skender.GetRoc** | **6,626.84 μs** | **1,219.04 μs** | **66.820 μs** |  **1.00** |    **0.01** | **1109.3750** | **937.5000** | **492.1875** | **7265.51 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetRoc |   938.74 μs |   214.56 μs | 11.761 μs |  0.14 |    0.00 |  205.0781 | 171.8750 |  43.9453 |  1476.6 KB |        0.20 |
