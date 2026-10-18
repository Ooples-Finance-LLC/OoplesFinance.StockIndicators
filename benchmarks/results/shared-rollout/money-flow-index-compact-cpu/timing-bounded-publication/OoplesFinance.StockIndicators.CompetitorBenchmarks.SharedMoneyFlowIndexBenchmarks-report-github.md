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
| **BuilderFull**       | **Skender.GetMfi**      | **1000**  |  **37.283 μs** |  **3.9076 μs** |  **2.5846 μs** |  **0.46** |    **0.03** |   **7.8125** |   **0.9766** |   **65.38 KB** |        **0.42** |
| BuilderLatestOnly | Skender.GetMfi      | 1000  |  31.434 μs |  0.9603 μs |  0.6352 μs |  0.39 |    0.01 |   1.9531 |        - |   17.77 KB |        0.11 |
| NativeSingle      | Skender.GetMfi      | 1000  |  81.575 μs |  3.8559 μs |  2.0167 μs |  1.00 |    0.03 |  19.0430 |   3.9063 |  156.75 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 1000  |  33.274 μs |  2.6169 μs |  1.7309 μs |  0.41 |    0.02 |  22.2168 |   6.1035 |  175.88 KB |        1.12 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetMfi**      | **10000** | **260.551 μs** |  **5.9581 μs** |  **3.1162 μs** |  **0.27** |    **0.02** |  **76.1719** |  **37.1094** |  **628.27 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetMfi      | 10000 | 224.630 μs |  4.1461 μs |  2.7424 μs |  0.23 |    0.01 |  17.5781 |   3.9063 |  158.42 KB |        0.10 |
| NativeSingle      | Skender.GetMfi      | 10000 | 970.311 μs | 87.6321 μs | 57.9632 μs |  1.00 |    0.08 | 187.5000 | 140.6250 | 1563.07 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 10000 | 418.943 μs | 42.8730 μs | 25.5130 μs |  0.43 |    0.03 | 205.0781 | 146.4844 | 1582.12 KB |        1.01 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **1000**  |  **35.097 μs** |  **2.3124 μs** |  **1.5295 μs** |  **3.93** |    **0.38** |   **7.8125** |   **1.2207** |   **65.38 KB** |        **8.16** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 1000  |  30.195 μs |  1.3897 μs |  0.9192 μs |  3.38 |    0.31 |   1.9531 |        - |   17.77 KB |        2.22 |
| NativeSingle      | TaLib.Functions.Mfi | 1000  |   9.008 μs |  1.3086 μs |  0.8656 μs |  1.01 |    0.13 |   0.9766 |        - |    8.01 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 1000  |   6.029 μs |  0.7405 μs |  0.4898 μs |  0.67 |    0.08 |   1.2817 |        - |   10.13 KB |        1.27 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **10000** | **271.770 μs** | **12.6730 μs** |  **8.3824 μs** |  **3.25** |    **0.31** |  **76.1719** |  **37.1094** |  **628.27 KB** |        **8.02** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 10000 | 232.243 μs |  6.8796 μs |  4.5505 μs |  2.78 |    0.26 |  17.5781 |   3.9063 |  158.42 KB |        2.02 |
| NativeSingle      | TaLib.Functions.Mfi | 10000 |  84.344 μs | 12.3251 μs |  8.1523 μs |  1.01 |    0.13 |   9.2773 |   1.4648 |   78.32 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 10000 |  22.941 μs |  0.8114 μs |  0.5367 μs |  0.27 |    0.03 |   9.8877 |   1.9531 |   80.45 KB |        1.03 |
