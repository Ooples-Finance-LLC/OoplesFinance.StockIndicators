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
| **Ooples**     | **1000**  | **TaLib(...)kaway [23]** |   **246.37 μs** | **222.834 μs** | **12.214 μs** |  **1.00** |    **0.06** |  **32.4707** |   **8.0566** |        **-** |  **266.55 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)kaway [23] |    30.14 μs |   6.556 μs |  0.359 μs |  0.12 |    0.01 |   1.4648 |   0.0305 |        - |   12.08 KB |        0.05 |
|            |       |                      |             |            |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)kaway [23]** | **2,640.02 μs** | **344.446 μs** | **18.880 μs** |  **1.00** |    **0.01** | **679.6875** | **562.5000** | **496.0938** | **3714.07 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)kaway [23] |   481.48 μs | 809.185 μs | 44.354 μs |  0.18 |    0.01 |  14.1602 |        - |        - |  117.55 KB |        0.03 |
