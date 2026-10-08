```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-----------:|----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)kiGap [35]** |   **166.3 μs** |   **294.4 μs** |  **16.13 μs** |  **1.01** |    **0.12** |   **32.2266** |   **8.0566** |        **-** |  **264.47 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)kiGap [35] |   579.1 μs |   147.7 μs |   8.10 μs |  3.50 |    0.29 |   88.8672 |  33.2031 |        - |  730.76 KB |        2.76 |
|            |       |                      |            |            |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)kiGap [35]** | **1,802.7 μs** |   **211.2 μs** |  **11.58 μs** |  **1.00** |    **0.01** |  **529.2969** | **417.9688** | **347.6563** | **3710.49 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)kiGap [35] | 9,907.5 μs | 3,902.2 μs | 213.89 μs |  5.50 |    0.11 | 1031.2500 | 750.0000 | 468.7500 | 7180.69 KB |        1.94 |
