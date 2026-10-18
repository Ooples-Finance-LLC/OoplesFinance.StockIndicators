```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation          | Count | Mean          | Error       | StdDev      | Ratio  | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |------------------- |------ |--------------:|------------:|------------:|-------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **TaLib.Functions.T3** | **1000**  |  **2,005.956 μs** |  **56.3723 μs** |  **33.5463 μs** | **460.34** |   **10.77** |        **-** |        **-** |     **689 KB** |       **98.76** |
| BuilderLatestOnly | TaLib.Functions.T3 | 1000  |  1,713.707 μs |  30.2493 μs |  18.0009 μs | 393.28 |    7.82 |  78.1250 |        - |  641.44 KB |       91.94 |
| NativeSingle      | TaLib.Functions.T3 | 1000  |      4.359 μs |   0.1207 μs |   0.0798 μs |   1.00 |    0.02 |   0.8240 |        - |    6.98 KB |        1.00 |
| NativeParallel    | TaLib.Functions.T3 | 1000  |     10.582 μs |   0.4478 μs |   0.2962 μs |   2.43 |    0.08 |   6.1035 |   0.2441 |   38.69 KB |        5.55 |
|                   |                    |       |               |             |             |        |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.T3** | **10000** | **18,423.992 μs** | **332.1685 μs** | **173.7306 μs** | **409.35** |   **12.38** | **833.3333** | **666.6667** | **7581.67 KB** |       **98.09** |
| BuilderLatestOnly | TaLib.Functions.T3 | 10000 | 18,446.480 μs | 464.1108 μs | 306.9806 μs | 409.85 |   13.50 | 833.3333 | 166.6667 |  7111.8 KB |       92.01 |
| NativeSingle      | TaLib.Functions.T3 | 10000 |     45.045 μs |   2.0815 μs |   1.3768 μs |   1.00 |    0.04 |   9.2773 |        - |   77.29 KB |        1.00 |
| NativeParallel    | TaLib.Functions.T3 | 10000 |    117.740 μs |  19.6148 μs |  12.9740 μs |   2.62 |    0.29 |  59.5703 |  19.5313 |  425.41 KB |        5.50 |
