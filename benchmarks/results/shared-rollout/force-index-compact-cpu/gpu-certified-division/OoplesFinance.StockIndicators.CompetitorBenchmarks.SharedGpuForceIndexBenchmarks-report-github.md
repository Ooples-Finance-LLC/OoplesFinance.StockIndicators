```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation            | Count | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------------- |------ |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skend(...)Index [21]** | **1000**  |    **142.19 μs** |   **4.288 μs** |   **2.552 μs** |  **2.50** |    **0.06** |   **7.8125** |        **-** |   **64.81 KB** |        **0.47** |
| BuilderLatestOnly | Skend(...)Index [21] | 1000  |    135.88 μs |   3.189 μs |   2.109 μs |  2.39 |    0.05 |   1.9531 |        - |   17.21 KB |        0.13 |
| GpuFull           | Skend(...)Index [21] | 1000  |  1,982.21 μs |  43.894 μs |  26.121 μs | 34.89 |    0.75 |        - |        - |    64.9 KB |        0.47 |
| GpuLatestOnly     | Skend(...)Index [21] | 1000  |  1,978.62 μs |  58.269 μs |  38.542 μs | 34.83 |    0.89 |        - |        - |    17.3 KB |        0.13 |
| NativeSingle      | Skend(...)Index [21] | 1000  |     56.83 μs |   1.744 μs |   1.038 μs |  1.00 |    0.02 |  16.6016 |   3.9063 |  137.15 KB |        1.00 |
| NativeParallel    | Skend(...)Index [21] | 1000  |    147.71 μs |   6.544 μs |   4.328 μs |  2.60 |    0.09 |  82.0313 |  29.2969 |  627.44 KB |        4.57 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skend(...)Index [21]** | **10000** |  **1,419.55 μs** |  **39.626 μs** |  **23.581 μs** |  **2.07** |    **0.04** |  **70.3125** |  **23.4375** |  **627.75 KB** |        **0.46** |
| BuilderLatestOnly | Skend(...)Index [21] | 10000 |  1,381.09 μs |  42.325 μs |  27.995 μs |  2.02 |    0.05 |  15.6250 |        - |   157.9 KB |        0.12 |
| GpuFull           | Skend(...)Index [21] | 10000 | 17,165.71 μs | 137.788 μs |  81.995 μs | 25.08 |    0.37 |        - |        - |  629.11 KB |        0.46 |
| GpuLatestOnly     | Skend(...)Index [21] | 10000 | 17,337.26 μs | 209.012 μs | 138.249 μs | 25.33 |    0.41 |        - |        - |   159.3 KB |        0.12 |
| NativeSingle      | Skend(...)Index [21] | 10000 |    684.54 μs |  16.958 μs |  10.092 μs |  1.00 |    0.02 | 164.0625 | 156.2500 | 1367.68 KB |        1.00 |
| NativeParallel    | Skend(...)Index [21] | 10000 |  2,407.30 μs | 252.165 μs | 166.791 μs |  3.52 |    0.24 | 781.2500 | 703.1250 | 6234.95 KB |        4.56 |
