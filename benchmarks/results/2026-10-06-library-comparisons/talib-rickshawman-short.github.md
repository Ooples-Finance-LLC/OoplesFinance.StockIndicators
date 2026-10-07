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
| **Ooples**     | **1000**  | **TaLib(...)awMan [25]** |   **266.71 μs** | **204.95 μs** | **11.234 μs** |  **1.00** |    **0.05** |  **32.4707** |   **7.0801** |        **-** |  **266.73 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)awMan [25] |    80.73 μs |  14.64 μs |  0.803 μs |  0.30 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.05 |
|            |       |                      |             |           |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)awMan [25]** | **2,923.81 μs** | **651.43 μs** | **35.707 μs** |  **1.00** |    **0.01** | **679.6875** | **574.2188** | **496.0938** | **3714.24 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)awMan [25] |   809.18 μs | 115.57 μs |  6.335 μs |  0.28 |    0.00 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
