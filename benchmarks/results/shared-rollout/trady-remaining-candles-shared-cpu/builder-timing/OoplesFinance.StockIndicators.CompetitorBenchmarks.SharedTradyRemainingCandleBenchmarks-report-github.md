```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean        | Error      | StdDev     | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |------------:|-----------:|-----------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **Trady(...)Cover [32]** |    **14.45 μs** |   **1.224 μs** |   **0.728 μs** |  **7.0801** |  **0.9766** |  **58.74 KB** |
| BuilderLatestOnly | 1000  | Trady(...)Cover [32] |    11.04 μs |   2.362 μs |   1.562 μs |  1.3428 |       - |  10.85 KB |
| **BuilderFull**       | **1000**  | **Trady(...)yDoji [31]** |    **16.97 μs** |   **1.692 μs** |   **1.007 μs** |  **6.9580** |  **1.0986** |   **57.1 KB** |
| BuilderLatestOnly | 1000  | Trady(...)yDoji [31] |    12.68 μs |   0.438 μs |   0.261 μs |  1.0986 |       - |   9.21 KB |
| **BuilderFull**       | **1000**  | **Trady(...)thods [37]** |   **285.70 μs** |   **7.635 μs** |   **5.050 μs** |  **5.8594** |       **-** |  **61.09 KB** |
| BuilderLatestOnly | 1000  | Trady(...)thods [37] |   274.43 μs |   6.831 μs |   4.518 μs |       - |       - |  13.21 KB |
| **BuilderFull**       | **1000**  | **Trady(...)thods [36]** |   **288.87 μs** |   **7.677 μs** |   **4.568 μs** |  **5.8594** |       **-** |  **61.11 KB** |
| BuilderLatestOnly | 1000  | Trady(...)thods [36] |   277.62 μs |   7.940 μs |   4.725 μs |       - |       - |  13.22 KB |
| **BuilderFull**       | **10000** | **Trady(...)Cover [32]** |    **67.67 μs** |   **2.449 μs** |   **1.620 μs** | **67.3828** | **22.4609** |  **551.3 KB** |
| BuilderLatestOnly | 10000 | Trady(...)Cover [32] |    35.20 μs |   3.531 μs |   2.336 μs |  9.7656 |  1.9531 |  81.17 KB |
| **BuilderFull**       | **10000** | **Trady(...)yDoji [31]** |    **64.25 μs** |   **2.200 μs** |   **1.309 μs** | **66.4063** | **62.9883** | **550.36 KB** |
| BuilderLatestOnly | 10000 | Trady(...)yDoji [31] |    37.17 μs |   1.913 μs |   1.138 μs |  9.7656 |  1.9531 |  80.24 KB |
| **BuilderFull**       | **10000** | **Trady(...)thods [37]** | **3,013.67 μs** |  **47.646 μs** |  **31.515 μs** | **62.5000** | **15.6250** | **553.74 KB** |
| BuilderLatestOnly | 10000 | Trady(...)thods [37] | 2,999.14 μs | 136.690 μs |  90.412 μs |       - |       - |  83.64 KB |
| **BuilderFull**       | **10000** | **Trady(...)thods [36]** | **2,966.44 μs** |  **42.756 μs** |  **28.280 μs** | **62.5000** | **15.6250** | **553.79 KB** |
| BuilderLatestOnly | 10000 | Trady(...)thods [36] | 3,015.93 μs | 224.154 μs | 148.264 μs |       - |       - |  83.66 KB |
