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
| **Ooples**     | **1000**  | **Skender.GetSma** |   **148.43 μs** |  **68.41 μs** |  **3.750 μs** |  **1.00** |    **0.03** |  **35.4004** |   **9.5215** |        **-** | **290.98 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetSma |    55.53 μs |  81.04 μs |  4.442 μs |  0.37 |    0.03 |  11.0474 |   1.6479 |        - |  90.74 KB |        0.31 |
|            |       |                |             |           |           |       |         |          |          |          |           |             |
| **Ooples**     | **10000** | **Skender.GetSma** | **1,824.18 μs** | **947.20 μs** | **51.919 μs** |  **1.00** |    **0.03** | **707.0313** | **572.2656** | **496.0938** | **3948.5 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetSma |   621.26 μs | 315.29 μs | 17.282 μs |  0.34 |    0.01 | 131.8359 |  66.4063 |  41.0156 | 899.58 KB |        0.23 |
