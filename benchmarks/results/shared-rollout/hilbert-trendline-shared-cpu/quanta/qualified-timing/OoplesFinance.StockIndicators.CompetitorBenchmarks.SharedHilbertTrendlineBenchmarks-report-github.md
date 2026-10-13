```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------- |------ |-----------:|----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **QuanTAlib.Htit** | **1000**  |   **138.0 μs** |   **1.96 μs** |   **1.17 μs** |  **0.63** |    **0.01** |   **3.9063** |        **-** |   **56.83 KB** |        **0.31** |
| BuilderLatestOnly | QuanTAlib.Htit | 1000  |   134.1 μs |   1.45 μs |   0.96 μs |  0.61 |    0.01 |   0.9766 |        - |    8.95 KB |        0.05 |
| NativeSingle      | QuanTAlib.Htit | 1000  |   219.4 μs |   8.67 μs |   4.54 μs |  1.00 |    0.03 |  21.4844 |        - |  184.54 KB |        1.00 |
| NativeParallel    | QuanTAlib.Htit | 1000  |   434.2 μs |  30.65 μs |  20.27 μs |  1.98 |    0.10 |  97.6563 |   3.9063 |  804.12 KB |        4.36 |
|                   |                |       |            |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **QuanTAlib.Htit** | **10000** | **1,414.9 μs** |  **52.97 μs** |  **31.52 μs** |  **0.65** |    **0.02** |  **62.5000** |  **46.8750** |  **549.45 KB** |        **0.30** |
| BuilderLatestOnly | QuanTAlib.Htit | 10000 | 1,380.3 μs |  50.71 μs |  33.54 μs |  0.63 |    0.02 |   7.8125 |        - |   79.33 KB |        0.04 |
| NativeSingle      | QuanTAlib.Htit | 10000 | 2,188.0 μs |  74.39 μs |  49.21 μs |  1.00 |    0.03 | 218.7500 |  31.2500 | 1841.99 KB |        1.00 |
| NativeParallel    | QuanTAlib.Htit | 10000 | 4,060.3 μs | 369.13 μs | 244.16 μs |  1.86 |    0.11 | 968.7500 | 187.5000 | 8016.29 KB |        4.35 |
