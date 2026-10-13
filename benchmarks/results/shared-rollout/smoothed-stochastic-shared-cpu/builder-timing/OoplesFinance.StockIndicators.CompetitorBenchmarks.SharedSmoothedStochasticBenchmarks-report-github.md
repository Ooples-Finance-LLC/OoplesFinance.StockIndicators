```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean       | Error     | StdDev    | Gen0      | Gen1     | Allocated   |
|------------------ |------ |--------------------- |-----------:|----------:|----------:|----------:|---------:|------------:|
| **BuilderFull**       | **1000**  | **Skender.GetSmi**       |   **538.4 μs** |  **18.95 μs** |  **11.28 μs** |         **-** |        **-** |    **81.83 KB** |
| BuilderLatestOnly | 1000  | Skender.GetSmi       |   541.0 μs |  16.84 μs |  11.14 μs |    3.9063 |        - |    34.25 KB |
| **BuilderFull**       | **1000**  | **Skender.GetStoch**     |   **785.9 μs** |  **29.98 μs** |  **19.83 μs** |  **179.6875** |  **39.0625** |   **1479.4 KB** |
| BuilderLatestOnly | 1000  | Skender.GetStoch     |   822.0 μs |  69.27 μs |  45.82 μs |  175.7813 |  27.3438 |  1431.75 KB |
| **BuilderFull**       | **1000**  | **Trady(...)Index [40]** |   **555.9 μs** |  **28.38 μs** |  **16.89 μs** |    **7.8125** |        **-** |    **81.86 KB** |
| BuilderLatestOnly | 1000  | Trady(...)Index [40] |   592.2 μs |  56.77 μs |  37.55 μs |    3.9063 |        - |    34.25 KB |
| **BuilderFull**       | **10000** | **Skender.GetSmi**       | **6,695.2 μs** | **279.56 μs** | **184.91 μs** |   **66.6667** |        **-** |   **789.33 KB** |
| BuilderLatestOnly | 10000 | Skender.GetSmi       | 6,366.8 μs | 590.00 μs | 351.10 μs |         - |        - |   319.44 KB |
| **BuilderFull**       | **10000** | **Skender.GetStoch**     | **8,025.2 μs** | **412.38 μs** | **245.40 μs** | **1833.3333** | **333.3333** | **14841.92 KB** |
| BuilderLatestOnly | 10000 | Skender.GetStoch     | 7,708.8 μs | 470.31 μs | 279.87 μs | 1714.2857 | 357.1429 | 14372.03 KB |
| **BuilderFull**       | **10000** | **Trady(...)Index [40]** | **5,883.6 μs** | **290.85 μs** | **173.08 μs** |   **93.7500** |  **62.5000** |   **789.03 KB** |
| BuilderLatestOnly | 10000 | Trady(...)Index [40] | 5,729.6 μs | 244.75 μs | 145.65 μs |   31.2500 |        - |   319.19 KB |
