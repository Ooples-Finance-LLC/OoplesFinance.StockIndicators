```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId          | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |---------------- |------------:|-------------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetTema** |   **336.53 μs** |    **82.336 μs** |   **4.513 μs** |  **1.00** |    **0.02** |  **32.2266** |   **7.8125** |        **-** |  **264.86 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetTema |    38.29 μs |     8.744 μs |   0.479 μs |  0.11 |    0.00 |  11.0474 |   1.6479 |        - |   90.74 KB |        0.34 |
|            |       |                 |             |              |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **Skender.GetTema** | **3,927.63 μs** | **5,353.412 μs** | **293.439 μs** |  **1.00** |    **0.09** | **679.6875** | **570.3125** | **496.0938** | **3712.67 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetTema |   562.53 μs |   949.048 μs |  52.021 μs |  0.14 |    0.02 | 135.7422 |  74.2188 |  44.9219 |  899.54 KB |        0.24 |
