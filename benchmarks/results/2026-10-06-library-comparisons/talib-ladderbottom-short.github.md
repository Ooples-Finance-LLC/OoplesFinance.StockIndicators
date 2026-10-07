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
| **Ooples**     | **1000**  | **TaLib(...)ottom [26]** |   **200.20 μs** |    **40.85 μs** |  **2.239 μs** |  **1.00** |    **0.01** |  **32.4707** |   **8.0566** |        **-** |  **266.55 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)ottom [26] |    23.60 μs |    19.36 μs |  1.061 μs |  0.12 |    0.00 |   1.4648 |   0.0305 |        - |   12.08 KB |        0.05 |
|            |       |                      |             |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)ottom [26]** | **2,309.49 μs** | **1,740.73 μs** | **95.415 μs** |  **1.00** |    **0.05** | **679.6875** | **562.5000** | **496.0938** | **3714.92 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)ottom [26] |   288.13 μs |   885.23 μs | 48.523 μs |  0.12 |    0.02 |  14.1602 |        - |        - |  117.55 KB |        0.03 |
