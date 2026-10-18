```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation          | Count | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------ |------------------- |------ |------------:|-----------:|-----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetDynamic** | **1000**  |   **913.33 μs** |  **87.117 μs** |  **57.623 μs** | **11.72** |    **0.75** |   **93.7500** |        **-** |        **-** |  **953.69 KB** |       **10.57** |
| BuilderLatestOnly | Skender.GetDynamic | 1000  |   874.08 μs |  27.514 μs |  18.199 μs | 11.22 |    0.32 |  109.3750 |        - |        - |  906.14 KB |       10.04 |
| NativeSingle      | Skender.GetDynamic | 1000  |    77.96 μs |   2.604 μs |   1.722 μs |  1.00 |    0.03 |   10.7422 |   0.9766 |        - |   90.24 KB |        1.00 |
| NativeParallel    | Skender.GetDynamic | 1000  |   144.17 μs |   6.846 μs |   4.074 μs |  1.85 |    0.06 |   59.5703 |  17.5781 |        - |  416.21 KB |        4.61 |
|                   |                    |       |             |            |            |       |         |           |          |          |            |             |
| **BuilderFull**       | **Skender.GetDynamic** | **10000** | **8,997.90 μs** | **345.603 μs** | **205.663 μs** |  **8.84** |    **0.24** | **1090.9091** | **909.0909** |        **-** | **9525.66 KB** |       **10.59** |
| BuilderLatestOnly | Skender.GetDynamic | 10000 | 8,819.47 μs | 342.761 μs | 226.715 μs |  8.66 |    0.25 | 1083.3333 | 333.3333 |        - | 9055.79 KB |       10.06 |
| NativeSingle      | Skender.GetDynamic | 10000 | 1,018.60 μs |  26.464 μs |  17.504 μs |  1.00 |    0.02 |  148.4375 | 101.5625 |  78.1250 |  899.88 KB |        1.00 |
| NativeParallel    | Skender.GetDynamic | 10000 | 3,645.69 μs | 306.043 μs | 182.122 μs |  3.58 |    0.18 |  593.7500 | 531.2500 | 218.7500 | 4133.02 KB |        4.59 |
