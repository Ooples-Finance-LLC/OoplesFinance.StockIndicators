```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)dBaby [38]** |    **476.8 μs** |    **72.73 μs** |   **3.99 μs** |  **1.00** |    **0.01** |   **32.7148** |  **10.7422** |        **-** |  **269.34 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)dBaby [38] |    718.0 μs |   559.29 μs |  30.66 μs |  1.51 |    0.06 |  113.2813 |  50.7813 |        - |  930.82 KB |        3.46 |
|            |       |                      |             |             |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)dBaby [38]** |  **5,031.2 μs** |   **308.13 μs** |  **16.89 μs** |  **1.00** |    **0.00** |  **515.6250** | **406.2500** | **335.9375** | **3715.88 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)dBaby [38] | 11,334.1 μs | 8,643.55 μs | 473.78 μs |  2.25 |    0.08 | 1234.3750 | 984.3750 | 656.2500 | 9157.46 KB |        2.46 |
