```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)entum [26]** |   **179.4 μs** |    **76.12 μs** |   **4.17 μs** |  **1.00** |    **0.03** |   **32.2266** |   **8.0566** |        **-** |  **265.04 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)entum [26] |   573.4 μs |   172.81 μs |   9.47 μs |  3.20 |    0.08 |  110.3516 |  39.0625 |        - |  904.99 KB |        3.41 |
|            |       |                      |            |             |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)entum [26]** | **2,104.8 μs** |   **290.47 μs** |  **15.92 μs** |  **1.00** |    **0.01** |  **531.2500** | **425.7813** | **351.5625** | **3710.11 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)entum [26] | 9,605.7 μs | 5,845.24 μs | 320.40 μs |  4.56 |    0.14 | 1265.6250 | 953.1250 | 578.1250 |  8829.9 KB |        2.38 |
