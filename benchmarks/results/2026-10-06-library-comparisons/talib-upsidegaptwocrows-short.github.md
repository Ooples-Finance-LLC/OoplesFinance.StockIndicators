```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Crows [31]** |   **268.75 μs** |    **70.87 μs** |  **3.884 μs** |  **1.00** |    **0.02** |  **32.7148** |   **8.3008** |        **-** |  **267.83 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Crows [31] |    46.79 μs |    13.70 μs |  0.751 μs |  0.17 |    0.00 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)Crows [31]** | **2,972.63 μs** |   **879.28 μs** | **48.196 μs** |  **1.00** |    **0.02** | **679.6875** | **558.5938** | **496.0938** | **3715.36 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Crows [31] |   624.84 μs | 1,586.47 μs | 86.960 μs |  0.21 |    0.03 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
