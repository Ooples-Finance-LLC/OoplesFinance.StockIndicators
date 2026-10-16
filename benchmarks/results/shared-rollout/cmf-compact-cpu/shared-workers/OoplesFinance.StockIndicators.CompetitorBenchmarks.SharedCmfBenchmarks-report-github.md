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
| **BuilderFull**       | **Skender.GetCmf** | **1000**  |    **41.77 μs** |  **3.532 μs** |  **2.336 μs** |  **0.26** |    **0.03** |  **11.2305** |   **1.9531** |       **-** |   **90.81 KB** |        **0.30** |
| BuilderLatestOnly | Skender.GetCmf | 1000  |    35.54 μs |  2.649 μs |  1.385 μs |  0.22 |    0.03 |   5.3711 |   0.4883 |       - |    43.2 KB |        0.15 |
| NativeSingle      | Skender.GetCmf | 1000  |   161.99 μs | 31.593 μs | 20.897 μs |  1.01 |    0.17 |  36.1328 |   9.7656 |       - |   297.9 KB |        1.00 |
| NativeParallel    | Skender.GetCmf | 1000  |    69.71 μs |  8.423 μs |  5.571 μs |  0.44 |    0.06 |  44.4336 |  14.6484 |       - |  353.29 KB |        1.19 |
|                   |                |       |             |           |           |       |         |          |          |         |            |             |
| **BuilderFull**       | **Skender.GetCmf** | **10000** |   **310.37 μs** | **12.068 μs** |  **7.982 μs** |  **0.15** |    **0.00** |  **95.7031** |  **64.4531** |       **-** |  **794.33 KB** |        **0.27** |
| BuilderLatestOnly | Skender.GetCmf | 10000 |   261.40 μs | 10.087 μs |  6.672 μs |  0.12 |    0.00 |  39.0625 |  13.6719 |       - |  324.49 KB |        0.11 |
| NativeSingle      | Skender.GetCmf | 10000 | 2,093.85 μs | 64.905 μs | 33.946 μs |  1.00 |    0.02 | 390.6250 | 312.5000 | 46.8750 | 2970.25 KB |        1.00 |
| NativeParallel    | Skender.GetCmf | 10000 |   711.37 μs | 25.682 μs | 15.283 μs |  0.34 |    0.01 | 414.0625 | 355.4688 |       - | 3095.55 KB |        1.04 |
