```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetObv** |   **267.15 μs** |   **109.87 μs** |  **6.023 μs** |  **1.00** |    **0.03** |  **36.6211** |   **8.7891** |        **-** |  **300.36 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetObv |    94.16 μs |    58.43 μs |  3.203 μs |  0.35 |    0.01 |  20.7520 |   4.1504 |        - |  169.95 KB |        0.57 |
|            |       |                |             |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **Skender.GetObv** | **2,941.49 μs** | **1,010.35 μs** | **55.381 μs** |  **1.00** |    **0.02** | **718.7500** | **593.7500** | **496.0938** | **4059.58 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetObv |   989.62 μs |   485.64 μs | 26.620 μs |  0.34 |    0.01 | 206.0547 | 140.6250 |        - | 1690.46 KB |        0.42 |
