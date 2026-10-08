```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |------------:|-------------:|-----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Cover [28]** |   **201.62 μs** |    **85.674 μs** |   **4.696 μs** |  **1.00** |    **0.03** |  **32.4707** |   **8.0566** |        **-** |    **266 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Cover [28] |    26.05 μs |     4.751 μs |   0.260 μs |  0.13 |    0.00 |   1.4648 |   0.0305 |        - |  12.08 KB |        0.05 |
|            |       |                      |             |              |            |       |         |          |          |          |           |             |
| **Ooples**     | **10000** | **TaLib(...)Cover [28]** | **2,639.32 μs** | **5,606.175 μs** | **307.293 μs** |  **1.01** |    **0.15** | **679.6875** | **566.4063** | **496.0938** | **3713.3 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Cover [28] |   339.65 μs |    94.805 μs |   5.197 μs |  0.13 |    0.01 |  14.1602 |        - |        - | 117.55 KB |        0.03 |
