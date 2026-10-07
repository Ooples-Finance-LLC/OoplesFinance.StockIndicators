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
| **Ooples**     | **1000**  | **TaLib(...)Lines [29]** |   **286.52 μs** |    **84.63 μs** |  **4.639 μs** |  **1.00** |    **0.02** |  **32.7148** |   **9.2773** |        **-** |  **268.66 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Lines [29] |    70.74 μs |    21.73 μs |  1.191 μs |  0.25 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.04 |
|            |       |                      |             |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)Lines [29]** | **3,058.33 μs** |   **935.96 μs** | **51.303 μs** |  **1.00** |    **0.02** | **679.6875** | **562.5000** | **496.0938** | **3715.79 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Lines [29] |   794.07 μs | 1,078.13 μs | 59.096 μs |  0.26 |    0.02 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
