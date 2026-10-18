```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|------------------ |--------------- |------ |-------------:|-----------:|-----------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetKvo** | **1000**  |  **1,269.78 μs** |  **36.311 μs** |  **24.017 μs** | **17.47** |    **0.36** |    **7.8125** |         **-** |   **81.14 KB** |        **0.39** |
| BuilderLatestOnly | Skender.GetKvo | 1000  |  1,291.84 μs |  64.426 μs |  42.614 μs | 17.77 |    0.59 |         - |         - |   33.58 KB |        0.16 |
| NativeSingle      | Skender.GetKvo | 1000  |     72.70 μs |   1.150 μs |   0.761 μs |  1.00 |    0.01 |   25.3906 |    7.8125 |  207.63 KB |        1.00 |
| NativeParallel    | Skender.GetKvo | 1000  |    198.63 μs |  16.374 μs |  10.831 μs |  2.73 |    0.14 |  124.0234 |   56.6406 |  945.16 KB |        4.55 |
|                   |                |       |              |            |            |       |         |           |           |            |             |
| **BuilderFull**       | **Skender.GetKvo** | **10000** | **13,037.07 μs** | **328.169 μs** | **195.288 μs** | **14.46** |    **0.39** |         **-** |         **-** |   **785.6 KB** |        **0.38** |
| BuilderLatestOnly | Skender.GetKvo | 10000 | 13,116.36 μs | 292.538 μs | 174.085 μs | 14.54 |    0.39 |         - |         - |  315.79 KB |        0.15 |
| NativeSingle      | Skender.GetKvo | 10000 |    902.34 μs |  37.470 μs |  22.298 μs |  1.00 |    0.03 |  250.0000 |  218.7500 | 2070.96 KB |        1.00 |
| NativeParallel    | Skender.GetKvo | 10000 |  3,401.31 μs | 288.130 μs | 190.580 μs |  3.77 |    0.22 | 1156.2500 | 1062.5000 | 9400.35 KB |        4.54 |
