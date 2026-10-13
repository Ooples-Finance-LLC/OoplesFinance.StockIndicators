```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev   | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |----------:|----------:|---------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **TaLib(...)kaway [23]** |  **36.56 μs** |  **3.976 μs** | **2.630 μs** |  **8.7891** |  **0.9766** |  **72.05 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)kaway [23] |  29.14 μs |  0.715 μs | 0.426 μs |  3.1738 |       - |  24.17 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)allow [35]** |  **29.37 μs** |  **0.814 μs** | **0.426 μs** |  **9.0332** |  **1.4648** |  **72.05 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)allow [35] |  25.10 μs |  1.018 μs | 0.673 μs |  3.1738 |       - |  24.17 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)ottom [26]** |  **29.51 μs** |  **0.682 μs** | **0.357 μs** |  **9.0332** |  **1.4648** |  **72.05 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)ottom [26] |  24.85 μs |  0.986 μs | 0.652 μs |  3.1738 |       - |  24.17 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)tHold [21]** |  **46.84 μs** |  **0.955 μs** | **0.632 μs** | **10.4980** |  **1.7090** |   **84.5 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)tHold [21] |  41.97 μs |  0.773 μs | 0.460 μs |  4.8828 |       - |  36.61 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)thods [39]** |  **46.68 μs** |  **0.618 μs** | **0.323 μs** | **10.4980** |  **1.7090** |  **84.49 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)thods [39] |  42.52 μs |  1.267 μs | 0.754 μs |  4.8828 |  0.2441 |  36.61 KB |
| **BuilderFull**       | **10000** | **TaLib(...)kaway [23]** | **243.70 μs** |  **2.839 μs** | **1.878 μs** | **68.3594** | **35.1563** | **564.62 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)kaway [23] | 215.44 μs |  5.268 μs | 3.485 μs | 11.7188 |  1.9531 |   94.5 KB |
| **BuilderFull**       | **10000** | **TaLib(...)allow [35]** | **211.28 μs** | **11.237 μs** | **7.432 μs** | **68.3594** | **35.1563** | **564.62 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)allow [35] | 171.62 μs |  2.906 μs | 1.729 μs | 11.7188 |  1.9531 |  94.49 KB |
| **BuilderFull**       | **10000** | **TaLib(...)ottom [26]** | **202.23 μs** |  **4.196 μs** | **2.497 μs** | **68.3594** | **35.1563** | **564.62 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)ottom [26] | 170.93 μs |  1.593 μs | 1.054 μs | 11.7188 |  1.9531 |  94.49 KB |
| **BuilderFull**       | **10000** | **TaLib(...)tHold [21]** | **352.94 μs** |  **9.605 μs** | **5.716 μs** | **70.3125** | **27.3438** | **577.07 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)tHold [21] | 309.85 μs |  4.094 μs | 2.708 μs | 11.7188 |  1.9531 | 106.95 KB |
| **BuilderFull**       | **10000** | **TaLib(...)thods [39]** | **344.62 μs** |  **3.709 μs** | **1.940 μs** | **70.3125** | **27.3438** | **577.06 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)thods [39] | 317.76 μs | 16.633 μs | 9.898 μs | 11.7188 |  1.9531 | 106.94 KB |
