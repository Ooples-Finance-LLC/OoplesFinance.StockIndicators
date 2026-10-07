```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)iStar [29]** |   **298.07 μs** |     **8.569 μs** |  **0.470 μs** |  **1.00** |    **0.00** |  **32.7148** |   **9.2773** |        **-** |  **271.05 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)iStar [29] |    79.74 μs |    17.837 μs |  0.978 μs |  0.27 |    0.00 |   1.4648 |        - |        - |   12.08 KB |        0.04 |
|            |       |                      |             |              |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)iStar [29]** | **3,272.97 μs** | **1,052.312 μs** | **57.681 μs** |  **1.00** |    **0.02** | **679.6875** | **558.5938** | **496.0938** | **3740.74 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)iStar [29] |   815.34 μs |    57.038 μs |  3.126 μs |  0.25 |    0.00 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
