```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation       | Count | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------ |---------------- |------ |------------:|-----------:|-----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **QuanTAlib.Slope** | **1000**  |    **86.97 μs** |  **10.244 μs** |   **6.776 μs** |  **0.67** |    **0.05** |  **13.6719** |   **1.9531** |        **-** |  **112.31 KB** |        **0.26** |
| BuilderLatestOnly | QuanTAlib.Slope | 1000  |    77.22 μs |   2.934 μs |   1.746 μs |  0.59 |    0.02 |   7.8125 |   0.9766 |        - |    64.7 KB |        0.15 |
| NativeSingle      | QuanTAlib.Slope | 1000  |   130.12 μs |   5.971 μs |   3.950 μs |  1.00 |    0.04 |  51.7578 |   6.8359 |        - |  432.23 KB |        1.00 |
| NativeParallel    | QuanTAlib.Slope | 1000  |    75.97 μs |  12.890 μs |   8.526 μs |  0.58 |    0.06 |  55.6641 |   7.8125 |   0.4883 |  455.65 KB |        1.05 |
|                   |                 |       |             |            |            |       |         |          |          |          |            |             |
| **BuilderFull**       | **QuanTAlib.Slope** | **10000** |   **804.01 μs** |  **63.569 μs** |  **42.047 μs** |  **0.40** |    **0.03** | **132.8125** | **128.9063** |        **-** | **1097.12 KB** |        **0.25** |
| BuilderLatestOnly | QuanTAlib.Slope | 10000 |   723.54 μs |  66.527 μs |  44.003 μs |  0.36 |    0.03 |  74.2188 |  35.1563 |        - |  627.27 KB |        0.14 |
| NativeSingle      | QuanTAlib.Slope | 10000 | 2,008.65 μs | 190.073 μs | 125.722 μs |  1.00 |    0.08 | 578.1250 | 109.3750 | 109.3750 | 4360.21 KB |        1.00 |
| NativeParallel    | QuanTAlib.Slope | 10000 | 1,720.17 μs | 174.918 μs | 115.698 μs |  0.86 |    0.07 | 578.1250 | 109.3750 | 109.3750 | 4386.88 KB |        1.01 |
