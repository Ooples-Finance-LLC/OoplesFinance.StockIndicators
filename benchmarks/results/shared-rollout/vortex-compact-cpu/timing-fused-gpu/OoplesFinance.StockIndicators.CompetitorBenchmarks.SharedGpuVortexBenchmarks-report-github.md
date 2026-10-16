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
| **BuilderFull**       | **Skender.GetVortex** | **1000**  |    **33.25 μs** |  **2.578 μs** |  **1.705 μs** |  **0.47** |    **0.03** |   **9.7656** |   **1.9531** |   **81.25 KB** |        **0.46** |
| BuilderLatestOnly | Skender.GetVortex | 1000  |    29.08 μs |  1.853 μs |  1.226 μs |  0.41 |    0.02 |   3.9063 |   0.2441 |   33.64 KB |        0.19 |
| GpuFull           | Skender.GetVortex | 1000  |   640.20 μs | 46.443 μs | 30.719 μs |  9.10 |    0.47 |   7.8125 |        - |   80.48 KB |        0.46 |
| GpuLatestOnly     | Skender.GetVortex | 1000  |   608.19 μs | 33.349 μs | 22.058 μs |  8.64 |    0.36 |   3.9063 |        - |   32.87 KB |        0.19 |
| NativeSingle      | Skender.GetVortex | 1000  |    70.43 μs |  2.671 μs |  1.767 μs |  1.00 |    0.03 |  21.4844 |   6.8359 |  176.29 KB |        1.00 |
| NativeParallel    | Skender.GetVortex | 1000  |    32.38 μs |  1.461 μs |  0.870 μs |  0.46 |    0.02 |  24.4141 |   6.5918 |  197.27 KB |        1.12 |
|                   |                   |       |             |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetVortex** | **10000** |   **279.51 μs** | **21.486 μs** | **14.212 μs** |  **0.30** |    **0.02** |  **93.7500** |  **46.8750** |  **784.77 KB** |        **0.45** |
| BuilderLatestOnly | Skender.GetVortex | 10000 |   216.08 μs |  3.670 μs |  2.428 μs |  0.23 |    0.01 |  37.1094 |  13.6719 |  314.92 KB |        0.18 |
| GpuFull           | Skender.GetVortex | 10000 | 1,148.98 μs | 33.964 μs | 22.465 μs |  1.23 |    0.06 |  93.7500 |  46.8750 |  784.03 KB |        0.45 |
| GpuLatestOnly     | Skender.GetVortex | 10000 | 1,137.12 μs | 31.629 μs | 20.921 μs |  1.22 |    0.06 |  31.2500 |   7.8125 |  314.17 KB |        0.18 |
| NativeSingle      | Skender.GetVortex | 10000 |   932.34 μs | 77.610 μs | 46.185 μs |  1.00 |    0.07 | 210.9375 | 140.6250 | 1758.38 KB |        1.00 |
| NativeParallel    | Skender.GetVortex | 10000 |   342.15 μs |  7.670 μs |  5.073 μs |  0.37 |    0.02 | 230.4688 | 169.9219 |  1779.3 KB |        1.01 |
