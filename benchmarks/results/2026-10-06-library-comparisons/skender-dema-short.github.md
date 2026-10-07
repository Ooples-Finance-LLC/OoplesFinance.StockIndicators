```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId          | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |---------------- |------------:|----------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetDema** |   **283.98 μs** |  **70.98 μs** |  **3.891 μs** |  **1.00** |    **0.02** |  **32.2266** |   **8.7891** |        **-** |  **264.76 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetDema |    38.13 μs |  54.09 μs |  2.965 μs |  0.13 |    0.01 |  11.0474 |   1.6479 |        - |   90.74 KB |        0.34 |
|            |       |                 |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **Skender.GetDema** | **3,066.00 μs** | **536.03 μs** | **29.382 μs** |  **1.00** |    **0.01** | **679.6875** | **562.5000** | **496.0938** | **3712.37 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetDema |   480.65 μs | 127.70 μs |  6.999 μs |  0.16 |    0.00 | 133.7891 |  69.3359 |  42.9688 |  899.64 KB |        0.24 |
