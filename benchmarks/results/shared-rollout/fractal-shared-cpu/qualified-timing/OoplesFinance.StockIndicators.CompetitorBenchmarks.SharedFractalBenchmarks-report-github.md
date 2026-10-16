```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------ |------ |-----------:|---------:|---------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **BuilderFull**       | **1000**  |   **202.9 μs** | **19.96 μs** | **11.88 μs** |  **1.42** |    **0.08** |   **3.9063** |       **-** |   **56.94 KB** |        **1.20** |
| BuilderLatestOnly | 1000  |   198.0 μs |  7.71 μs |  5.10 μs |  1.39 |    0.04 |   0.9766 |       - |    9.06 KB |        0.19 |
| NativeSingle      | 1000  |   142.7 μs |  3.53 μs |  2.33 μs |  1.00 |    0.02 |   4.8828 |       - |   47.38 KB |        1.00 |
| NativeParallel    | 1000  |   225.9 μs | 11.29 μs |  7.47 μs |  1.58 |    0.06 |  21.4844 |       - |   187.6 KB |        3.96 |
|                   |       |            |          |          |       |         |          |         |            |             |
| **BuilderFull**       | **10000** | **1,937.8 μs** | **22.78 μs** | **11.91 μs** |  **1.31** |    **0.05** |  **62.5000** | **31.2500** |  **549.62 KB** |        **1.17** |
| BuilderLatestOnly | 10000 | 1,933.0 μs | 37.83 μs | 22.51 μs |  1.31 |    0.05 |        - |       - |   79.49 KB |        0.17 |
| NativeSingle      | 10000 | 1,480.2 μs | 84.54 μs | 55.92 μs |  1.00 |    0.05 |  54.6875 |  7.8125 |  469.31 KB |        1.00 |
| NativeParallel    | 10000 | 2,182.4 μs | 99.96 μs | 66.12 μs |  1.48 |    0.07 | 218.7500 | 31.2500 | 1840.02 KB |        3.92 |
