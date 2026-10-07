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
| **Ooples**     | **1000**  | **TaLib(...)istar [21]** |   **229.89 μs** |  **92.797 μs** |  **5.086 μs** |  **1.00** |    **0.03** |  **32.4707** |   **9.0332** |        **-** |  **266.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)istar [21] |    33.82 μs |   6.072 μs |  0.333 μs |  0.15 |    0.00 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |            |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)istar [21]** | **2,487.78 μs** | **203.017 μs** | **11.128 μs** |  **1.00** |    **0.01** | **679.6875** | **570.3125** | **496.0938** | **3714.46 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)istar [21] |   342.81 μs |  90.201 μs |  4.944 μs |  0.14 |    0.00 |  14.1602 |        - |        - |  117.55 KB |        0.03 |
