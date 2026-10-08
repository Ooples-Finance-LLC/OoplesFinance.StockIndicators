```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)dBaby [27]** |   **290.82 μs** |   **136.40 μs** |   **7.477 μs** |  **1.00** |    **0.03** |  **32.7148** |  **10.7422** |        **-** |  **269.41 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)dBaby [27] |    87.78 μs |   138.27 μs |   7.579 μs |  0.30 |    0.02 |   1.4648 |        - |        - |   12.08 KB |        0.04 |
|            |       |                      |             |             |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)dBaby [27]** | **3,179.55 μs** | **2,407.55 μs** | **131.966 μs** |  **1.00** |    **0.05** | **679.6875** | **570.3125** | **496.0938** | **3717.44 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)dBaby [27] |   850.97 μs |   550.60 μs |  30.180 μs |  0.27 |    0.01 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
