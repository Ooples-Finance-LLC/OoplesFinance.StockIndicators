```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=10  LaunchCount=1  UnrollFactor=1  
WarmupCount=10  

```
| Method            | Operation           | Count | Mean       | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |-------------------- |------ |-----------:|-----------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetMfi**      | **1000**  |  **37.813 μs** |  **2.3499 μs** |  **1.5543 μs** |  **0.47** |    **0.03** |   **7.8125** |   **0.9766** |   **65.34 KB** |        **0.42** |
| BuilderLatestOnly | Skender.GetMfi      | 1000  |  28.411 μs |  1.1838 μs |  0.7045 μs |  0.35 |    0.02 |   1.9531 |        - |   17.77 KB |        0.11 |
| NativeSingle      | Skender.GetMfi      | 1000  |  80.547 μs |  6.3235 μs |  3.7630 μs |  1.00 |    0.06 |  19.0430 |   3.9063 |  156.75 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 1000  |  34.017 μs |  4.9509 μs |  3.2747 μs |  0.42 |    0.04 |  22.2168 |   6.5918 |  175.88 KB |        1.12 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetMfi**      | **10000** | **255.288 μs** |  **7.8573 μs** |  **5.1971 μs** |  **0.28** |    **0.01** |  **76.1719** |  **37.1094** |  **628.23 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetMfi      | 10000 | 219.738 μs |  4.0928 μs |  2.7071 μs |  0.24 |    0.01 |  17.5781 |   3.9063 |  158.42 KB |        0.10 |
| NativeSingle      | Skender.GetMfi      | 10000 | 923.364 μs | 35.5571 μs | 23.5188 μs |  1.00 |    0.03 | 187.5000 | 140.6250 | 1563.06 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 10000 | 391.015 μs | 33.8952 μs | 20.1705 μs |  0.42 |    0.02 | 203.1250 | 144.5313 | 1582.13 KB |        1.01 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **1000**  |  **33.680 μs** |  **1.9049 μs** |  **1.2600 μs** |  **4.16** |    **0.18** |   **7.8125** |   **1.2207** |   **65.34 KB** |        **8.16** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 1000  |  28.902 μs |  1.1856 μs |  0.7842 μs |  3.57 |    0.13 |   1.9531 |        - |   17.77 KB |        2.22 |
| NativeSingle      | TaLib.Functions.Mfi | 1000  |   8.104 μs |  0.3584 μs |  0.2133 μs |  1.00 |    0.04 |   0.9766 |        - |    8.01 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 1000  |   5.753 μs |  0.3639 μs |  0.2407 μs |  0.71 |    0.03 |   1.2512 |   0.0305 |   10.13 KB |        1.27 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **10000** | **255.340 μs** |  **7.3070 μs** |  **4.8331 μs** |  **3.41** |    **0.08** |  **76.1719** |  **37.1094** |  **628.23 KB** |        **8.02** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 10000 | 220.124 μs |  4.9916 μs |  3.3017 μs |  2.94 |    0.06 |  17.5781 |   3.9063 |  158.42 KB |        2.02 |
| NativeSingle      | TaLib.Functions.Mfi | 10000 |  74.788 μs |  1.6449 μs |  1.0880 μs |  1.00 |    0.02 |   9.2773 |   1.4648 |   78.32 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 10000 |  22.747 μs |  1.6442 μs |  1.0876 μs |  0.30 |    0.01 |   9.8877 |   1.9531 |   80.45 KB |        1.03 |
