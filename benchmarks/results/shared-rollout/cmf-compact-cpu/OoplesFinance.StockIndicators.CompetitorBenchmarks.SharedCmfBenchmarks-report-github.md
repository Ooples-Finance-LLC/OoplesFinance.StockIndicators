```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2    | Allocated  | Alloc Ratio |
|------------------ |--------------- |------ |------------:|-----------:|-----------:|------:|--------:|---------:|---------:|--------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetCmf** | **1000**  |   **345.22 μs** |  **44.968 μs** |  **26.760 μs** |  **2.53** |    **0.20** |        **-** |        **-** |       **-** |   **81.79 KB** |        **0.27** |
| BuilderLatestOnly | Skender.GetCmf | 1000  |   338.12 μs |  14.644 μs |   9.686 μs |  2.48 |    0.09 |   3.9063 |        - |       - |   34.14 KB |        0.11 |
| NativeSingle      | Skender.GetCmf | 1000  |   136.56 μs |   5.594 μs |   3.329 μs |  1.00 |    0.03 |  36.1328 |  10.7422 |       - |   297.9 KB |        1.00 |
| NativeParallel    | Skender.GetCmf | 1000  |    65.43 μs |   1.759 μs |   1.047 μs |  0.48 |    0.01 |  44.4336 |  14.6484 |       - |  353.61 KB |        1.19 |
|                   |                |       |             |            |            |       |         |          |          |         |            |             |
| **BuilderFull**       | **Skender.GetCmf** | **10000** | **3,356.44 μs** |  **56.324 μs** |  **37.255 μs** |  **1.58** |    **0.08** |  **93.7500** |  **62.5000** |       **-** |  **785.48 KB** |        **0.26** |
| BuilderLatestOnly | Skender.GetCmf | 10000 | 3,327.84 μs | 105.523 μs |  55.191 μs |  1.57 |    0.08 |  31.2500 |        - |       - |  315.64 KB |        0.11 |
| NativeSingle      | Skender.GetCmf | 10000 | 2,131.13 μs | 174.700 μs | 115.553 μs |  1.00 |    0.07 | 390.6250 | 312.5000 | 46.8750 | 2970.25 KB |        1.00 |
| NativeParallel    | Skender.GetCmf | 10000 |   758.19 μs |  78.343 μs |  51.819 μs |  0.36 |    0.03 | 414.0625 | 339.8438 |       - | 3095.86 KB |        1.04 |
