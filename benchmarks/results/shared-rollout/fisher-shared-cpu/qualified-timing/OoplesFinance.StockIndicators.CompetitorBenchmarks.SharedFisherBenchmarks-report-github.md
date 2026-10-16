```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation            | Count | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------ |--------------------- |------ |-----------:|---------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skend(...)sform [26]** | **1000**  |   **145.5 μs** |  **5.45 μs** |  **3.24 μs** |  **1.44** |    **0.03** |   **7.8125** |        **-** |        **-** |    **81.3 KB** |        **0.67** |
| BuilderLatestOnly | Skend(...)sform [26] | 1000  |   141.4 μs |  3.54 μs |  2.11 μs |  1.40 |    0.02 |   3.9063 |        - |        - |    33.7 KB |        0.28 |
| NativeSingle      | Skend(...)sform [26] | 1000  |   101.2 μs |  1.51 μs |  0.79 μs |  1.00 |    0.01 |  14.6484 |   2.9297 |        - |  121.54 KB |        1.00 |
| NativeParallel    | Skend(...)sform [26] | 1000  |   173.9 μs |  4.46 μs |  2.33 μs |  1.72 |    0.03 |  77.1484 |  28.3203 |        - |  556.88 KB |        4.58 |
|                   |                      |       |            |          |          |       |         |          |          |          |            |             |
| **BuilderFull**       | **Skend(...)sform [26]** | **10000** | **1,432.4 μs** | **61.81 μs** | **36.78 μs** |  **1.23** |    **0.04** |  **93.7500** |  **46.8750** |        **-** |  **784.86 KB** |        **0.65** |
| BuilderLatestOnly | Skend(...)sform [26] | 10000 | 1,438.9 μs | 98.64 μs | 65.24 μs |  1.23 |    0.06 |  31.2500 |  15.6250 |        - |  315.02 KB |        0.26 |
| NativeSingle      | Skend(...)sform [26] | 10000 | 1,167.7 μs | 47.20 μs | 28.09 μs |  1.00 |    0.03 | 187.5000 | 132.8125 |  78.1250 | 1212.05 KB |        1.00 |
| NativeParallel    | Skend(...)sform [26] | 10000 | 3,699.0 μs | 69.46 μs | 45.94 μs |  3.17 |    0.08 | 781.2500 | 750.0000 | 218.7500 | 5536.58 KB |        4.57 |
