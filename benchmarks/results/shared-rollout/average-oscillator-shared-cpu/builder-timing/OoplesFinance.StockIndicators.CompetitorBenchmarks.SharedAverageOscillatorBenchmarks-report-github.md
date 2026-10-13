```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean         | Error      | StdDev     | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |-------------:|-----------:|-----------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **Skender.GetAwesome**   |    **64.151 μs** |  **4.7236 μs** |  **3.1244 μs** | **10.7422** |  **1.9531** |     **93 KB** |
| BuilderLatestOnly | 1000  | Skender.GetAwesome   |    58.598 μs |  2.1048 μs |  1.3922 μs |  5.3711 |  0.4883 |  45.39 KB |
| **BuilderFull**       | **1000**  | **Trady(...)erage [40]** |    **44.393 μs** |  **1.4035 μs** |  **0.8352 μs** |  **6.8359** |  **0.9766** |  **56.66 KB** |
| BuilderLatestOnly | 1000  | Trady(...)erage [40] |    41.028 μs |  0.4777 μs |  0.2498 μs |  0.9766 |       - |   8.78 KB |
| **BuilderFull**       | **1000**  | **Trady(...)lator [50]** |   **148.076 μs** |  **1.8437 μs** |  **1.0971 μs** |  **7.8125** |  **0.9766** |  **64.94 KB** |
| BuilderLatestOnly | 1000  | Trady(...)lator [50] |   143.697 μs |  1.8996 μs |  1.2565 μs |  1.9531 |       - |  17.32 KB |
| **BuilderFull**       | **1000**  | **Trady(...)lator [45]** |    **48.817 μs** |  **5.4929 μs** |  **3.6332 μs** |  **9.0332** |  **1.4648** |  **74.32 KB** |
| BuilderLatestOnly | 1000  | Trady(...)lator [45] |    41.116 μs |  0.9312 μs |  0.5542 μs |  3.1738 |       - |  26.71 KB |
| **BuilderFull**       | **1000**  | **Trady(...)erage [37]** |    **14.572 μs** |  **1.4312 μs** |  **0.8517 μs** |  **7.3242** |  **1.2207** |  **60.05 KB** |
| BuilderLatestOnly | 1000  | Trady(...)erage [37] |     9.129 μs |  1.0960 μs |  0.6522 μs |  1.4648 |       - |  12.17 KB |
| **BuilderFull**       | **10000** | **Skender.GetAwesome**   |   **436.281 μs** | **14.5966 μs** |  **9.6547 μs** | **93.7500** | **62.5000** | **796.54 KB** |
| BuilderLatestOnly | 10000 | Skender.GetAwesome   |   400.115 μs | 11.9874 μs |  7.9289 μs | 39.0625 | 11.7188 | 326.69 KB |
| **BuilderFull**       | **10000** | **Trady(...)erage [40]** |   **513.950 μs** | **21.5239 μs** | **14.2367 μs** | **66.4063** | **58.5938** | **549.25 KB** |
| BuilderLatestOnly | 10000 | Trady(...)erage [40] |   482.840 μs | 16.6549 μs | 11.0162 μs |  7.8125 |       - |  79.13 KB |
| **BuilderFull**       | **10000** | **Trady(...)lator [50]** | **1,487.228 μs** | **12.3465 μs** |  **7.3472 μs** | **70.3125** | **23.4375** | **627.87 KB** |
| BuilderLatestOnly | 10000 | Trady(...)lator [50] | 1,457.215 μs | 23.1101 μs | 13.7525 μs | 15.6250 |       - | 158.02 KB |
| **BuilderFull**       | **10000** | **Trady(...)lator [45]** |   **330.759 μs** |  **6.2903 μs** |  **4.1607 μs** | **76.1719** | **42.9688** | **637.21 KB** |
| BuilderLatestOnly | 10000 | Trady(...)lator [45] |   299.393 μs |  5.7453 μs |  3.4189 μs | 19.5313 |  5.8594 | 167.36 KB |
| **BuilderFull**       | **10000** | **Trady(...)erage [37]** |    **73.175 μs** |  **7.5234 μs** |  **4.9763 μs** | **67.3828** | **22.4609** | **552.61 KB** |
| BuilderLatestOnly | 10000 | Trady(...)erage [37] |    41.726 μs |  1.1724 μs |  0.7755 μs | 10.0098 |  1.9531 |  82.49 KB |
