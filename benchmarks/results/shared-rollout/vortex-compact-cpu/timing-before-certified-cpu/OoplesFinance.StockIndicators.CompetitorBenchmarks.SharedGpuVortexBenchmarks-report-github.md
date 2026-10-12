```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=10  LaunchCount=1  UnrollFactor=1  
WarmupCount=10  

```
| Method            | Operation         | Count | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |------------------ |------ |------------:|----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetVortex** | **1000**  |    **87.12 μs** |  **5.261 μs** |  **3.480 μs** |  **1.17** |    **0.05** |  **11.7188** |   **1.9531** |   **99.25 KB** |        **0.56** |
| BuilderLatestOnly | Skender.GetVortex | 1000  |    77.96 μs |  5.368 μs |  3.551 μs |  1.05 |    0.05 |   6.8359 |   0.4883 |   51.64 KB |        0.29 |
| GpuFull           | Skender.GetVortex | 1000  |   741.48 μs |  8.921 μs |  5.901 μs |  9.95 |    0.15 |   7.8125 |        - |   80.55 KB |        0.46 |
| GpuLatestOnly     | Skender.GetVortex | 1000  |   732.41 μs |  9.708 μs |  5.078 μs |  9.83 |    0.14 |   3.9063 |        - |   32.94 KB |        0.19 |
| NativeSingle      | Skender.GetVortex | 1000  |    74.54 μs |  1.885 μs |  0.986 μs |  1.00 |    0.02 |  21.4844 |   6.8359 |  176.28 KB |        1.00 |
| NativeParallel    | Skender.GetVortex | 1000  |    34.16 μs |  3.529 μs |  2.100 μs |  0.46 |    0.03 |  24.6582 |   6.8359 |  197.27 KB |        1.12 |
|                   |                   |       |             |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetVortex** | **10000** |   **670.23 μs** | **27.380 μs** | **18.110 μs** |  **0.70** |    **0.03** |  **97.6563** |  **62.5000** |  **802.79 KB** |        **0.46** |
| BuilderLatestOnly | Skender.GetVortex | 10000 |   629.68 μs |  6.931 μs |  4.124 μs |  0.66 |    0.02 |  39.0625 |  11.7188 |  332.94 KB |        0.19 |
| GpuFull           | Skender.GetVortex | 10000 | 1,442.06 μs | 63.796 μs | 42.197 μs |  1.51 |    0.06 |  93.7500 |  46.8750 |  784.09 KB |        0.45 |
| GpuLatestOnly     | Skender.GetVortex | 10000 | 1,357.56 μs | 25.102 μs | 16.603 μs |  1.42 |    0.05 |  31.2500 |   7.8125 |  314.24 KB |        0.18 |
| NativeSingle      | Skender.GetVortex | 10000 |   957.09 μs | 61.349 μs | 32.087 μs |  1.00 |    0.05 | 210.9375 | 140.6250 | 1758.38 KB |        1.00 |
| NativeParallel    | Skender.GetVortex | 10000 |   384.20 μs | 37.207 μs | 24.610 μs |  0.40 |    0.03 | 230.4688 | 164.0625 | 1779.31 KB |        1.01 |
