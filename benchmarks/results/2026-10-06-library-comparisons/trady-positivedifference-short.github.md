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
| **Ooples**     | **1000**  | **Trady(...)rence [34]** |   **178.0 μs** |    **85.59 μs** |   **4.69 μs** |  **1.00** |    **0.03** |   **32.2266** |   **8.0566** |        **-** |  **264.96 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)rence [34] |   552.6 μs |   486.01 μs |  26.64 μs |  3.11 |    0.15 |  110.3516 |  39.0625 |        - |  904.99 KB |        3.42 |
|            |       |                      |            |             |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)rence [34]** | **2,233.8 μs** | **2,488.57 μs** | **136.41 μs** |  **1.00** |    **0.08** |  **535.1563** | **425.7813** | **355.4688** | **3710.13 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)rence [34] | 9,648.1 μs | 8,086.45 μs | 443.25 μs |  4.33 |    0.29 | 1265.6250 | 953.1250 | 578.1250 | 8829.79 KB |        2.38 |
