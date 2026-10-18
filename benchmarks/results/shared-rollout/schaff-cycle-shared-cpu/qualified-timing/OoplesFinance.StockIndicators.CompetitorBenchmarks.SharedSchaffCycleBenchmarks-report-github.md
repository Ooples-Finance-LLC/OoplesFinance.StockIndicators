```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Gen2     | Allocated   | Alloc Ratio |
|------------------ |--------------- |------ |------------:|------------:|------------:|------:|--------:|----------:|----------:|---------:|------------:|------------:|
| **BuilderFull**       | **Skender.GetStc** | **1000**  |    **931.9 μs** |    **24.33 μs** |    **12.72 μs** |  **5.71** |    **0.38** |    **7.8125** |         **-** |        **-** |    **66.56 KB** |        **0.12** |
| BuilderLatestOnly | Skender.GetStc | 1000  |    912.0 μs |    17.65 μs |    10.50 μs |  5.59 |    0.37 |         - |         - |        - |       19 KB |        0.04 |
| NativeSingle      | Skender.GetStc | 1000  |    163.9 μs |    17.60 μs |    11.64 μs |  1.00 |    0.09 |   64.4531 |   18.5547 |        - |   534.44 KB |        1.00 |
| NativeParallel    | Skender.GetStc | 1000  |    413.1 μs |    17.74 μs |     9.28 μs |  2.53 |    0.17 |  320.3125 |  156.2500 |        - |  2409.04 KB |        4.51 |
|                   |                |       |             |             |             |       |         |           |           |          |             |             |
| **BuilderFull**       | **Skender.GetStc** | **10000** |  **9,265.4 μs** |   **198.69 μs** |   **131.42 μs** |  **2.49** |    **0.05** |         **-** |         **-** |        **-** |   **630.17 KB** |        **0.11** |
| BuilderLatestOnly | Skender.GetStc | 10000 |  9,430.8 μs |   433.33 μs |   257.86 μs |  2.54 |    0.07 |         - |         - |        - |   160.36 KB |        0.03 |
| NativeSingle      | Skender.GetStc | 10000 |  3,719.4 μs |    90.92 μs |    54.10 μs |  1.00 |    0.02 |  750.0000 |  656.2500 | 187.5000 |  5621.88 KB |        1.00 |
| NativeParallel    | Skender.GetStc | 10000 | 17,217.4 μs | 1,549.40 μs | 1,024.83 μs |  4.63 |    0.27 | 3166.6667 | 2500.0000 | 500.0000 | 25154.38 KB |        4.47 |
