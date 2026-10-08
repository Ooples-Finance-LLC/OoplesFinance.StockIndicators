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
| **Ooples**     | **1000**  | **TaLib(...)Cross [25]** |   **254.21 μs** |  **54.70 μs** |  **2.998 μs** |  **1.00** |    **0.01** |  **32.2266** |   **8.7891** |        **-** |  **266.18 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Cross [25] |    62.42 μs |  15.11 μs |  0.828 μs |  0.25 |    0.00 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)Cross [25]** | **2,826.71 μs** | **801.11 μs** | **43.911 μs** |  **1.00** |    **0.02** | **679.6875** | **566.4063** | **496.0938** | **3713.13 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Cross [25] |   657.02 μs |  20.46 μs |  1.122 μs |  0.23 |    0.00 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
