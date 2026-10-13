```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation           | Count | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------ |-------------------- |------ |------------:|----------:|----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **BuilderFull**       | **TaLib.Functions.Cmo** | **1000**  |   **178.35 μs** |  **4.121 μs** |  **2.726 μs** | **14.18** |    **0.28** |  **7.8125** |       **-** |  **64.83 KB** |        **8.41** |
| BuilderLatestOnly | TaLib.Functions.Cmo | 1000  |   172.16 μs |  1.603 μs |  0.954 μs | 13.68 |    0.19 |  1.9531 |       - |  17.23 KB |        2.23 |
| NativeSingle      | TaLib.Functions.Cmo | 1000  |    12.58 μs |  0.261 μs |  0.173 μs |  1.00 |    0.02 |  0.8545 |       - |   7.71 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Cmo | 1000  |    24.17 μs |  0.781 μs |  0.517 μs |  1.92 |    0.05 |  7.0801 |  0.2441 |  42.71 KB |        5.54 |
|                   |                     |       |             |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **TaLib.Functions.Cmo** | **10000** | **1,620.76 μs** | **30.889 μs** | **18.382 μs** | **12.84** |    **0.19** | **62.5000** | **15.6250** | **627.84 KB** |        **8.05** |
| BuilderLatestOnly | TaLib.Functions.Cmo | 10000 | 1,605.76 μs | 33.420 μs | 19.888 μs | 12.73 |    0.20 | 15.6250 |       - | 157.99 KB |        2.02 |
| NativeSingle      | TaLib.Functions.Cmo | 10000 |   126.20 μs |  1.970 μs |  1.303 μs |  1.00 |    0.01 |  8.7891 |  0.9766 |  78.03 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Cmo | 10000 |   167.81 μs |  3.939 μs |  2.344 μs |  1.33 |    0.02 | 59.5703 | 20.5078 | 429.44 KB |        5.50 |
