```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)kkake [21]** |   **157.631 μs** |  **61.085 μs** |  **3.3482 μs** |  **1.00** |    **0.03** |  **32.2266** |   **8.0566** |        **-** |  **264.88 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)kkake [21] |     3.999 μs |   2.180 μs |  0.1195 μs |  0.03 |    0.00 |   1.4725 |   0.0305 |        - |   12.08 KB |        0.05 |
|            |       |                      |              |            |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)kkake [21]** | **1,826.166 μs** | **512.502 μs** | **28.0919 μs** |  **1.00** |    **0.02** | **679.6875** | **576.1719** | **498.0469** | **3712.25 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)kkake [21] |    41.540 μs |  71.575 μs |  3.9233 μs |  0.02 |    0.00 |  14.2822 |        - |        - |  117.55 KB |        0.03 |
