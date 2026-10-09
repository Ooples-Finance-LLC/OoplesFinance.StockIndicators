```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=15  IterationTime=250ms  LaunchCount=1
UnrollFactor=1  WarmupCount=8

```
| Method           | PairId               | Count | Mean       | Error      | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|----------------- |--------------------- |------ |-----------:|-----------:|-----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **ArrayBuilder**     | **TaLib.Functions.Asin** | **1000**  |  **34.524 μs** |  **4.2562 μs** |  **3.9813 μs** |  **2.84** |    **0.33** |  **8.2484** |  **1.5755** |  **68.36 KB** |        **8.69** |
| ProjectedBuilder | TaLib.Functions.Asin | 1000  |  37.904 μs |  3.1699 μs |  2.9651 μs |  3.12 |    0.26 |  8.2916 |  1.0951 |  68.97 KB |        8.77 |
| NativeOwned      | TaLib.Functions.Asin | 1000  |  12.163 μs |  0.4651 μs |  0.4351 μs |  1.00 |    0.05 |  0.9508 |       - |   7.87 KB |        1.00 |
|                  |                      |       |            |            |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Asin** | **10000** | **277.239 μs** | **54.7002 μs** | **51.1666 μs** |  **1.76** |    **0.40** | **76.7528** | **76.0148** | **631.33 KB** |        **8.07** |
| ProjectedBuilder | TaLib.Functions.Asin | 10000 | 393.357 μs | 68.9888 μs | 61.1568 μs |  2.50 |    0.51 | 76.5832 | 75.1105 | 631.96 KB |        8.08 |
| NativeOwned      | TaLib.Functions.Asin | 10000 | 159.985 μs | 23.0127 μs | 21.5261 μs |  1.02 |    0.19 |  9.0863 |  1.5144 |  78.18 KB |        1.00 |
|                  |                      |       |            |            |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Sma**  | **1000**  |  **17.014 μs** |  **2.1752 μs** |  **1.8164 μs** |  **6.03** |    **0.70** |  **7.6383** |  **1.2386** |  **63.06 KB** |        **8.02** |
| ProjectedBuilder | TaLib.Functions.Sma  | 1000  |  39.164 μs |  4.8008 μs |  4.0089 μs | 13.87 |    1.55 |  9.5403 |  1.8792 |  79.17 KB |       10.06 |
| NativeOwned      | TaLib.Functions.Sma  | 1000  |   2.831 μs |  0.1774 μs |  0.1573 μs |  1.00 |    0.08 |  0.9563 |  0.0230 |   7.87 KB |        1.00 |
|                  |                      |       |            |            |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Sma**  | **10000** | **199.629 μs** | **18.6233 μs** | **15.5513 μs** |  **7.37** |    **0.64** | **67.2926** | **32.8638** |  **555.7 KB** |        **7.11** |
| ProjectedBuilder | TaLib.Functions.Sma  | 10000 | 304.109 μs | 28.9092 μs | 27.0417 μs | 11.23 |    1.08 | 86.4041 | 43.2020 | 712.43 KB |        9.11 |
| NativeOwned      | TaLib.Functions.Sma  | 10000 |  27.124 μs |  1.2633 μs |  1.1817 μs |  1.00 |    0.06 |  9.4953 |  1.4993 |  78.18 KB |        1.00 |
