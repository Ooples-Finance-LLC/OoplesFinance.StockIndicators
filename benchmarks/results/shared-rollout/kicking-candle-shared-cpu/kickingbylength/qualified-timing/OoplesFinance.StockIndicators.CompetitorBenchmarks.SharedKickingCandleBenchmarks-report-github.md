```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------ |------ |--------------------- |------------:|----------:|----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **BuilderFull**       | **1000**  | **TaLib(...)cking [21]** |    **44.24 μs** |  **1.982 μs** |  **1.037 μs** |  **0.50** |    **0.02** |  **9.7656** |  **0.9766** |  **82.18 KB** |       **20.67** |
| BuilderLatestOnly | 1000  | TaLib(...)cking [21] |    38.89 μs |  1.780 μs |  1.059 μs |  0.44 |    0.02 |  4.3945 |  0.2441 |  34.29 KB |        8.62 |
| NativeSingle      | 1000  | TaLib(...)cking [21] |    87.77 μs |  4.936 μs |  3.265 μs |  1.00 |    0.05 |       - |       - |   3.98 KB |        1.00 |
| NativeParallel    | 1000  | TaLib(...)cking [21] |    27.99 μs |  0.967 μs |  0.575 μs |  0.32 |    0.01 |  0.9766 |       - |    8.8 KB |        2.21 |
|                   |       |                      |             |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **1000**  | **TaLib(...)ength [29]** |    **45.41 μs** |  **1.235 μs** |  **0.735 μs** |  **0.43** |    **0.01** | **10.2539** |  **1.4648** |  **82.18 KB** |       **20.27** |
| BuilderLatestOnly | 1000  | TaLib(...)ength [29] |    37.03 μs |  2.422 μs |  1.441 μs |  0.35 |    0.02 |  4.3945 |  0.2441 |  34.29 KB |        8.46 |
| NativeSingle      | 1000  | TaLib(...)ength [29] |   105.16 μs |  3.829 μs |  2.532 μs |  1.00 |    0.03 |       - |       - |   4.05 KB |        1.00 |
| NativeParallel    | 1000  | TaLib(...)ength [29] |    28.04 μs |  0.966 μs |  0.575 μs |  0.27 |    0.01 |  0.9766 |       - |    8.8 KB |        2.17 |
|                   |       |                      |             |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **TaLib(...)cking [21]** |   **294.73 μs** |  **7.579 μs** |  **5.013 μs** |  **0.34** |    **0.01** | **68.3594** | **29.2969** | **574.75 KB** |       **14.67** |
| BuilderLatestOnly | 10000 | TaLib(...)cking [21] |   255.46 μs |  4.851 μs |  3.209 μs |  0.29 |    0.01 | 11.7188 |  1.9531 | 104.63 KB |        2.67 |
| NativeSingle      | 10000 | TaLib(...)cking [21] |   873.38 μs | 47.348 μs | 31.318 μs |  1.00 |    0.05 |       - |       - |  39.17 KB |        1.00 |
| NativeParallel    | 10000 | TaLib(...)cking [21] |   246.05 μs |  3.059 μs |  1.821 μs |  0.28 |    0.01 | 11.7188 |  1.9531 |  79.15 KB |        2.02 |
|                   |       |                      |             |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **TaLib(...)ength [29]** |   **301.47 μs** |  **9.883 μs** |  **6.537 μs** |  **0.29** |    **0.01** | **68.3594** | **29.2969** | **574.75 KB** |       **14.64** |
| BuilderLatestOnly | 10000 | TaLib(...)ength [29] |   253.62 μs |  2.209 μs |  1.314 μs |  0.24 |    0.00 | 11.7188 |  1.9531 | 104.63 KB |        2.66 |
| NativeSingle      | 10000 | TaLib(...)ength [29] | 1,046.10 μs | 24.976 μs | 13.063 μs |  1.00 |    0.02 |       - |       - |  39.27 KB |        1.00 |
| NativeParallel    | 10000 | TaLib(...)ength [29] |   246.57 μs |  3.884 μs |  2.569 μs |  0.24 |    0.00 | 11.7188 |  1.9531 |  79.15 KB |        2.02 |
