```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean       | Error     | StdDev   | Gen0     | Gen1     | Allocated  |
|------------------ |------ |--------------------- |-----------:|----------:|---------:|---------:|---------:|-----------:|
| **BuilderFull**       | **1000**  | **Skender.GetDoji**      |   **179.4 μs** |   **7.90 μs** |  **5.23 μs** |  **58.5938** |  **15.6250** |  **503.61 KB** |
| BuilderLatestOnly | 1000  | Skender.GetDoji      |   176.9 μs |  13.15 μs |  8.70 μs |  55.6641 |  13.6719 |     456 KB |
| **BuilderFull**       | **1000**  | **Skend(...)nAshi [21]** |   **128.0 μs** |   **6.97 μs** |  **4.61 μs** |  **10.7422** |   **1.9531** |   **88.48 KB** |
| BuilderLatestOnly | 1000  | Skend(...)nAshi [21] |   119.0 μs |   3.63 μs |  1.90 μs |   4.8828 |        - |   40.86 KB |
| **BuilderFull**       | **1000**  | **Skender.GetMarubozu**  |   **118.9 μs** |  **14.25 μs** |  **9.43 μs** |  **17.5781** |   **3.9063** |   **144.2 KB** |
| BuilderLatestOnly | 1000  | Skender.GetMarubozu  |   102.1 μs |   3.99 μs |  2.37 μs |  11.7188 |   1.9531 |   96.59 KB |
| **BuilderFull**       | **10000** | **Skender.GetDoji**      | **1,684.9 μs** |  **62.99 μs** | **32.94 μs** | **609.3750** | **531.2500** | **5004.54 KB** |
| BuilderLatestOnly | 10000 | Skender.GetDoji      | 1,639.5 μs | 143.61 μs | 94.99 μs | 546.8750 | 500.0000 | 4534.69 KB |
| **BuilderFull**       | **10000** | **Skend(...)nAshi [21]** | **1,223.1 μs** |  **45.93 μs** | **27.33 μs** | **101.5625** |  **46.8750** |  **862.36 KB** |
| BuilderLatestOnly | 10000 | Skend(...)nAshi [21] | 1,172.6 μs |  36.95 μs | 19.32 μs |  46.8750 |  23.4375 |  392.51 KB |
| **BuilderFull**       | **10000** | **Skender.GetMarubozu**  | **1,035.2 μs** |  **40.89 μs** | **27.04 μs** | **171.8750** | **109.3750** | **1410.35 KB** |
| BuilderLatestOnly | 10000 | Skender.GetMarubozu  |   969.8 μs |  49.69 μs | 32.86 μs | 109.3750 |  70.3125 |  940.49 KB |
