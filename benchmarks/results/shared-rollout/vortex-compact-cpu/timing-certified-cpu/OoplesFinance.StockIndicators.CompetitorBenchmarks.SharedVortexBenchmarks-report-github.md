```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=10  LaunchCount=1  UnrollFactor=1  
WarmupCount=10  

```
| Method            | Operation         | Count | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |------------------ |------ |----------:|-----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetVortex** | **1000**  |  **32.90 μs** |   **2.882 μs** |  **1.906 μs** |  **0.47** |    **0.03** |   **9.7656** |   **1.9531** |   **81.25 KB** |        **0.46** |
| BuilderLatestOnly | Skender.GetVortex | 1000  |  25.51 μs |   1.870 μs |  1.113 μs |  0.36 |    0.02 |   3.9063 |   0.2441 |   33.64 KB |        0.19 |
| NativeSingle      | Skender.GetVortex | 1000  |  70.27 μs |   3.489 μs |  2.076 μs |  1.00 |    0.04 |  21.4844 |   6.8359 |  176.28 KB |        1.00 |
| NativeParallel    | Skender.GetVortex | 1000  |  33.07 μs |   5.384 μs |  3.561 μs |  0.47 |    0.05 |  24.6582 |   6.5918 |  197.27 KB |        1.12 |
|                   |                   |       |           |            |           |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetVortex** | **10000** | **230.66 μs** |   **8.153 μs** |  **5.393 μs** |  **0.25** |    **0.02** |  **93.7500** |  **46.8750** |  **784.77 KB** |        **0.45** |
| BuilderLatestOnly | Skender.GetVortex | 10000 | 196.89 μs |   6.099 μs |  4.034 μs |  0.21 |    0.01 |  38.0859 |  12.6953 |  314.92 KB |        0.18 |
| NativeSingle      | Skender.GetVortex | 10000 | 934.15 μs | 110.261 μs | 65.614 μs |  1.00 |    0.09 | 210.9375 | 140.6250 | 1758.37 KB |        1.00 |
| NativeParallel    | Skender.GetVortex | 10000 | 339.39 μs |  12.454 μs |  7.411 μs |  0.36 |    0.02 | 230.4688 | 164.0625 | 1779.29 KB |        1.01 |
