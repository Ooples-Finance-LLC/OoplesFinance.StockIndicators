```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation     | Count | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------ |-------------- |------ |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetT3** | **1000**  |  **1,803.00 μs** |  **25.469 μs** |  **15.156 μs** | **33.69** |    **1.03** |        **-** |        **-** |        **-** |  **768.86 KB** |        **8.52** |
| BuilderLatestOnly | Skender.GetT3 | 1000  |  1,843.81 μs |  30.906 μs |  18.392 μs | 34.45 |    1.06 |  78.1250 |        - |        - |  721.32 KB |        7.99 |
| NativeSingle      | Skender.GetT3 | 1000  |     53.57 μs |   2.842 μs |   1.691 μs |  1.00 |    0.04 |  10.7422 |   0.9766 |        - |   90.24 KB |        1.00 |
| NativeParallel    | Skender.GetT3 | 1000  |    120.38 μs |  14.405 μs |   9.528 μs |  2.25 |    0.18 |  59.5703 |  17.5781 |        - |  416.21 KB |        4.61 |
|                   |               |       |              |            |            |       |         |          |          |          |            |             |
| **BuilderFull**       | **Skender.GetT3** | **10000** | **18,560.84 μs** | **484.115 μs** | **288.089 μs** | **24.41** |    **0.45** | **833.3333** | **666.6667** |        **-** | **7661.54 KB** |        **8.51** |
| BuilderLatestOnly | Skender.GetT3 | 10000 | 18,282.58 μs | 316.886 μs | 165.738 μs | 24.04 |    0.34 | 833.3333 | 166.6667 |        - | 7191.78 KB |        7.99 |
| NativeSingle      | Skender.GetT3 | 10000 |    760.50 μs |  15.142 μs |   9.011 μs |  1.00 |    0.02 | 152.3438 | 105.4688 |  82.0313 |  900.08 KB |        1.00 |
| NativeParallel    | Skender.GetT3 | 10000 |  3,692.50 μs |  42.675 μs |  25.395 μs |  4.86 |    0.06 | 593.7500 | 562.5000 | 218.7500 | 4133.88 KB |        4.59 |
