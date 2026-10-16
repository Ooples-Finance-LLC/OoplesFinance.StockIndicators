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
| **BuilderFull**       | **Skender.GetMfi**      | **1000**  |  **35.477 μs** |  **3.2513 μs** |  **2.1505 μs** |  **0.45** |    **0.04** |   **7.8125** |   **0.9766** |   **65.38 KB** |        **0.42** |
| BuilderLatestOnly | Skender.GetMfi      | 1000  |  28.871 μs |  1.6799 μs |  1.1112 μs |  0.36 |    0.03 |   1.9531 |        - |   17.77 KB |        0.11 |
| NativeSingle      | Skender.GetMfi      | 1000  |  79.751 μs |  7.8238 μs |  5.1750 μs |  1.00 |    0.09 |  19.0430 |   3.9063 |  156.75 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 1000  |  32.615 μs |  2.5274 μs |  1.5040 μs |  0.41 |    0.03 |  22.2168 |   6.5918 |  175.88 KB |        1.12 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetMfi**      | **10000** | **255.928 μs** |  **6.5563 μs** |  **4.3366 μs** |  **0.27** |    **0.02** |  **76.1719** |  **37.1094** |  **628.27 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetMfi      | 10000 | 213.863 μs |  6.9459 μs |  4.5943 μs |  0.22 |    0.01 |  17.5781 |   3.9063 |  158.42 KB |        0.10 |
| NativeSingle      | Skender.GetMfi      | 10000 | 955.353 μs | 88.5790 μs | 58.5895 μs |  1.00 |    0.08 | 187.5000 | 140.6250 | 1563.05 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 10000 | 416.548 μs | 32.9152 μs | 21.7714 μs |  0.44 |    0.03 | 205.0781 | 146.4844 | 1582.12 KB |        1.01 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **1000**  |  **34.656 μs** |  **3.3705 μs** |  **2.2294 μs** |  **4.30** |    **0.29** |   **7.8125** |   **1.2207** |   **65.38 KB** |        **8.16** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 1000  |  30.293 μs |  1.2742 μs |  0.8428 μs |  3.76 |    0.14 |   1.9531 |        - |   17.77 KB |        2.22 |
| NativeSingle      | TaLib.Functions.Mfi | 1000  |   8.059 μs |  0.3219 μs |  0.2129 μs |  1.00 |    0.04 |   0.9766 |        - |    8.01 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 1000  |   5.782 μs |  0.2039 μs |  0.1349 μs |  0.72 |    0.02 |   1.2512 |   0.0305 |   10.13 KB |        1.27 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **10000** | **248.899 μs** |  **3.2785 μs** |  **2.1686 μs** |  **3.28** |    **0.08** |  **76.1719** |  **37.1094** |  **628.27 KB** |        **8.02** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 10000 | 219.397 μs |  4.9769 μs |  3.2919 μs |  2.89 |    0.08 |  17.5781 |   3.9063 |  158.41 KB |        2.02 |
| NativeSingle      | TaLib.Functions.Mfi | 10000 |  75.846 μs |  3.1010 μs |  1.8454 μs |  1.00 |    0.03 |   9.2773 |   1.4648 |   78.32 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 10000 |  21.863 μs |  0.5866 μs |  0.3880 μs |  0.29 |    0.01 |   9.8877 |   1.9531 |   80.45 KB |        1.03 |
