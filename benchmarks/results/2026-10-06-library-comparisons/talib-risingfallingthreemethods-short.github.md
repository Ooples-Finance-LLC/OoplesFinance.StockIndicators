```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-----------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)thods [39]** |   **288.2 μs** |   **112.2 μs** |   **6.15 μs** |  **1.00** |    **0.03** |  **32.7148** |   **8.3008** |        **-** |   **268.2 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)thods [39] |   141.6 μs |   256.9 μs |  14.08 μs |  0.49 |    0.04 |   1.4648 |        - |        - |   12.14 KB |        0.05 |
|            |       |                      |            |            |           |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)thods [39]** | **3,140.7 μs** | **2,004.6 μs** | **109.88 μs** |  **1.00** |    **0.04** | **679.6875** | **578.1250** | **496.0938** | **3716.25 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)thods [39] | 1,373.0 μs |   271.3 μs |  14.87 μs |  0.44 |    0.01 |  13.6719 |        - |        - |  117.62 KB |        0.03 |
