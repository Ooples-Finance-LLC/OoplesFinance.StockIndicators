```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation        | Count | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------ |----------------- |------ |-------------:|-----------:|-----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **QuanTAlib.Slope**  | **1000**  |    **327.96 μs** |   **6.947 μs** |   **4.134 μs** |  **2.41** |    **0.17** |         **-** |        **-** |        **-** |  **119.99 KB** |        **0.28** |
| BuilderLatestOnly | QuanTAlib.Slope  | 1000  |    322.47 μs |   3.296 μs |   1.724 μs |  2.37 |    0.17 |    7.8125 |        - |        - |    72.4 KB |        0.17 |
| NativeSingle      | QuanTAlib.Slope  | 1000  |    136.95 μs |  15.961 μs |  10.557 μs |  1.01 |    0.10 |   51.7578 |   6.8359 |        - |  432.23 KB |        1.00 |
| NativeParallel    | QuanTAlib.Slope  | 1000  |     63.63 μs |   2.716 μs |   1.796 μs |  0.47 |    0.04 |   55.6641 |   8.3008 |   0.4883 |  455.65 KB |        1.05 |
|                   |                  |       |              |            |            |       |         |           |          |          |            |             |
| **BuilderFull**       | **QuanTAlib.Slope**  | **10000** |  **2,847.92 μs** |  **39.102 μs** |  **25.864 μs** |  **1.16** |    **0.10** |  **125.0000** |  **78.1250** |        **-** | **1104.91 KB** |        **0.25** |
| BuilderLatestOnly | QuanTAlib.Slope  | 10000 |  2,798.11 μs |  30.952 μs |  20.473 μs |  1.14 |    0.09 |   62.5000 |  31.2500 |        - |  635.06 KB |        0.15 |
| NativeSingle      | QuanTAlib.Slope  | 10000 |  2,464.96 μs | 305.313 μs | 201.946 μs |  1.01 |    0.11 |  578.1250 | 109.3750 | 109.3750 | 4360.29 KB |        1.00 |
| NativeParallel    | QuanTAlib.Slope  | 10000 |  1,787.23 μs | 146.108 μs |  96.642 μs |  0.73 |    0.07 |  578.1250 | 109.3750 | 109.3750 | 4386.98 KB |        1.01 |
|                   |                  |       |              |            |            |       |         |           |          |          |            |             |
| **BuilderFull**       | **Skender.GetSlope** | **1000**  |  **1,561.90 μs** |  **71.600 μs** |  **42.608 μs** | **13.46** |    **0.39** |    **7.8125** |        **-** |        **-** |  **113.39 KB** |        **0.71** |
| BuilderLatestOnly | Skender.GetSlope | 1000  |  1,535.80 μs |  47.646 μs |  31.515 μs | 13.24 |    0.31 |         - |        - |        - |   65.85 KB |        0.41 |
| NativeSingle      | Skender.GetSlope | 1000  |    116.03 μs |   2.895 μs |   1.514 μs |  1.00 |    0.02 |   19.5313 |   3.9063 |        - |  160.55 KB |        1.00 |
| NativeParallel    | Skender.GetSlope | 1000  |    212.77 μs |   5.158 μs |   3.412 μs |  1.83 |    0.04 |   99.6094 |  39.0625 |        - |  732.79 KB |        4.56 |
|                   |                  |       |              |            |            |       |         |           |          |          |            |             |
| **BuilderFull**       | **Skender.GetSlope** | **10000** | **15,642.78 μs** | **805.069 μs** | **532.503 μs** |  **9.78** |    **1.05** |         **-** |        **-** |        **-** | **1099.37 KB** |        **0.69** |
| BuilderLatestOnly | Skender.GetSlope | 10000 | 15,334.56 μs | 362.480 μs | 215.706 μs |  9.59 |    0.99 |         - |        - |        - |  629.55 KB |        0.39 |
| NativeSingle      | Skender.GetSlope | 10000 |  1,614.95 μs | 254.183 μs | 168.126 μs |  1.01 |    0.14 |  234.3750 | 187.5000 |  78.1250 | 1602.77 KB |        1.00 |
| NativeParallel    | Skender.GetSlope | 10000 |  3,750.15 μs | 209.137 μs | 124.454 μs |  2.35 |    0.25 | 1000.0000 | 875.0000 | 218.7500 | 7294.96 KB |        4.55 |
