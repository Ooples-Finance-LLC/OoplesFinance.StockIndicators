```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean        | Error      | StdDev    | Gen0    | Gen1    | Allocated  |
|------------------ |------ |--------------------- |------------:|-----------:|----------:|--------:|--------:|-----------:|
| **BuilderFull**       | **1000**  | **Skender.GetKeltner**   |   **389.30 μs** |  **21.935 μs** | **13.053 μs** |  **7.8125** |       **-** |  **104.67 KB** |
| BuilderLatestOnly | 1000  | Skender.GetKeltner   |   377.38 μs |   3.839 μs |  2.008 μs |  5.8594 |       - |   57.06 KB |
| **BuilderFull**       | **1000**  | **Skend(...)lopes [22]** |    **56.13 μs** |   **3.733 μs** |  **2.221 μs** |  **9.7656** |  **1.9531** |   **86.36 KB** |
| BuilderLatestOnly | 1000  | Skend(...)lopes [22] |    51.75 μs |   2.490 μs |  1.647 μs |  4.3945 |       - |   38.74 KB |
| **BuilderFull**       | **1000**  | **Skend(...)Bands [21]** |   **354.67 μs** |  **11.654 μs** |  **7.709 μs** | **11.7188** |  **1.9531** |   **97.41 KB** |
| BuilderLatestOnly | 1000  | Skend(...)Bands [21] |   346.17 μs |   6.278 μs |  3.736 μs |  5.8594 |       - |   49.79 KB |
| **BuilderFull**       | **1000**  | **Trady(...)nnels [31]** |   **339.08 μs** |  **13.136 μs** |  **8.689 μs** | **11.7188** |  **1.9531** |   **96.84 KB** |
| BuilderLatestOnly | 1000  | Trady(...)nnels [31] |   329.85 μs |   8.027 μs |  4.777 μs |  5.8594 |       - |   49.23 KB |
| **BuilderFull**       | **10000** | **Skender.GetKeltner**   | **3,960.78 μs** | **108.270 μs** | **71.614 μs** | **93.7500** | **31.2500** | **1019.39 KB** |
| BuilderLatestOnly | 10000 | Skender.GetKeltner   | 3,912.17 μs |  38.810 μs | 23.095 μs | 62.5000 | 31.2500 |  549.56 KB |
| **BuilderFull**       | **10000** | **Skend(...)lopes [22]** |   **467.51 μs** |  **23.438 μs** | **15.503 μs** | **93.7500** | **58.5938** |  **789.92 KB** |
| BuilderLatestOnly | 10000 | Skend(...)lopes [22] |   426.45 μs |  16.870 μs | 11.159 μs | 39.0625 | 15.6250 |  320.06 KB |
| **BuilderFull**       | **10000** | **Skend(...)Bands [21]** | **3,542.21 μs** |  **62.161 μs** | **41.115 μs** | **93.7500** | **31.2500** |  **941.81 KB** |
| BuilderLatestOnly | 10000 | Skend(...)Bands [21] | 3,483.16 μs |  77.969 μs | 46.398 μs | 31.2500 |       - |  471.95 KB |
| **BuilderFull**       | **10000** | **Trady(...)nnels [31]** | **3,371.79 μs** | **102.074 μs** | **60.743 μs** | **93.7500** | **31.2500** |  **941.24 KB** |
| BuilderLatestOnly | 10000 | Trady(...)nnels [31] | 3,305.95 μs |  58.630 μs | 38.780 μs | 31.2500 |       - |  471.39 KB |
