```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|---------:|---------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Block [26]** |   **408.2 μs** | **101.0 μs** |  **5.54 μs** |  **1.00** |    **0.02** |  **32.7148** |  **10.2539** |        **-** | **269.93 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Block [26] |   219.2 μs | 180.2 μs |  9.88 μs |  0.54 |    0.02 |   1.4648 |        - |        - |  12.08 KB |        0.04 |
|            |       |                      |            |          |          |       |         |          |          |          |           |             |
| **Ooples**     | **10000** | **TaLib(...)Block [26]** | **4,375.8 μs** | **844.8 μs** | **46.30 μs** |  **1.00** |    **0.01** | **671.8750** | **554.6875** | **492.1875** | **3717.7 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Block [26] | 2,141.9 μs | 264.2 μs | 14.48 μs |  0.49 |    0.01 |  11.7188 |        - |        - | 117.57 KB |        0.03 |
