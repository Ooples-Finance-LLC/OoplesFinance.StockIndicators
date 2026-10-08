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
| **Ooples**     | **1000**  | **TaLib(...)Crows [22]** |   **214.84 μs** |   **7.113 μs** |  **0.390 μs** |  **1.00** |    **0.00** |  **32.4707** |   **8.0566** |        **-** |  **266.37 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Crows [22] |    25.88 μs |   2.974 μs |  0.163 μs |  0.12 |    0.00 |   1.4648 |   0.0305 |        - |   12.08 KB |        0.05 |
|            |       |                      |             |            |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)Crows [22]** | **2,381.87 μs** | **781.133 μs** | **42.817 μs** |  **1.00** |    **0.02** | **679.6875** | **570.3125** | **496.0938** | **3713.33 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Crows [22] |   335.43 μs | 244.935 μs | 13.426 μs |  0.14 |    0.01 |  14.1602 |        - |        - |  117.55 KB |        0.03 |
