```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId         | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------- |------ |--------------- |------------:|----------:|----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetEma** |   **181.69 μs** | **141.25 μs** |  **7.742 μs** |  **1.00** |    **0.05** |  **35.4004** |   **9.5215** |        **-** | **290.98 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetEma |    35.43 μs |  29.54 μs |  1.619 μs |  0.20 |    0.01 |  11.0474 |   1.6479 |        - |  90.74 KB |        0.31 |
|            |       |                |             |           |           |       |         |          |          |          |           |             |
| **Ooples**     | **10000** | **Skender.GetEma** | **2,088.23 μs** | **626.72 μs** | **34.353 μs** |  **1.00** |    **0.02** | **707.0313** | **574.2188** | **496.0938** | **3949.6 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetEma |   502.47 μs | 668.59 μs | 36.648 μs |  0.24 |    0.02 | 131.8359 |  69.8242 |  41.0156 | 899.52 KB |        0.23 |
