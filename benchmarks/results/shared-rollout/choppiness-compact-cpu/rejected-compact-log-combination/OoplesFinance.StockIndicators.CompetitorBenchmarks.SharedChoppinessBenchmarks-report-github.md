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
| **BuilderFull**       | **Skender.GetChop** | **1000**  |   **157.02 μs** |  **7.449 μs** |  **4.927 μs** |  **1.48** |    **0.05** |   **7.8125** |        **-** |   **73.13 KB** |        **0.46** |
| BuilderLatestOnly | Skender.GetChop | 1000  |   147.64 μs |  1.647 μs |  1.090 μs |  1.39 |    0.03 |   2.9297 |        - |   25.52 KB |        0.16 |
| NativeSingle      | Skender.GetChop | 1000  |   106.03 μs |  3.231 μs |  2.137 μs |  1.00 |    0.03 |  19.5313 |   3.9063 |  160.66 KB |        1.00 |
| NativeParallel    | Skender.GetChop | 1000  |    33.28 μs |  0.596 μs |  0.355 μs |  0.31 |    0.01 |  22.4609 |   5.6152 |  180.11 KB |        1.12 |
|                   |                 |       |             |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetChop** | **10000** | **1,395.60 μs** | **70.089 μs** | **46.360 μs** |  **1.14** |    **0.05** |  **70.3125** |  **39.0625** |  **636.05 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetChop | 10000 | 1,343.28 μs | 10.695 μs |  7.074 μs |  1.10 |    0.03 |  15.6250 |        - |  166.21 KB |        0.10 |
| NativeSingle      | Skender.GetChop | 10000 | 1,224.08 μs | 50.498 μs | 33.401 μs |  1.00 |    0.04 | 195.3125 | 140.6250 | 1602.12 KB |        1.00 |
| NativeParallel    | Skender.GetChop | 10000 |   414.55 μs | 42.779 μs | 28.296 μs |  0.34 |    0.02 | 214.8438 | 144.5313 | 1621.51 KB |        1.01 |
