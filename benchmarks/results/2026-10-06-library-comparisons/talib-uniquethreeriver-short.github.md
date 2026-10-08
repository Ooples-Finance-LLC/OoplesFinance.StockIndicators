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
| **Ooples**     | **1000**  | **TaLib(...)River [30]** |   **266.54 μs** |  **66.26 μs** |  **3.632 μs** |  **1.00** |    **0.02** |  **32.7148** |   **8.3008** |        **-** |  **267.83 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)River [30] |    50.77 μs |  24.00 μs |  1.315 μs |  0.19 |    0.00 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)River [30]** | **2,858.70 μs** | **804.99 μs** | **44.124 μs** |  **1.00** |    **0.02** | **679.6875** | **554.6875** | **496.0938** | **3715.21 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)River [30] |   640.85 μs | 605.22 μs | 33.174 μs |  0.22 |    0.01 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
