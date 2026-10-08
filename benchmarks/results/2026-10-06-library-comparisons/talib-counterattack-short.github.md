```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|----------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)ttack [27]** |   **271.82 μs** | **278.87 μs** | **15.286 μs** |  **1.00** |    **0.07** |  **32.2266** |   **8.3008** |        **-** |  **267.27 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)ttack [27] |    79.71 μs |  38.47 μs |  2.109 μs |  0.29 |    0.02 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)ttack [27]** | **2,916.33 μs** | **870.41 μs** | **47.710 μs** |  **1.00** |    **0.02** | **679.6875** | **570.3125** | **496.0938** | **3714.92 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)ttack [27] |   804.92 μs | 347.69 μs | 19.058 μs |  0.28 |    0.01 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
