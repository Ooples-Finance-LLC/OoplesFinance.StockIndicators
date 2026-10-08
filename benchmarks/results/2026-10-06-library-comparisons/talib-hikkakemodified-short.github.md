```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-----------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)ified [29]** |   **180.58 μs** |  **30.073 μs** |  **1.648 μs** |  **1.00** |    **0.01** |  **32.2266** |   **8.5449** |        **-** |  **265.58 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)ified [29] |    21.87 μs |   3.242 μs |  0.178 μs |  0.12 |    0.00 |   1.4648 |   0.0305 |        - |   12.08 KB |        0.05 |
|            |       |                      |             |            |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)ified [29]** | **2,108.33 μs** | **886.645 μs** | **48.600 μs** |  **1.00** |    **0.03** | **679.6875** | **562.5000** | **496.0938** | **3714.21 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)ified [29] |   290.62 μs | 368.768 μs | 20.213 μs |  0.14 |    0.01 |  14.1602 |        - |        - |  117.55 KB |        0.03 |
