```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0      | Gen1     | Allocated  | Alloc Ratio |
|------------------ |------ |------------:|-----------:|-----------:|------:|--------:|----------:|---------:|-----------:|------------:|
| **BuilderFull**       | **1000**  |   **770.81 μs** |  **14.191 μs** |   **8.445 μs** | **10.59** |    **0.26** |         **-** |        **-** |   **57.58 KB** |        **0.27** |
| BuilderLatestOnly | 1000  |   768.97 μs |   8.531 μs |   4.462 μs | 10.57 |    0.25 |         - |        - |    9.76 KB |        0.05 |
| NativeSingle      | 1000  |    72.82 μs |   3.450 μs |   1.804 μs |  1.00 |    0.03 |   25.8789 |   0.4883 |  214.78 KB |        1.00 |
| NativeParallel    | 1000  |   144.39 μs |   3.610 μs |   2.148 μs |  1.98 |    0.05 |  114.2578 |   3.9063 |  928.78 KB |        4.32 |
|                   |       |             |            |            |       |         |           |          |            |             |
| **BuilderFull**       | **10000** | **7,751.20 μs** |  **99.662 μs** |  **59.307 μs** | **10.20** |    **0.61** |         **-** |        **-** |  **550.75 KB** |        **0.25** |
| BuilderLatestOnly | 10000 | 7,737.49 μs | 163.840 μs | 108.370 μs | 10.18 |    0.62 |         - |        - |   80.64 KB |        0.04 |
| NativeSingle      | 10000 |   762.53 μs |  75.888 μs |  50.195 μs |  1.00 |    0.09 |  265.6250 |  46.8750 | 2173.04 KB |        1.00 |
| NativeParallel    | 10000 | 1,419.21 μs |  79.633 μs |  52.672 μs |  1.87 |    0.13 | 1164.0625 | 203.1250 | 9494.81 KB |        4.37 |
