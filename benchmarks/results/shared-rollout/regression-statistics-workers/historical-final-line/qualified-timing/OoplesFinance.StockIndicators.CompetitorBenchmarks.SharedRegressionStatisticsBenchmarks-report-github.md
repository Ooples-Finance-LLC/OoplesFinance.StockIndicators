```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation        | Count | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------ |----------------- |------ |------------:|-----------:|-----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetSlope** | **1000**  |   **100.60 μs** |  **19.142 μs** |  **12.661 μs** |  **0.88** |    **0.11** |   **13.6719** |   **1.9531** |        **-** |  **112.31 KB** |        **0.70** |
| BuilderLatestOnly | Skender.GetSlope | 1000  |    81.39 μs |   4.520 μs |   2.690 μs |  0.71 |    0.03 |    7.8125 |   0.9766 |        - |    64.7 KB |        0.40 |
| NativeSingle      | Skender.GetSlope | 1000  |   113.98 μs |   3.299 μs |   1.963 μs |  1.00 |    0.02 |   19.5313 |   3.9063 |        - |  160.55 KB |        1.00 |
| NativeParallel    | Skender.GetSlope | 1000  |   223.36 μs |   4.885 μs |   3.231 μs |  1.96 |    0.04 |   99.6094 |  37.1094 |        - |  732.79 KB |        4.56 |
|                   |                  |       |             |            |            |       |         |           |          |          |            |             |
| **BuilderFull**       | **Skender.GetSlope** | **10000** |   **804.04 μs** |  **15.742 μs** |  **10.412 μs** |  **0.48** |    **0.04** |  **132.8125** | **125.0000** |        **-** | **1097.15 KB** |        **0.68** |
| BuilderLatestOnly | Skender.GetSlope | 10000 |   756.62 μs |  28.795 μs |  15.060 μs |  0.45 |    0.04 |   74.2188 |  35.1563 |        - |  627.27 KB |        0.39 |
| NativeSingle      | Skender.GetSlope | 10000 | 1,688.36 μs | 241.713 μs | 159.878 μs |  1.01 |    0.13 |  242.1875 | 187.5000 |  85.9375 | 1602.78 KB |        1.00 |
| NativeParallel    | Skender.GetSlope | 10000 | 3,744.52 μs | 104.868 μs |  54.848 μs |  2.24 |    0.21 | 1000.0000 | 906.2500 | 218.7500 | 7294.37 KB |        4.55 |
