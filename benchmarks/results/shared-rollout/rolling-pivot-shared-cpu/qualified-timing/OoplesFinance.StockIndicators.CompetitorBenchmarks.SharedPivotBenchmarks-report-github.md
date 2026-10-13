```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |------ |-----------:|----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **1000**  |   **474.3 μs** |  **26.43 μs** |  **17.48 μs** |  **0.89** |    **0.04** |  **15.6250** |        **-** |  **193.27 KB** |        **0.39** |
| BuilderLatestOnly | 1000  |   462.6 μs |   5.12 μs |   2.68 μs |  0.86 |    0.02 |  15.6250 |   3.9063 |  145.68 KB |        0.29 |
| NativeSingle      | 1000  |   536.1 μs |  24.03 μs |  14.30 μs |  1.00 |    0.04 |  58.5938 |  19.5313 |  499.64 KB |        1.00 |
| NativeParallel    | 1000  |   156.2 μs |   2.16 μs |   1.28 μs |  0.29 |    0.01 |  67.3828 |  24.4141 |  549.14 KB |        1.10 |
|                   |       |            |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **10000** | **4,773.4 μs** | **155.38 μs** | **102.77 μs** |  **0.79** |    **0.04** | **218.7500** | **125.0000** | **1881.46 KB** |        **0.37** |
| BuilderLatestOnly | 10000 | 4,716.9 μs | 184.23 μs | 109.63 μs |  0.79 |    0.04 | 156.2500 |  93.7500 | 1411.62 KB |        0.28 |
| NativeSingle      | 10000 | 6,020.0 μs | 415.41 μs | 274.77 μs |  1.00 |    0.06 | 562.5000 | 437.5000 | 5035.36 KB |        1.00 |
| NativeParallel    | 10000 | 1,796.1 μs |  45.28 μs |  29.95 μs |  0.30 |    0.01 | 656.2500 | 484.3750 | 5154.97 KB |        1.02 |
