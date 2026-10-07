```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|----------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)iStar [22]** |   **264.62 μs** | **129.79 μs** |  **7.114 μs** |  **1.00** |    **0.03** |  **32.2266** |   **8.7891** |        **-** |  **266.18 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)iStar [22] |    63.77 μs |  14.41 μs |  0.790 μs |  0.24 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)iStar [22]** | **2,785.11 μs** | **362.10 μs** | **19.848 μs** |  **1.00** |    **0.01** | **679.6875** | **566.4063** | **496.0938** | **3713.11 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)iStar [22] |   640.41 μs |  91.45 μs |  5.013 μs |  0.23 |    0.00 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
