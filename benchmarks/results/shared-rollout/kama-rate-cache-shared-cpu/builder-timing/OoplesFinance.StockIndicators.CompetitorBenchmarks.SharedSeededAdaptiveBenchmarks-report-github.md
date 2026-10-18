```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation       | Count | Mean       | Error     | StdDev   | Gen0    | Gen1    | Allocated |
|------------------ |---------------- |------ |-----------:|----------:|---------:|--------:|--------:|----------:|
| **BuilderFull**       | **QuanTAlib.Kama**  | **1000**  |   **382.3 μs** |  **11.68 μs** |  **6.95 μs** |  **9.7656** |  **1.9531** |  **82.04 KB** |
| BuilderLatestOnly | QuanTAlib.Kama  | 1000  |   372.6 μs |   8.66 μs |  5.73 μs |  3.9063 |       - |  34.43 KB |
| **BuilderFull**       | **QuanTAlib.Kama**  | **10000** | **3,790.0 μs** | **156.94 μs** | **93.39 μs** | **93.7500** | **62.5000** | **785.82 KB** |
| BuilderLatestOnly | QuanTAlib.Kama  | 10000 | 3,747.2 μs |  33.20 μs | 17.36 μs | 31.2500 |       - | 315.98 KB |
| **BuilderFull**       | **Skender.GetKama** | **1000**  |   **396.6 μs** |  **21.58 μs** | **12.84 μs** |  **9.7656** |  **1.9531** |  **82.04 KB** |
| BuilderLatestOnly | Skender.GetKama | 1000  |   378.0 μs |  10.95 μs |  7.24 μs |  3.9063 |       - |  34.43 KB |
| **BuilderFull**       | **Skender.GetKama** | **10000** | **3,825.3 μs** |  **79.83 μs** | **47.50 μs** | **93.7500** | **62.5000** | **785.82 KB** |
| BuilderLatestOnly | Skender.GetKama | 10000 | 3,810.3 μs | 138.06 μs | 91.32 μs | 31.2500 |       - | 315.98 KB |
