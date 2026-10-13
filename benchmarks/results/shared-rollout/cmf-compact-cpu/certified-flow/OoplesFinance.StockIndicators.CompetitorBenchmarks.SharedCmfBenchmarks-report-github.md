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
| **BuilderFull**       | **Skender.GetCmf** | **1000**  |   **102.99 μs** | **14.298 μs** |  **8.508 μs** |  **0.61** |    **0.12** |   **7.8125** |        **-** |       **-** |   **81.74 KB** |        **0.27** |
| BuilderLatestOnly | Skender.GetCmf | 1000  |    96.94 μs |  1.282 μs |  0.848 μs |  0.57 |    0.10 |   3.9063 |        - |       - |   34.13 KB |        0.11 |
| NativeSingle      | Skender.GetCmf | 1000  |   176.55 μs | 58.057 μs | 38.401 μs |  1.04 |    0.29 |  36.1328 |  10.7422 |       - |   297.9 KB |        1.00 |
| NativeParallel    | Skender.GetCmf | 1000  |    63.34 μs |  2.505 μs |  1.657 μs |  0.37 |    0.07 |  44.4336 |  14.6484 |       - |  353.61 KB |        1.19 |
|                   |                |       |             |           |           |       |         |          |          |         |            |             |
| **BuilderFull**       | **Skender.GetCmf** | **10000** |   **995.99 μs** | **42.296 μs** | **27.976 μs** |  **0.47** |    **0.02** |  **93.7500** |  **54.6875** |       **-** |  **785.31 KB** |        **0.26** |
| BuilderLatestOnly | Skender.GetCmf | 10000 |   965.22 μs | 57.329 μs | 29.984 μs |  0.46 |    0.02 |  31.2500 |  15.6250 |       - |  315.45 KB |        0.11 |
| NativeSingle      | Skender.GetCmf | 10000 | 2,104.22 μs | 66.764 μs | 39.730 μs |  1.00 |    0.03 | 390.6250 | 312.5000 | 46.8750 | 2970.25 KB |        1.00 |
| NativeParallel    | Skender.GetCmf | 10000 |   739.24 μs | 47.036 μs | 31.112 μs |  0.35 |    0.02 | 414.0625 | 347.6563 |       - | 3095.86 KB |        1.04 |
