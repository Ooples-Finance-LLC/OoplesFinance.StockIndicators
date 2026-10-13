```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------ |--------------- |------ |-----------:|----------:|---------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **BuilderFull**       | **QuanTAlib.Kama** | **1000**  |   **391.0 μs** |  **97.34 μs** | **64.39 μs** |  **2.03** |    **0.32** |        **-** |       **-** |   **82.03 KB** |        **1.73** |
| BuilderLatestOnly | QuanTAlib.Kama | 1000  |   320.5 μs |   7.35 μs |  4.37 μs |  1.66 |    0.03 |   3.9063 |       - |   34.44 KB |        0.73 |
| NativeSingle      | QuanTAlib.Kama | 1000  |   193.0 μs |   4.81 μs |  2.86 μs |  1.00 |    0.02 |   4.8828 |       - |   47.46 KB |        1.00 |
| NativeParallel    | QuanTAlib.Kama | 1000  |   285.2 μs |   4.58 μs |  2.73 μs |  1.48 |    0.02 |  21.4844 |       - |  188.25 KB |        3.97 |
|                   |                |       |            |           |          |       |         |          |         |            |             |
| **BuilderFull**       | **QuanTAlib.Kama** | **10000** | **3,420.8 μs** | **177.81 μs** | **93.00 μs** |  **1.75** |    **0.05** |  **93.7500** | **62.5000** |  **785.74 KB** |        **1.67** |
| BuilderLatestOnly | QuanTAlib.Kama | 10000 | 3,261.8 μs | 117.09 μs | 69.68 μs |  1.67 |    0.04 |  31.2500 |       - |  315.95 KB |        0.67 |
| NativeSingle      | QuanTAlib.Kama | 10000 | 1,951.9 μs |  41.01 μs | 24.41 μs |  1.00 |    0.02 |  46.8750 |       - |  469.45 KB |        1.00 |
| NativeParallel    | QuanTAlib.Kama | 10000 | 2,846.8 μs | 104.32 μs | 69.00 μs |  1.46 |    0.04 | 218.7500 | 31.2500 | 1840.69 KB |        3.92 |
