```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId        | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |-------------- |------------:|------------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Sma** |   **142.02 μs** |    **16.88 μs** |  **0.925 μs** |  **1.00** |    **0.01** |  **35.4004** |   **9.5215** |        **-** |  **290.98 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Sma |    90.48 μs |    36.76 μs |  2.015 μs |  0.64 |    0.01 |  26.2451 |   0.7324 |        - |  215.05 KB |        0.74 |
|            |       |               |             |             |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Sma** | **1,662.25 μs** |   **189.37 μs** | **10.380 μs** |  **1.00** |    **0.01** | **708.9844** | **574.2188** | **498.0469** | **3949.05 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Sma |   940.74 μs | 1,000.34 μs | 54.832 μs |  0.57 |    0.03 | 265.6250 |  52.7344 |        - | 2173.25 KB |        0.55 |
