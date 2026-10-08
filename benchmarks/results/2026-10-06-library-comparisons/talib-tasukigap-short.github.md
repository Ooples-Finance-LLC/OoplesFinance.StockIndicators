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
| **Ooples**     | **1000**  | **TaLib(...)kiGap [23]** |   **207.23 μs** |   **171.491 μs** |   **9.400 μs** |  **1.00** |    **0.06** |  **32.2266** |   **8.5449** |        **-** |  **265.39 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)kiGap [23] |    24.04 μs |     4.073 μs |   0.223 μs |  0.12 |    0.00 |   1.4648 |   0.0305 |        - |   12.08 KB |        0.05 |
|            |       |                      |             |              |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)kiGap [23]** | **2,551.02 μs** | **3,722.962 μs** | **204.068 μs** |  **1.00** |    **0.10** | **679.6875** | **566.4063** | **496.0938** | **3712.46 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)kiGap [23] |   291.57 μs |   131.380 μs |   7.201 μs |  0.11 |    0.01 |  14.1602 |        - |        - |  117.55 KB |        0.03 |
