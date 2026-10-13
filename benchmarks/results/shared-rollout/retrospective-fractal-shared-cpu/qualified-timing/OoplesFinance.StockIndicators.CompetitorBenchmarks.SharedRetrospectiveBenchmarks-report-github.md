```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |------ |------------:|----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **1000**  |    **61.07 μs** |  **3.337 μs** |  **1.986 μs** |  **0.16** |    **0.01** |   **9.7656** |   **1.9531** |   **81.98 KB** |        **0.77** |
| BuilderLatestOnly | 1000  |    55.63 μs |  1.834 μs |  1.092 μs |  0.15 |    0.00 |   3.9063 |        - |   34.41 KB |        0.32 |
| NativeSingle      | 1000  |   373.84 μs |  8.639 μs |  5.141 μs |  1.00 |    0.02 |  11.7188 |   1.9531 |  105.87 KB |        1.00 |
| NativeParallel    | 1000  |   109.97 μs |  0.831 μs |  0.495 μs |  0.29 |    0.00 |  18.5547 |   3.9063 |  147.16 KB |        1.39 |
|                   |       |             |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **10000** |   **615.61 μs** | **60.048 μs** | **35.733 μs** |  **0.16** |    **0.01** |  **93.7500** |  **50.7813** |  **785.51 KB** |        **0.74** |
| BuilderLatestOnly | 10000 |   538.88 μs |  9.450 μs |  6.251 μs |  0.14 |    0.00 |  35.1563 |  11.7188 |   315.7 KB |        0.30 |
| NativeSingle      | 10000 | 3,917.29 μs | 25.275 μs | 13.219 μs |  1.00 |    0.00 | 125.0000 |  31.2500 | 1055.31 KB |        1.00 |
| NativeParallel    | 10000 | 1,108.84 μs | 96.830 μs | 64.047 μs |  0.28 |    0.02 | 164.0625 | 156.2500 | 1166.69 KB |        1.11 |
