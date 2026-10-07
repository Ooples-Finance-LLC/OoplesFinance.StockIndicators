```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)iStar [29]** |   **301.55 μs** |    **70.718 μs** |   **3.876 μs** |  **1.00** |    **0.02** |  **32.7148** |   **9.2773** |        **-** |  **270.59 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)iStar [29] |    79.95 μs |     4.439 μs |   0.243 μs |  0.27 |    0.00 |   1.4648 |        - |        - |   12.08 KB |        0.04 |
|            |       |                      |             |              |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)iStar [29]** | **3,650.29 μs** | **4,571.394 μs** | **250.574 μs** |  **1.00** |    **0.08** | **683.5938** | **558.5938** | **496.0938** | **3741.72 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)iStar [29] |   825.75 μs |   240.987 μs |  13.209 μs |  0.23 |    0.01 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
