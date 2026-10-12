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
| **BuilderFull**       | **Skender.GetChop** | **1000**  |   **143.29 μs** |  **7.487 μs** |  **4.952 μs** |  **1.30** |    **0.07** |   **7.8125** |        **-** |   **73.13 KB** |        **0.46** |
| BuilderLatestOnly | Skender.GetChop | 1000  |   132.28 μs |  0.844 μs |  0.502 μs |  1.20 |    0.05 |   2.9297 |        - |   25.52 KB |        0.16 |
| NativeSingle      | Skender.GetChop | 1000  |   110.00 μs |  7.762 μs |  4.619 μs |  1.00 |    0.06 |  19.5313 |   3.9063 |  160.66 KB |        1.00 |
| NativeParallel    | Skender.GetChop | 1000  |    33.85 μs |  0.766 μs |  0.507 μs |  0.31 |    0.01 |  22.4609 |   5.6152 |  180.11 KB |        1.12 |
|                   |                 |       |             |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetChop** | **10000** | **1,236.64 μs** | **13.504 μs** |  **8.036 μs** |  **1.02** |    **0.02** |  **70.3125** |  **39.0625** |  **636.05 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetChop | 10000 | 1,185.11 μs |  8.619 μs |  5.129 μs |  0.97 |    0.02 |  15.6250 |        - |  166.21 KB |        0.10 |
| NativeSingle      | Skender.GetChop | 10000 | 1,216.29 μs | 46.025 μs | 27.389 μs |  1.00 |    0.03 | 195.3125 | 140.6250 | 1602.12 KB |        1.00 |
| NativeParallel    | Skender.GetChop | 10000 |   398.31 μs | 13.327 μs |  8.815 μs |  0.33 |    0.01 | 214.8438 | 148.4375 | 1621.53 KB |        1.01 |
