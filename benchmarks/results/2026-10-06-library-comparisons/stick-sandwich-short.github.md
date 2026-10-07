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
| **Ooples**     | **1000**  | **TaLib(...)dwich [27]** |   **194.62 μs** |  **60.56 μs** |  **3.319 μs** |  **1.00** |    **0.02** |  **32.4707** |   **8.0566** |        **-** |  **265.67 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)dwich [27] |    23.64 μs |  25.82 μs |  1.415 μs |  0.12 |    0.01 |   1.4648 |   0.0305 |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)dwich [27]** | **2,165.11 μs** | **408.15 μs** | **22.372 μs** |  **1.00** |    **0.01** | **679.6875** | **566.4063** | **496.0938** | **3713.18 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)dwich [27] |   250.96 μs |  47.34 μs |  2.595 μs |  0.12 |    0.00 |  14.1602 |        - |        - |  117.55 KB |        0.03 |
