```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|----------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)tHold [21]** |   **287.6 μs** |  **91.04 μs** |  **4.99 μs** |  **1.00** |    **0.02** |  **32.7148** |   **8.3008** |        **-** |  **268.21 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)tHold [21] |   107.3 μs |  27.23 μs |  1.49 μs |  0.37 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |            |           |          |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)tHold [21]** | **3,087.0 μs** | **218.97 μs** | **12.00 μs** |  **1.00** |    **0.00** | **679.6875** | **554.6875** | **496.0938** | **3716.89 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)tHold [21] | 1,118.5 μs | 608.98 μs | 33.38 μs |  0.36 |    0.01 |  13.6719 |        - |        - |  117.56 KB |        0.03 |
