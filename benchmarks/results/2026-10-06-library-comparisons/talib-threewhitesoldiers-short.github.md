```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|----------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)diers [32]** |   **363.4 μs** | **211.78 μs** | **11.61 μs** |  **1.00** |    **0.04** |  **32.7148** |  **10.7422** |        **-** |  **269.76 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)diers [32] |   164.3 μs |  54.46 μs |  2.99 μs |  0.45 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.04 |
|            |       |                      |            |           |          |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)diers [32]** | **3,833.8 μs** | **546.16 μs** | **29.94 μs** |  **1.00** |    **0.01** | **679.6875** | **562.5000** | **496.0938** | **3717.13 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)diers [32] | 1,576.8 μs |  72.13 μs |  3.95 μs |  0.41 |    0.00 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
