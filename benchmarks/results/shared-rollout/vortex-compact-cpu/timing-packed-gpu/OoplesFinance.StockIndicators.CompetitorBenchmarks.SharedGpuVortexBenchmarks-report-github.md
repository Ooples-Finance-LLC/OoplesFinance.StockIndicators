```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=10  LaunchCount=1  UnrollFactor=1  
WarmupCount=10  

```
| Method            | Operation         | Count | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |------------------ |------ |----------:|----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetVortex** | **1000**  |  **33.67 μs** |  **1.486 μs** |  **0.983 μs** |  **0.47** |    **0.02** |   **9.7656** |   **1.9531** |   **81.25 KB** |        **0.46** |
| BuilderLatestOnly | Skender.GetVortex | 1000  |  29.45 μs |  1.804 μs |  1.193 μs |  0.41 |    0.02 |   3.9063 |   0.2441 |   33.64 KB |        0.19 |
| GpuFull           | Skender.GetVortex | 1000  | 325.16 μs | 23.282 μs | 15.399 μs |  4.58 |    0.24 |   9.7656 |   1.9531 |   80.44 KB |        0.46 |
| GpuLatestOnly     | Skender.GetVortex | 1000  | 308.70 μs | 16.096 μs | 10.646 μs |  4.35 |    0.19 |   3.9063 |        - |   32.82 KB |        0.19 |
| NativeSingle      | Skender.GetVortex | 1000  |  71.09 μs |  3.134 μs |  2.073 μs |  1.00 |    0.04 |  21.4844 |   6.8359 |  176.29 KB |        1.00 |
| NativeParallel    | Skender.GetVortex | 1000  |  28.83 μs |  2.352 μs |  1.400 μs |  0.41 |    0.02 |  24.4141 |   6.8359 |  197.27 KB |        1.12 |
|                   |                   |       |           |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetVortex** | **10000** | **255.52 μs** | **11.463 μs** |  **7.582 μs** |  **0.28** |    **0.01** |  **93.7500** |  **46.8750** |  **784.77 KB** |        **0.45** |
| BuilderLatestOnly | Skender.GetVortex | 10000 | 225.72 μs | 10.655 μs |  7.048 μs |  0.24 |    0.01 |  37.1094 |  13.6719 |  314.92 KB |        0.18 |
| GpuFull           | Skender.GetVortex | 10000 | 751.80 μs | 14.240 μs |  9.419 μs |  0.81 |    0.03 |  93.7500 |  46.8750 |     784 KB |        0.45 |
| GpuLatestOnly     | Skender.GetVortex | 10000 | 706.44 μs | 11.855 μs |  7.841 μs |  0.76 |    0.02 |  35.1563 |  11.7188 |  314.11 KB |        0.18 |
| NativeSingle      | Skender.GetVortex | 10000 | 926.11 μs | 42.764 μs | 28.286 μs |  1.00 |    0.04 | 210.9375 | 140.6250 | 1758.38 KB |        1.00 |
| NativeParallel    | Skender.GetVortex | 10000 | 355.30 μs | 19.207 μs | 12.704 μs |  0.38 |    0.02 | 230.4688 | 164.0625 |  1779.3 KB |        1.01 |
