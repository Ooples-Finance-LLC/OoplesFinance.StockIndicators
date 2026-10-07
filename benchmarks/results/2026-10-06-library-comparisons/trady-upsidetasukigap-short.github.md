```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)kiGap [33]** |   **145.9 μs** |    **72.17 μs** |   **3.96 μs** |  **1.00** |    **0.03** |   **32.2266** |   **8.0566** |        **-** |  **264.55 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)kiGap [33] |   615.6 μs |   248.07 μs |  13.60 μs |  4.22 |    0.13 |   88.8672 |  33.2031 |        - |  730.76 KB |        2.76 |
|            |       |                      |            |             |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)kiGap [33]** | **1,737.5 μs** |   **227.08 μs** |  **12.45 μs** |  **1.00** |    **0.01** |  **525.3906** | **412.1094** | **343.7500** | **3710.41 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)kiGap [33] | 9,156.0 μs | 4,628.73 μs | 253.72 μs |  5.27 |    0.13 | 1031.2500 | 750.0000 | 468.7500 |  7180.9 KB |        1.94 |
