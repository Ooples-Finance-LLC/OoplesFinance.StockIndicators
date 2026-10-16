```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=10  LaunchCount=1  UnrollFactor=1  
WarmupCount=10  

```
| Method            | Operation       | Count | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |---------------- |------ |------------:|----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetChop** | **1000**  |    **52.84 μs** |  **1.462 μs** |  **0.967 μs** |  **0.49** |    **0.01** |   **8.7891** |   **0.9766** |   **73.12 KB** |        **0.46** |
| BuilderLatestOnly | Skender.GetChop | 1000  |    47.68 μs |  1.422 μs |  0.846 μs |  0.44 |    0.01 |   2.9297 |        - |   25.51 KB |        0.16 |
| NativeSingle      | Skender.GetChop | 1000  |   108.08 μs |  4.823 μs |  2.870 μs |  1.00 |    0.04 |  19.5313 |   3.9063 |  160.66 KB |        1.00 |
| NativeParallel    | Skender.GetChop | 1000  |    33.91 μs |  0.690 μs |  0.361 μs |  0.31 |    0.01 |  22.4609 |   5.6152 |  180.11 KB |        1.12 |
|                   |                 |       |             |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetChop** | **10000** |   **435.48 μs** |  **6.768 μs** |  **3.540 μs** |  **0.36** |    **0.01** |  **74.2188** |  **39.0625** |  **636.02 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetChop | 10000 |   406.67 μs |  3.840 μs |  2.540 μs |  0.34 |    0.01 |  19.5313 |   3.9063 |  166.18 KB |        0.10 |
| NativeSingle      | Skender.GetChop | 10000 | 1,199.85 μs | 28.929 μs | 19.135 μs |  1.00 |    0.02 | 195.3125 | 140.6250 | 1602.12 KB |        1.00 |
| NativeParallel    | Skender.GetChop | 10000 |   394.57 μs | 23.320 μs | 13.877 μs |  0.33 |    0.01 | 214.8438 | 156.2500 | 1621.53 KB |        1.01 |
