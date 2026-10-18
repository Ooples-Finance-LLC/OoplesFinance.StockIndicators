```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error    | StdDev   | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |----------:|---------:|---------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **TaLib(...)kiGap [23]** |  **24.49 μs** | **1.773 μs** | **1.173 μs** |  **7.8125** |  **0.9766** |  **64.62 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)kiGap [23] |  18.80 μs | 0.443 μs | 0.264 μs |  2.0752 |       - |  16.73 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)istar [21]** |  **35.22 μs** | **4.163 μs** | **2.754 μs** |  **8.7891** |  **1.4648** |   **70.8 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)istar [21] |  28.13 μs | 1.249 μs | 0.743 μs |  2.6855 |       - |  22.92 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)thods [39]** |  **19.83 μs** | **1.463 μs** | **0.871 μs** |  **7.2021** |  **1.0986** |  **59.73 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)thods [39] |  13.05 μs | 0.447 μs | 0.234 μs |  1.4648 |       - |  11.85 KB |
| **BuilderFull**       | **10000** | **TaLib(...)kiGap [23]** | **175.21 μs** | **9.593 μs** | **6.345 μs** | **67.3828** | **22.4609** | **557.18 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)kiGap [23] | 140.28 μs | 7.543 μs | 4.989 μs |  9.7656 |  1.9531 |  87.05 KB |
| **BuilderFull**       | **10000** | **TaLib(...)istar [21]** | **235.13 μs** | **4.585 μs** | **3.033 μs** | **68.3594** | **35.1563** | **563.37 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)istar [21] | 198.19 μs | 2.041 μs | 1.350 μs | 10.7422 |  0.9766 |  93.24 KB |
| **BuilderFull**       | **10000** | **TaLib(...)thods [39]** | **129.71 μs** | **2.702 μs** | **1.608 μs** | **67.3828** | **22.4609** | **552.29 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)thods [39] |  86.58 μs | 2.026 μs | 1.205 μs |  9.7656 |  1.9531 |  82.17 KB |
