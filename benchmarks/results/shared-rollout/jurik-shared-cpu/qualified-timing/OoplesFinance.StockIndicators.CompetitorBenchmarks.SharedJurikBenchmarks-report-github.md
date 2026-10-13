```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2   | Allocated   | Alloc Ratio |
|------------------ |------ |-----------:|----------:|----------:|------:|--------:|----------:|---------:|-------:|------------:|------------:|
| **BuilderFull**       | **1000**  |   **166.4 μs** |   **6.57 μs** |   **4.35 μs** |  **0.84** |    **0.07** |    **3.9063** |        **-** |      **-** |     **58.4 KB** |        **0.15** |
| BuilderLatestOnly | 1000  |   159.9 μs |   3.32 μs |   1.97 μs |  0.81 |    0.07 |         - |        - |      - |    10.52 KB |        0.03 |
| NativeSingle      | 1000  |   199.4 μs |  26.06 μs |  17.24 μs |  1.01 |    0.12 |   44.9219 |        - |      - |    382.9 KB |        1.00 |
| NativeParallel    | 1000  |   306.9 μs |  18.63 μs |  11.08 μs |  1.55 |    0.14 |  205.0781 |   7.8125 | 1.9531 |  1677.83 KB |        4.38 |
|                   |       |            |           |           |       |         |           |          |        |             |             |
| **BuilderFull**       | **10000** | **1,573.0 μs** |  **29.10 μs** |  **15.22 μs** |  **0.89** |    **0.01** |   **62.5000** |  **23.4375** |      **-** |   **551.01 KB** |        **0.14** |
| BuilderLatestOnly | 10000 | 1,511.8 μs |  16.64 μs |   9.90 μs |  0.85 |    0.01 |    7.8125 |        - |      - |    80.89 KB |        0.02 |
| NativeSingle      | 10000 | 1,776.8 μs |  45.76 μs |  23.93 μs |  1.00 |    0.02 |  468.7500 |  93.7500 |      - |  3877.53 KB |        1.00 |
| NativeParallel    | 10000 | 3,236.3 μs | 493.47 μs | 326.40 μs |  1.82 |    0.18 | 2093.7500 | 250.0000 |      - | 17156.86 KB |        4.42 |
