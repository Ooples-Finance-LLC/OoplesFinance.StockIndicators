```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId        | Mean        | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |-------------- |------------:|------------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Ema** |   **181.97 μs** |    **78.96 μs** |   **4.328 μs** |  **1.00** |    **0.03** |  **35.4004** |   **9.5215** |        **-** |  **290.98 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Ema |    47.37 μs |    19.43 μs |   1.065 μs |  0.26 |    0.01 |   5.7983 |   0.1831 |   0.0610 |   48.02 KB |        0.17 |
|            |       |               |             |             |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Ema** | **2,114.25 μs** | **2,417.98 μs** | **132.538 μs** |  **1.00** |    **0.08** | **707.0313** | **566.4063** | **496.0938** | **3948.82 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Ema |   474.58 μs |    96.99 μs |   5.316 μs |  0.23 |    0.01 |  57.1289 |  11.2305 |        - |   469.9 KB |        0.12 |
