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
| **Ooples**     | **1000**  | **TaLib(...)tside [26]** |   **178.610 μs** |  **87.774 μs** |  **4.8112 μs** |  **1.00** |    **0.03** |  **32.2266** |   **8.0566** |        **-** |  **265.13 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)tside [26] |     4.761 μs |   4.884 μs |  0.2677 μs |  0.03 |    0.00 |   1.4725 |   0.0305 |        - |   12.08 KB |        0.05 |
|            |       |                      |              |            |            |       |         |          |          |          |            |             |
| **Ooples**     | **10000** | **TaLib(...)tside [26]** | **2,057.470 μs** | **909.212 μs** | **49.8370 μs** |  **1.00** |    **0.03** | **679.6875** | **570.3125** | **496.0938** | **3712.76 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)tside [26] |    68.410 μs |  43.376 μs |  2.3776 μs |  0.03 |    0.00 |  14.2822 |        - |        - |  117.55 KB |        0.03 |
