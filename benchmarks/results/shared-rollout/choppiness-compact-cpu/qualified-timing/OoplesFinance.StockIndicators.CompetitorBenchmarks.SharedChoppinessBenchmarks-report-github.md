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
| **BuilderFull**       | **Skender.GetChop** | **1000**  |    **35.16 μs** |  **2.349 μs** |  **1.553 μs** |  **0.33** |    **0.01** |   **7.8125** |   **0.9766** |      **70 KB** |        **0.44** |
| BuilderLatestOnly | Skender.GetChop | 1000  |    30.26 μs |  1.333 μs |  0.882 μs |  0.28 |    0.01 |   2.6855 |        - |   22.39 KB |        0.14 |
| NativeSingle      | Skender.GetChop | 1000  |   106.39 μs |  2.502 μs |  1.655 μs |  1.00 |    0.02 |  19.5313 |   3.9063 |  160.66 KB |        1.00 |
| NativeParallel    | Skender.GetChop | 1000  |    34.39 μs |  0.860 μs |  0.569 μs |  0.32 |    0.01 |  22.4609 |   5.6152 |  180.11 KB |        1.12 |
|                   |                 |       |             |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetChop** | **10000** |   **289.96 μs** | **10.751 μs** |  **7.111 μs** |  **0.24** |    **0.01** |  **76.1719** |  **74.2188** |  **632.88 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetChop | 10000 |   259.11 μs |  3.617 μs |  2.393 μs |  0.22 |    0.00 |  19.5313 |   5.8594 |  163.04 KB |        0.10 |
| NativeSingle      | Skender.GetChop | 10000 | 1,203.43 μs | 37.986 μs | 25.125 μs |  1.00 |    0.03 | 195.3125 | 140.6250 | 1602.12 KB |        1.00 |
| NativeParallel    | Skender.GetChop | 10000 |   404.63 μs | 12.441 μs |  8.229 μs |  0.34 |    0.01 | 214.8438 | 148.4375 | 1621.52 KB |        1.01 |
