```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation            | Count | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------------- |------ |-------------:|-----------:|-----------:|-------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skend(...)Index [21]** | **1000**  |    **139.53 μs** |   **2.343 μs** |   **1.394 μs** |   **2.46** |    **0.10** |   **7.8125** |   **0.9766** |   **64.81 KB** |        **0.47** |
| BuilderLatestOnly | Skend(...)Index [21] | 1000  |    136.06 μs |   3.866 μs |   2.301 μs |   2.40 |    0.10 |   1.9531 |        - |   17.21 KB |        0.13 |
| GpuFull           | Skend(...)Index [21] | 1000  |  6,668.41 μs |  11.612 μs |   6.910 μs | 117.44 |    4.57 |        - |        - |   65.37 KB |        0.48 |
| GpuLatestOnly     | Skend(...)Index [21] | 1000  |  6,662.68 μs |  25.113 μs |  16.611 μs | 117.34 |    4.57 |        - |        - |   17.73 KB |        0.13 |
| NativeSingle      | Skend(...)Index [21] | 1000  |     56.87 μs |   4.075 μs |   2.425 μs |   1.00 |    0.06 |  16.6016 |   3.9063 |  137.15 KB |        1.00 |
| NativeParallel    | Skend(...)Index [21] | 1000  |    171.44 μs |   8.664 μs |   5.156 μs |   3.02 |    0.15 |  83.9844 |  31.2500 |  627.43 KB |        4.57 |
|                   |                      |       |              |            |            |        |         |          |          |            |             |
| **BuilderFull**       | **Skend(...)Index [21]** | **10000** |  **1,518.41 μs** | **175.954 μs** | **116.383 μs** |   **2.10** |    **0.16** |  **70.3125** |  **23.4375** |  **627.75 KB** |        **0.46** |
| BuilderLatestOnly | Skend(...)Index [21] | 10000 |  1,714.73 μs |  61.048 μs |  40.379 μs |   2.37 |    0.07 |  15.6250 |        - |   157.9 KB |        0.12 |
| GpuFull           | Skend(...)Index [21] | 10000 | 64,399.06 μs | 147.862 μs |  77.334 μs |  89.04 |    1.84 |        - |        - |  629.88 KB |        0.46 |
| GpuLatestOnly     | Skend(...)Index [21] | 10000 | 64,290.79 μs | 125.428 μs |  82.963 μs |  88.89 |    1.83 |        - |        - |  159.99 KB |        0.12 |
| NativeSingle      | Skend(...)Index [21] | 10000 |    723.56 μs |  26.261 μs |  15.627 μs |   1.00 |    0.03 | 164.0625 | 160.1563 | 1367.65 KB |        1.00 |
| NativeParallel    | Skend(...)Index [21] | 10000 |  2,720.28 μs | 281.772 μs | 186.374 μs |   3.76 |    0.26 | 781.2500 | 687.5000 | 6234.96 KB |        4.56 |
