```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId              | Mean         | Error         | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |-------------------- |-------------:|--------------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Sma** |   **155.646 μs** |   **141.8737 μs** |  **7.7766 μs** |  **1.00** |    **0.06** |  **35.4004** |   **9.5215** |        **-** |  **290.98 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Sma |     3.317 μs |     0.8952 μs |  0.0491 μs |  0.02 |    0.00 |   1.9569 |   0.0420 |        - |   16.02 KB |        0.06 |
|            |       |                     |              |               |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Sma** | **1,825.936 μs** | **1,026.9652 μs** | **56.2914 μs** |  **1.00** |    **0.04** | **708.9844** | **574.2188** | **498.0469** | **3950.16 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Sma |    35.748 μs |    11.2048 μs |  0.6142 μs |  0.02 |    0.00 |  19.0430 |   3.1738 |        - |  156.64 KB |        0.04 |
