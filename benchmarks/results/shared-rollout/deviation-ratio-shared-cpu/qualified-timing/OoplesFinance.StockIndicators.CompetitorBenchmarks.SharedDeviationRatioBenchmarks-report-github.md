```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1     | Allocated   | Alloc Ratio |
|------------------ |------ |------------:|------------:|------------:|------:|--------:|----------:|---------:|------------:|------------:|
| **BuilderFull**       | **1000**  |    **563.8 μs** |    **25.93 μs** |    **15.43 μs** |  **0.18** |    **0.01** |   **15.6250** |        **-** |   **154.02 KB** |        **0.17** |
| BuilderLatestOnly | 1000  |    547.8 μs |    14.90 μs |     8.87 μs |  0.18 |    0.01 |   11.7188 |        - |   106.44 KB |        0.12 |
| NativeSingle      | 1000  |  3,079.5 μs |   193.36 μs |   127.90 μs |  1.00 |    0.06 |  109.3750 |        - |   919.97 KB |        1.00 |
| NativeParallel    | 1000  |  4,281.6 μs |   131.25 μs |    86.81 μs |  1.39 |    0.06 |  468.7500 |        - |  3892.17 KB |        4.23 |
|                   |       |             |             |             |       |         |           |          |             |             |
| **BuilderFull**       | **10000** |  **5,775.3 μs** |   **181.96 μs** |    **95.17 μs** |  **0.18** |    **0.01** |  **187.5000** | **156.2500** |  **1580.42 KB** |        **0.16** |
| BuilderLatestOnly | 10000 |  5,621.9 μs |   136.03 μs |    80.95 μs |  0.17 |    0.01 |  125.0000 |  31.2500 |   1110.6 KB |        0.11 |
| NativeSingle      | 10000 | 33,003.4 μs | 2,238.98 μs | 1,480.95 μs |  1.00 |    0.06 | 1000.0000 |        - |  9767.68 KB |        1.00 |
| NativeParallel    | 10000 | 45,892.4 μs | 1,557.73 μs | 1,030.34 μs |  1.39 |    0.06 | 5250.0000 | 250.0000 | 43454.74 KB |        4.45 |
