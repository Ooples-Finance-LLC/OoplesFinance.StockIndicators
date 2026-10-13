```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0      | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------- |------ |------------:|-----------:|-----------:|------:|--------:|----------:|---------:|-----------:|------------:|
| **BuilderFull**       | **QuanTAlib.Mgdi** | **1000**  |   **889.82 μs** |  **38.785 μs** |  **25.654 μs** | **18.89** |    **0.54** |   **93.7500** |        **-** |  **953.69 KB** |       **20.22** |
| BuilderLatestOnly | QuanTAlib.Mgdi | 1000  |   873.46 μs |  33.731 μs |  20.073 μs | 18.54 |    0.43 |  109.3750 |        - |  906.14 KB |       19.21 |
| NativeSingle      | QuanTAlib.Mgdi | 1000  |    47.10 μs |   0.574 μs |   0.380 μs |  1.00 |    0.01 |    5.6152 |        - |   47.16 KB |        1.00 |
| NativeParallel    | QuanTAlib.Mgdi | 1000  |    70.92 μs |   3.190 μs |   2.110 μs |  1.51 |    0.04 |   22.9492 |   0.4883 |  185.86 KB |        3.94 |
|                   |                |       |             |            |            |       |         |           |          |            |             |
| **BuilderFull**       | **QuanTAlib.Mgdi** | **10000** | **8,726.63 μs** | **219.880 μs** | **145.437 μs** | **18.57** |    **0.34** | **1090.9091** | **909.0909** | **9525.51 KB** |       **20.31** |
| BuilderLatestOnly | QuanTAlib.Mgdi | 10000 | 8,879.20 μs | 382.366 μs | 252.911 μs | 18.90 |    0.54 | 1083.3333 | 333.3333 | 9055.79 KB |       19.31 |
| NativeSingle      | QuanTAlib.Mgdi | 10000 |   469.91 μs |   8.095 μs |   4.234 μs |  1.00 |    0.01 |   54.6875 |   7.8125 |  469.06 KB |        1.00 |
| NativeParallel    | QuanTAlib.Mgdi | 10000 |   633.19 μs |   8.239 μs |   4.903 μs |  1.35 |    0.02 |  222.6563 |  42.9688 | 1838.23 KB |        3.92 |
