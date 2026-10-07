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
| **Ooples**     | **1000**  | **Trady(...)rence [34]** |   **167.5 μs** |    **42.00 μs** |   **2.30 μs** |  **1.00** |    **0.02** |   **32.2266** |   **8.0566** |        **-** |  **265.04 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)rence [34] |   536.3 μs |   311.05 μs |  17.05 μs |  3.20 |    0.10 |  110.3516 |  39.0625 |        - |  904.99 KB |        3.41 |
|            |       |                      |            |             |           |       |         |           |          |          |            |             |
| **Ooples**     | **10000** | **Trady(...)rence [34]** | **2,187.3 μs** | **4,820.10 μs** | **264.21 μs** |  **1.01** |    **0.15** |  **535.1563** | **421.8750** | **355.4688** | **3710.32 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)rence [34] | 9,867.4 μs | 8,718.97 μs | 477.92 μs |  4.55 |    0.49 | 1296.8750 | 984.3750 | 609.3750 | 8830.35 KB |        2.38 |
