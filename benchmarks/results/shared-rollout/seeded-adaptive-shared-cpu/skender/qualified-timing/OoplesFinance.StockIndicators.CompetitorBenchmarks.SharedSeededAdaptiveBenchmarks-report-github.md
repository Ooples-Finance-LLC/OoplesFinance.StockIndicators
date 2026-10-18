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
| **BuilderFull**       | **Skender.GetKama** | **1000**  |   **329.80 μs** |  **22.889 μs** |  **15.140 μs** |  **4.37** |    **0.21** |        **-** |        **-** |        **-** |   **82.03 KB** |        **0.77** |
| BuilderLatestOnly | Skender.GetKama | 1000  |   346.76 μs |  35.095 μs |  23.213 μs |  4.59 |    0.31 |   3.9063 |        - |        - |   34.43 KB |        0.33 |
| NativeSingle      | Skender.GetKama | 1000  |    75.51 μs |   2.663 μs |   1.585 μs |  1.00 |    0.03 |  12.6953 |   1.9531 |        - |  105.87 KB |        1.00 |
| NativeParallel    | Skender.GetKama | 1000  |   155.28 μs |   1.878 μs |   1.242 μs |  2.06 |    0.04 |  67.3828 |  21.4844 |        - |  486.52 KB |        4.60 |
|                   |                 |       |             |            |            |       |         |          |          |          |            |             |
| **BuilderFull**       | **Skender.GetKama** | **10000** | **3,712.41 μs** | **823.567 μs** | **544.738 μs** |  **3.53** |    **0.53** |  **93.7500** |  **62.5000** |        **-** |  **785.78 KB** |        **0.74** |
| BuilderLatestOnly | Skender.GetKama | 10000 | 3,248.56 μs |  83.988 μs |  55.553 μs |  3.09 |    0.17 |  31.2500 |        - |        - |  315.95 KB |        0.30 |
| NativeSingle      | Skender.GetKama | 10000 | 1,054.62 μs |  91.372 μs |  60.437 μs |  1.00 |    0.08 | 164.0625 | 148.4375 |  78.1250 | 1055.92 KB |        1.00 |
| NativeParallel    | Skender.GetKama | 10000 | 3,713.12 μs |  50.576 μs |  33.453 μs |  3.53 |    0.19 | 687.5000 | 625.0000 | 218.7500 | 4833.64 KB |        4.58 |
