```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetWma** |   **206.15 μs** |    **61.72 μs** |   **3.383 μs** |  **1.00** |    **0.02** |  **36.3770** |  **11.9629** |        **-** |  **298.84 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetWma |    67.28 μs |    10.33 μs |   0.566 μs |  0.33 |    0.01 |  10.9863 |   1.5869 |        - |   90.74 KB |        0.30 |
|            |       |                |             |             |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **Skender.GetWma** | **2,356.60 μs** | **1,987.98 μs** | **108.968 μs** |  **1.00** |    **0.06** | **714.8438** | **531.2500** | **496.0938** | **4028.34 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetWma |   790.41 μs |   476.17 μs |  26.100 μs |  0.34 |    0.02 | 133.7891 |  71.2891 |  42.9688 |  899.61 KB |        0.22 |
