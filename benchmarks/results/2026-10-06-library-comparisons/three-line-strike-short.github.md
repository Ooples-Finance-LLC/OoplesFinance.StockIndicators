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
| **Ooples**     | **1000**  | **TaLib(...)trike [29]** |   **217.30 μs** | **106.44 μs** |  **5.834 μs** |  **1.00** |    **0.03** |  **32.4707** |   **8.0566** |        **-** |  **265.67 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)trike [29] |    55.28 μs | 108.89 μs |  5.969 μs |  0.25 |    0.02 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)trike [29]** | **2,347.83 μs** | **155.31 μs** |  **8.513 μs** |  **1.00** |    **0.00** | **679.6875** | **558.5938** | **496.0938** | **3713.27 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)trike [29] |   549.05 μs | 278.43 μs | 15.262 μs |  0.23 |    0.01 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
