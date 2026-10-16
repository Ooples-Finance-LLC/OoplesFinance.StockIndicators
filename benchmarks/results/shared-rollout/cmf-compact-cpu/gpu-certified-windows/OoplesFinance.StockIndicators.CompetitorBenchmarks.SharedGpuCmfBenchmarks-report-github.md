```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2    | Allocated  | Alloc Ratio |
|------------------ |--------------- |------ |------------:|----------:|----------:|------:|--------:|---------:|---------:|--------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetCmf** | **1000**  |    **42.17 μs** |  **3.335 μs** |  **1.985 μs** |  **0.30** |    **0.02** |  **10.7422** |   **1.9531** |       **-** |   **90.81 KB** |        **0.30** |
| BuilderLatestOnly | Skender.GetCmf | 1000  |    34.77 μs |  1.463 μs |  0.968 μs |  0.25 |    0.01 |   5.3711 |   0.4883 |       - |    43.2 KB |        0.15 |
| GpuFull           | Skender.GetCmf | 1000  |   519.15 μs | 20.265 μs | 10.599 μs |  3.69 |    0.13 |   7.8125 |        - |       - |   80.58 KB |        0.27 |
| GpuLatestOnly     | Skender.GetCmf | 1000  |   493.58 μs |  8.439 μs |  5.022 μs |  3.50 |    0.11 |   3.9063 |        - |       - |   32.94 KB |        0.11 |
| NativeSingle      | Skender.GetCmf | 1000  |   140.98 μs |  6.371 μs |  4.214 μs |  1.00 |    0.04 |  36.1328 |   9.7656 |       - |   297.9 KB |        1.00 |
| NativeParallel    | Skender.GetCmf | 1000  |    60.97 μs |  1.713 μs |  1.133 μs |  0.43 |    0.01 |  44.4336 |  14.6484 |       - |  353.29 KB |        1.19 |
|                   |                |       |             |           |           |       |         |          |          |         |            |             |
| **BuilderFull**       | **Skender.GetCmf** | **10000** |   **338.58 μs** | **33.960 μs** | **22.462 μs** |  **0.16** |    **0.01** |  **95.7031** |  **64.4531** |       **-** |  **794.33 KB** |        **0.27** |
| BuilderLatestOnly | Skender.GetCmf | 10000 |   263.07 μs |  4.482 μs |  2.965 μs |  0.13 |    0.00 |  39.0625 |  13.6719 |       - |  324.49 KB |        0.11 |
| GpuFull           | Skender.GetCmf | 10000 | 1,108.33 μs | 35.554 μs | 23.517 μs |  0.53 |    0.02 |  93.7500 |  46.8750 |       - |  784.09 KB |        0.26 |
| GpuLatestOnly     | Skender.GetCmf | 10000 | 1,047.60 μs | 36.157 μs | 23.915 μs |  0.50 |    0.02 |  31.2500 |   7.8125 |       - |  314.24 KB |        0.11 |
| NativeSingle      | Skender.GetCmf | 10000 | 2,093.12 μs | 96.293 μs | 63.692 μs |  1.00 |    0.04 | 375.0000 | 296.8750 | 31.2500 | 2970.15 KB |        1.00 |
| NativeParallel    | Skender.GetCmf | 10000 |   711.81 μs | 17.095 μs | 10.173 μs |  0.34 |    0.01 | 414.0625 | 347.6563 |       - | 3095.56 KB |        1.04 |
