```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation       | Count | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2     | Allocated  | Alloc Ratio |
|------------------ |---------------- |------ |------------:|----------:|----------:|------:|--------:|----------:|----------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetMama** | **1000**  |  **1,341.2 μs** |  **29.23 μs** |  **19.33 μs** | **10.30** |    **0.26** |         **-** |         **-** |        **-** |   **82.71 KB** |        **0.43** |
| BuilderLatestOnly | Skender.GetMama | 1000  |  1,297.9 μs |  27.84 μs |  18.42 μs |  9.97 |    0.25 |         - |         - |        - |   35.06 KB |        0.18 |
| NativeSingle      | Skender.GetMama | 1000  |    130.3 μs |   5.06 μs |   3.01 μs |  1.00 |    0.03 |   23.4375 |    4.8828 |        - |  192.06 KB |        1.00 |
| NativeParallel    | Skender.GetMama | 1000  |    242.2 μs |  18.67 μs |  12.35 μs |  1.86 |    0.10 |  121.0938 |   44.9219 |        - |  875.31 KB |        4.56 |
|                   |                 |       |             |           |           |       |         |           |           |          |            |             |
| **BuilderFull**       | **Skender.GetMama** | **10000** | **13,007.6 μs** | **740.55 μs** | **387.32 μs** |  **8.05** |    **0.42** |         **-** |         **-** |        **-** |  **787.11 KB** |        **0.41** |
| BuilderLatestOnly | Skender.GetMama | 10000 | 13,151.0 μs | 916.76 μs | 606.38 μs |  8.13 |    0.51 |         - |         - |        - |   317.3 KB |        0.17 |
| NativeSingle      | Skender.GetMama | 10000 |  1,620.0 μs | 116.04 μs |  76.75 μs |  1.00 |    0.06 |  265.6250 |  218.7500 |  78.1250 | 1915.44 KB |        1.00 |
| NativeParallel    | Skender.GetMama | 10000 |  3,952.3 μs | 623.71 μs | 371.16 μs |  2.44 |    0.24 | 1156.2500 | 1062.5000 | 218.7500 | 8702.26 KB |        4.54 |
