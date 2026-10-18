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
| **BuilderFull**       | **Skender.GetVortex** | **1000**  |  **29.88 μs** |  **2.457 μs** |  **1.285 μs** |  **0.42** |    **0.02** |   **9.7656** |   **1.9531** |   **81.21 KB** |        **0.46** |
| BuilderLatestOnly | Skender.GetVortex | 1000  |  29.79 μs |  1.799 μs |  1.190 μs |  0.42 |    0.02 |   3.9063 |   0.2441 |   33.64 KB |        0.19 |
| GpuFull           | Skender.GetVortex | 1000  | 311.88 μs |  4.886 μs |  2.908 μs |  4.43 |    0.11 |   9.7656 |   1.9531 |    80.4 KB |        0.46 |
| GpuLatestOnly     | Skender.GetVortex | 1000  | 311.89 μs | 12.919 μs |  8.545 μs |  4.43 |    0.15 |   3.9063 |        - |   32.83 KB |        0.19 |
| NativeSingle      | Skender.GetVortex | 1000  |  70.49 μs |  2.568 μs |  1.698 μs |  1.00 |    0.03 |  21.4844 |   6.8359 |  176.29 KB |        1.00 |
| NativeParallel    | Skender.GetVortex | 1000  |  28.19 μs |  1.486 μs |  0.777 μs |  0.40 |    0.01 |  24.6582 |   6.5918 |  197.27 KB |        1.12 |
|                   |                   |       |           |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetVortex** | **10000** | **218.11 μs** |  **9.253 μs** |  **6.120 μs** |  **0.26** |    **0.01** |  **93.7500** |  **46.8750** |  **784.73 KB** |        **0.45** |
| BuilderLatestOnly | Skender.GetVortex | 10000 | 184.69 μs |  5.017 μs |  3.318 μs |  0.22 |    0.01 |  37.1094 |  13.6719 |  314.92 KB |        0.18 |
| GpuFull           | Skender.GetVortex | 10000 | 773.35 μs |  9.394 μs |  4.913 μs |  0.91 |    0.02 |  93.7500 |  46.8750 |  783.92 KB |        0.45 |
| GpuLatestOnly     | Skender.GetVortex | 10000 | 710.22 μs | 10.629 μs |  7.031 μs |  0.84 |    0.02 |  35.1563 |  11.7188 |  314.11 KB |        0.18 |
| NativeSingle      | Skender.GetVortex | 10000 | 849.77 μs | 31.427 μs | 16.437 μs |  1.00 |    0.03 | 210.9375 | 140.6250 | 1758.38 KB |        1.00 |
| NativeParallel    | Skender.GetVortex | 10000 | 342.83 μs | 17.465 μs |  9.135 μs |  0.40 |    0.01 | 230.4688 | 167.9688 |  1779.3 KB |        1.01 |
