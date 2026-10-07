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
| **Ooples**     | **1000**  | **TaLib(...)gStar [25]** |   **253.43 μs** |   **124.769 μs** |   **6.839 μs** |  **1.00** |    **0.03** |  **33.2031** |   **8.3008** |        **-** |  **273.57 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)gStar [25] |    75.35 μs |     4.116 μs |   0.226 μs |  0.30 |    0.01 |   1.4648 |        - |        - |   12.08 KB |        0.04 |
|            |       |                      |             |              |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)gStar [25]** | **2,810.79 μs** | **2,265.470 μs** | **124.178 μs** |  **1.00** |    **0.05** | **687.5000** | **566.4063** | **496.0938** | **3773.89 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)gStar [25] |   797.77 μs |    93.475 μs |   5.124 μs |  0.28 |    0.01 |  13.6719 |        - |        - |  117.55 KB |        0.03 |
