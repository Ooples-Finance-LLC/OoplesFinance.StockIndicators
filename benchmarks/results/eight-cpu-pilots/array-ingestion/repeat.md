```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=20  IterationTime=500ms  LaunchCount=1
UnrollFactor=1  WarmupCount=8

```
| Method           | PairId               | Count | Mean       | Error      | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|----------------- |--------------------- |------ |-----------:|-----------:|-----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **ArrayBuilder**     | **TaLib.Functions.Asin** | **1000**  |  **32.502 μs** |  **1.9443 μs** |  **2.2391 μs** |  **1.84** |    **0.21** |  **8.3529** |  **1.1756** |  **68.93 KB** |        **8.76** |
| ProjectedBuilder | TaLib.Functions.Asin | 1000  |  38.356 μs |  2.5475 μs |  2.9337 μs |  2.17 |    0.26 |  8.3995 |  1.1559 |  68.96 KB |        8.77 |
| NativeOwned      | TaLib.Functions.Asin | 1000  |  17.848 μs |  1.4218 μs |  1.6373 μs |  1.01 |    0.13 |  0.9376 |       - |   7.87 KB |        1.00 |
|                  |                      |       |            |            |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Asin** | **10000** | **315.217 μs** | **48.4963 μs** | **51.8905 μs** |  **1.96** |    **0.41** | **76.9231** | **76.3697** | **631.91 KB** |        **8.08** |
| ProjectedBuilder | TaLib.Functions.Asin | 10000 | 334.523 μs | 36.3102 μs | 41.8149 μs |  2.08 |    0.38 | 76.8683 | 76.1566 | 631.94 KB |        8.08 |
| NativeOwned      | TaLib.Functions.Asin | 10000 | 163.471 μs | 17.3401 μs | 19.9689 μs |  1.02 |    0.18 |  9.3213 |  1.4565 |  78.18 KB |        1.00 |
|                  |                      |       |            |            |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Sma**  | **1000**  |  **27.229 μs** |  **1.8596 μs** |  **2.1415 μs** |  **9.76** |    **0.91** |  **9.6430** |  **1.5868** |  **79.07 KB** |       **10.05** |
| ProjectedBuilder | TaLib.Functions.Sma  | 1000  |  34.749 μs |  1.3881 μs |  1.5986 μs | 12.46 |    0.86 |  9.6229 |  1.5575 |   79.1 KB |       10.05 |
| NativeOwned      | TaLib.Functions.Sma  | 1000  |   2.797 μs |  0.1324 μs |  0.1524 μs |  1.00 |    0.07 |  0.9613 |  0.0266 |   7.87 KB |        1.00 |
|                  |                      |       |            |            |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Sma**  | **10000** | **289.123 μs** | **41.3732 μs** | **45.9862 μs** | **10.79** |    **1.70** | **86.3309** | **43.1655** | **712.33 KB** |        **9.11** |
| ProjectedBuilder | TaLib.Functions.Sma  | 10000 | 308.389 μs | 28.9628 μs | 33.3536 μs | 11.51 |    1.25 | 86.5885 | 42.9688 | 712.36 KB |        9.11 |
| NativeOwned      | TaLib.Functions.Sma  | 10000 |  26.807 μs |  0.6166 μs |  0.7101 μs |  1.00 |    0.04 |  9.4821 |  1.5654 |  78.18 KB |        1.00 |
