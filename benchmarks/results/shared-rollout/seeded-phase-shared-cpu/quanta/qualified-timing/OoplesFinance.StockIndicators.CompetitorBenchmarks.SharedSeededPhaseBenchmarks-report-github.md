```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Allocated  | Alloc Ratio |
|------------------ |--------------- |------ |------------:|----------:|----------:|------:|--------:|---------:|-----------:|------------:|
| **BuilderFull**       | **QuanTAlib.Mama** | **1000**  |  **1,260.6 μs** |  **19.31 μs** |  **10.10 μs** |  **3.43** |    **0.07** |        **-** |   **82.71 KB** |        **1.47** |
| BuilderLatestOnly | QuanTAlib.Mama | 1000  |  1,289.8 μs |  39.13 μs |  25.88 μs |  3.51 |    0.09 |        - |   35.12 KB |        0.62 |
| NativeSingle      | QuanTAlib.Mama | 1000  |    368.0 μs |  10.36 μs |   6.85 μs |  1.00 |    0.02 |   5.8594 |   56.25 KB |        1.00 |
| NativeParallel    | QuanTAlib.Mama | 1000  |    616.6 μs |  48.31 μs |  31.95 μs |  1.68 |    0.09 |  23.4375 |  203.83 KB |        3.62 |
|                   |                |       |             |           |           |       |         |          |            |             |
| **BuilderFull**       | **QuanTAlib.Mama** | **10000** | **13,091.1 μs** | **783.59 μs** | **518.30 μs** |  **3.56** |    **0.14** |        **-** |  **787.11 KB** |        **1.43** |
| BuilderLatestOnly | QuanTAlib.Mama | 10000 | 13,233.5 μs | 800.56 μs | 529.52 μs |  3.59 |    0.14 |        - |   317.3 KB |        0.58 |
| NativeSingle      | QuanTAlib.Mama | 10000 |  3,682.3 μs |  54.12 μs |  35.79 μs |  1.00 |    0.01 |  31.2500 |  548.67 KB |        1.00 |
| NativeParallel    | QuanTAlib.Mama | 10000 |  6,108.4 μs | 747.25 μs | 494.26 μs |  1.66 |    0.13 | 187.5000 | 1926.69 KB |        3.51 |
