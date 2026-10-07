```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)alled [21]** |   **380.9 μs** |    **59.01 μs** |   **3.23 μs** |  **1.00** |    **0.01** |  **32.7148** |   **9.2773** |        **-** |   **270.7 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)alled [21] |   128.1 μs |    28.30 μs |   1.55 μs |  0.34 |    0.00 |   1.4648 |        - |        - |   12.08 KB |        0.04 |
|            |       |                      |            |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)alled [21]** | **4,190.6 μs** | **2,596.06 μs** | **142.30 μs** |  **1.00** |    **0.04** | **671.8750** | **570.3125** | **492.1875** | **3719.38 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)alled [21] | 1,298.9 μs |   321.06 μs |  17.60 μs |  0.31 |    0.01 |  13.6719 |        - |        - |  117.56 KB |        0.03 |
