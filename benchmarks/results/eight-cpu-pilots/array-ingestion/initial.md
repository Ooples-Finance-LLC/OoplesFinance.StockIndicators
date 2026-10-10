```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=5

```
| Method           | PairId               | Count | Mean       | Error       | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|----------------- |--------------------- |------ |-----------:|------------:|-----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **ArrayBuilder**     | **TaLib.Functions.Asin** | **1000**  |  **46.891 μs** |  **15.1248 μs** | **10.0041 μs** |  **2.52** |    **0.59** |  **7.8125** |  **0.9766** |  **68.93 KB** |        **8.76** |
| ProjectedBuilder | TaLib.Functions.Asin | 1000  |  45.072 μs |   1.4463 μs |  0.9566 μs |  2.42 |    0.27 |  8.3008 |  0.9766 |  68.97 KB |        8.77 |
| NativeOwned      | TaLib.Functions.Asin | 1000  |  18.785 μs |   2.6794 μs |  1.7722 μs |  1.01 |    0.15 |  0.8545 |       - |   7.87 KB |        1.00 |
|                  |                      |       |            |             |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Asin** | **10000** | **536.184 μs** |  **58.6319 μs** | **30.6656 μs** |  **2.86** |    **0.29** | **74.2188** | **70.3125** | **631.94 KB** |        **8.08** |
| ProjectedBuilder | TaLib.Functions.Asin | 10000 | 452.431 μs | 109.9403 μs | 72.7187 μs |  2.41 |    0.42 | 70.3125 | 62.5000 |    632 KB |        8.08 |
| NativeOwned      | TaLib.Functions.Asin | 10000 | 189.042 μs |  23.7769 μs | 15.7270 μs |  1.01 |    0.12 |  7.8125 |       - |   78.2 KB |        1.00 |
|                  |                      |       |            |             |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Sma**  | **1000**  |  **46.236 μs** |   **7.0146 μs** |  **4.6397 μs** | **10.81** |    **1.70** |  **8.7891** |  **0.9766** |  **79.08 KB** |       **10.05** |
| ProjectedBuilder | TaLib.Functions.Sma  | 1000  |  48.333 μs |  13.9544 μs |  9.2300 μs | 11.30 |    2.50 |  9.2773 |  1.4648 |  79.11 KB |       10.06 |
| NativeOwned      | TaLib.Functions.Sma  | 1000  |   4.338 μs |   0.7909 μs |  0.5231 μs |  1.01 |    0.17 |  0.9460 |       - |   7.87 KB |        1.00 |
|                  |                      |       |            |             |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Sma**  | **10000** | **371.288 μs** |  **51.3757 μs** | **33.9819 μs** | **10.70** |    **1.10** | **85.9375** | **42.9688** | **712.34 KB** |        **9.11** |
| ProjectedBuilder | TaLib.Functions.Sma  | 10000 | 550.782 μs |  93.9702 μs | 62.1554 μs | 15.88 |    1.92 | 85.9375 | 42.9688 | 712.39 KB |        9.11 |
| NativeOwned      | TaLib.Functions.Sma  | 10000 |  34.792 μs |   2.9950 μs |  1.9810 μs |  1.00 |    0.08 |  9.5215 |  1.4648 |  78.18 KB |        1.00 |
