```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)South [31]** |   **357.7 μs** |   **176.19 μs** |   **9.66 μs** |  **1.00** |    **0.03** |  **32.7148** |  **10.7422** |        **-** |  **269.77 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)South [31] |   101.1 μs |    15.27 μs |   0.84 μs |  0.28 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.04 |
|            |       |                      |            |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)South [31]** | **3,759.2 μs** | **1,991.18 μs** | **109.14 μs** |  **1.00** |    **0.04** | **679.6875** | **554.6875** | **496.0938** | **3718.31 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)South [31] | 1,006.2 μs |   141.41 μs |   7.75 μs |  0.27 |    0.01 |  13.6719 |        - |        - |  117.56 KB |        0.03 |
