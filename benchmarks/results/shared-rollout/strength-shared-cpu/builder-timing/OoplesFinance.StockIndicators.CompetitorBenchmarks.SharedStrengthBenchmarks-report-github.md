```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean        | Error     | StdDev    | Gen0     | Gen1    | Allocated  |
|------------------ |------ |--------------------- |------------:|----------:|----------:|---------:|--------:|-----------:|
| **BuilderFull**       | **1000**  | **Skender.GetAdx**       |  **1,358.8 μs** |  **16.54 μs** |   **9.84 μs** |   **7.8125** |       **-** |  **113.74 KB** |
| BuilderLatestOnly | 1000  | Skender.GetAdx       |  1,356.3 μs |  26.26 μs |  15.63 μs |   7.8125 |       - |   66.19 KB |
| **BuilderFull**       | **1000**  | **Skender.GetRsi**       |    **173.3 μs** |   **4.36 μs** |   **2.89 μs** |   **7.8125** |  **0.9766** |   **64.84 KB** |
| BuilderLatestOnly | 1000  | Skender.GetRsi       |    170.6 μs |   6.71 μs |   4.44 μs |   1.9531 |       - |   17.22 KB |
| **BuilderFull**       | **1000**  | **Trady(...)lator [37]** |    **182.1 μs** |   **2.05 μs** |   **1.07 μs** |   **7.8125** |       **-** |   **65.06 KB** |
| BuilderLatestOnly | 1000  | Trady(...)lator [37] |    193.5 μs |  10.84 μs |   7.17 μs |   1.9531 |       - |   17.43 KB |
| **BuilderFull**       | **1000**  | **Trady(...)entum [32]** |    **197.2 μs** |   **9.74 μs** |   **6.44 μs** |   **7.8125** |       **-** |    **65.2 KB** |
| BuilderLatestOnly | 1000  | Trady(...)entum [32] |    194.3 μs |   2.09 μs |   1.09 μs |   1.9531 |       - |   17.58 KB |
| **BuilderFull**       | **1000**  | **Trady(...)Index [37]** |    **208.1 μs** |   **9.38 μs** |   **4.90 μs** |   **7.8125** |       **-** |   **65.21 KB** |
| BuilderLatestOnly | 1000  | Trady(...)Index [37] |    190.2 μs |   1.18 μs |   0.62 μs |   1.9531 |       - |   17.59 KB |
| **BuilderFull**       | **1000**  | **Trady(...)ength [32]** |    **195.9 μs** |   **2.02 μs** |   **1.20 μs** |   **7.8125** |       **-** |   **65.05 KB** |
| BuilderLatestOnly | 1000  | Trady(...)ength [32] |    191.4 μs |   6.82 μs |   3.56 μs |   1.9531 |       - |   17.44 KB |
| **BuilderFull**       | **1000**  | **Trady(...)Index [37]** |    **184.4 μs** |   **3.81 μs** |   **2.52 μs** |   **7.8125** |  **0.9766** |   **65.05 KB** |
| BuilderLatestOnly | 1000  | Trady(...)Index [37] |    186.5 μs |   4.66 μs |   2.44 μs |   1.9531 |       - |   17.43 KB |
| **BuilderFull**       | **10000** | **Skender.GetAdx**       | **14,063.1 μs** | **267.49 μs** | **176.93 μs** | **125.0000** |       **-** | **1099.62 KB** |
| BuilderLatestOnly | 10000 | Skender.GetAdx       | 13,869.5 μs | 269.40 μs | 160.32 μs |        - |       - |  630.01 KB |
| **BuilderFull**       | **10000** | **Skender.GetRsi**       |  **1,845.4 μs** |  **51.97 μs** |  **27.18 μs** |  **62.5000** | **15.6250** |  **627.86 KB** |
| BuilderLatestOnly | 10000 | Skender.GetRsi       |  1,805.5 μs |  22.14 μs |  14.64 μs |  15.6250 |       - |  158.01 KB |
| **BuilderFull**       | **10000** | **Trady(...)lator [37]** |  **2,051.8 μs** |  **32.10 μs** |  **21.23 μs** |  **62.5000** | **15.6250** |  **628.07 KB** |
| BuilderLatestOnly | 10000 | Trady(...)lator [37] |  1,996.5 μs |  46.08 μs |  30.48 μs |  15.6250 |       - |  158.22 KB |
| **BuilderFull**       | **10000** | **Trady(...)entum [32]** |  **2,092.7 μs** |  **47.11 μs** |  **31.16 μs** |  **62.5000** | **15.6250** |  **628.23 KB** |
| BuilderLatestOnly | 10000 | Trady(...)entum [32] |  2,080.2 μs |  90.06 μs |  59.57 μs |  15.6250 |       - |  158.37 KB |
| **BuilderFull**       | **10000** | **Trady(...)Index [37]** |  **2,129.7 μs** |  **49.58 μs** |  **32.79 μs** |  **62.5000** | **15.6250** |  **628.25 KB** |
| BuilderLatestOnly | 10000 | Trady(...)Index [37] |  2,233.2 μs | 374.47 μs | 247.69 μs |  15.6250 |       - |  158.38 KB |
| **BuilderFull**       | **10000** | **Trady(...)ength [32]** |  **2,023.2 μs** |  **27.83 μs** |  **16.56 μs** |  **62.5000** | **15.6250** |  **628.09 KB** |
| BuilderLatestOnly | 10000 | Trady(...)ength [32] |  1,941.4 μs |  47.14 μs |  31.18 μs |  15.6250 |       - |  158.22 KB |
| **BuilderFull**       | **10000** | **Trady(...)Index [37]** |  **2,035.7 μs** |  **28.96 μs** |  **15.15 μs** |  **62.5000** | **15.6250** |   **628.1 KB** |
| BuilderLatestOnly | 10000 | Trady(...)Index [37] |  1,912.3 μs |  49.41 μs |  32.68 μs |  15.6250 |       - |  158.23 KB |
