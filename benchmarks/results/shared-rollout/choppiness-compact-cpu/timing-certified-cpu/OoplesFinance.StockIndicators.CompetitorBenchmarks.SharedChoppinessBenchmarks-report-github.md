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
| **BuilderFull**       | **Skender.GetChop** | **1000**  |    **39.83 μs** |  **4.512 μs** |  **2.985 μs** |  **0.34** |    **0.03** |   **7.8125** |   **0.9766** |      **70 KB** |        **0.44** |
| BuilderLatestOnly | Skender.GetChop | 1000  |    34.81 μs |  1.514 μs |  1.001 μs |  0.30 |    0.01 |   2.6855 |        - |   22.39 KB |        0.14 |
| NativeSingle      | Skender.GetChop | 1000  |   118.08 μs |  7.030 μs |  4.650 μs |  1.00 |    0.05 |  19.5313 |   3.9063 |  160.66 KB |        1.00 |
| NativeParallel    | Skender.GetChop | 1000  |    42.66 μs |  3.577 μs |  2.129 μs |  0.36 |    0.02 |  22.4609 |   5.6152 |  180.11 KB |        1.12 |
|                   |                 |       |             |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetChop** | **10000** |   **312.54 μs** |  **5.800 μs** |  **3.034 μs** |  **0.24** |    **0.01** |  **76.1719** |  **74.2188** |  **632.88 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetChop | 10000 |   276.45 μs |  8.861 μs |  5.861 μs |  0.21 |    0.01 |  19.5313 |   5.8594 |  163.04 KB |        0.10 |
| NativeSingle      | Skender.GetChop | 10000 | 1,318.00 μs | 68.159 μs | 45.083 μs |  1.00 |    0.05 | 195.3125 | 140.6250 | 1602.12 KB |        1.00 |
| NativeParallel    | Skender.GetChop | 10000 |   521.11 μs | 45.441 μs | 27.041 μs |  0.40 |    0.02 | 214.8438 | 152.3438 | 1621.53 KB |        1.01 |
