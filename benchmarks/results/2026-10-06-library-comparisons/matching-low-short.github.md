```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error     | StdDev    | Ratio | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|----------:|----------:|------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)ngLow [25]** |   **206.80 μs** |  **16.79 μs** |  **0.920 μs** |  **1.00** |  **32.4707** |   **8.0566** |        **-** |  **265.67 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)ngLow [25] |    28.36 μs |  52.13 μs |  2.857 μs |  0.14 |   1.4648 |   0.0305 |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)ngLow [25]** | **2,196.68 μs** | **429.96 μs** | **23.568 μs** |  **1.00** | **679.6875** | **566.4063** | **496.0938** | **3713.04 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)ngLow [25] |   294.85 μs | 133.79 μs |  7.334 μs |  0.13 |  14.1602 |        - |        - |  117.55 KB |        0.03 |
