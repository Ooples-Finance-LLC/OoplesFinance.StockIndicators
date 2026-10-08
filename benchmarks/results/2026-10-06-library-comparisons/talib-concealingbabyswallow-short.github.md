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
| **Ooples**     | **1000**  | **TaLib(...)allow [35]** |   **203.41 μs** |    **28.64 μs** |  **1.570 μs** |  **1.00** |    **0.01** |  **32.4707** |   **8.0566** |        **-** |  **266.55 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)allow [35] |    63.32 μs |    27.98 μs |  1.534 μs |  0.31 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)allow [35]** | **2,182.42 μs** |   **365.20 μs** | **20.018 μs** |  **1.00** |    **0.01** | **679.6875** | **566.4063** | **496.0938** | **3714.66 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)allow [35] |   705.28 μs | 1,380.70 μs | 75.681 μs |  0.32 |    0.03 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
