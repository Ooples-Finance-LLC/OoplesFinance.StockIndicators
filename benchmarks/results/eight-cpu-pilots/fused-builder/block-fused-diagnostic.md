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
| **ArrayBuilder**     | **TaLib.Functions.Asin** | **1000**  |  **30.116 μs** |  **0.8631 μs** |  **0.8073 μs** |  **1.79** |    **0.07** |  **7.8486** |  **1.5005** |  **65.03 KB** |        **8.26** |
| ProjectedBuilder | TaLib.Functions.Asin | 1000  |  36.811 μs |  4.5159 μs |  4.2242 μs |  2.19 |    0.25 |  8.3051 |  1.0766 |  68.97 KB |        8.77 |
| NativeOwned      | TaLib.Functions.Asin | 1000  |  16.855 μs |  0.5194 μs |  0.4604 μs |  1.00 |    0.04 |  0.9358 |       - |   7.87 KB |        1.00 |
|                  |                      |       |            |            |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Asin** | **10000** | **223.923 μs** | **34.8213 μs** | **32.5719 μs** |  **1.37** |    **0.23** | **75.6303** | **43.0672** |    **628 KB** |        **8.03** |
| ProjectedBuilder | TaLib.Functions.Asin | 10000 | 333.004 μs | 19.1830 μs | 17.9438 μs |  2.03 |    0.21 | 76.5306 | 75.2551 | 631.96 KB |        8.08 |
| NativeOwned      | TaLib.Functions.Asin | 10000 | 164.844 μs | 14.8429 μs | 13.8841 μs |  1.01 |    0.12 |  9.4659 |  1.3523 |  78.18 KB |        1.00 |
|                  |                      |       |            |            |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Sma**  | **1000**  |  **12.234 μs** |  **0.9406 μs** |  **0.8799 μs** |  **4.63** |    **0.35** |  **6.9713** |  **1.1460** |   **57.3 KB** |        **7.28** |
| ProjectedBuilder | TaLib.Functions.Sma  | 1000  |  32.488 μs |  1.6539 μs |  1.5471 μs | 12.29 |    0.69 |  9.5767 |  1.8917 |  79.17 KB |       10.06 |
| NativeOwned      | TaLib.Functions.Sma  | 1000  |   2.645 μs |  0.0927 μs |  0.0867 μs |  1.00 |    0.05 |  0.9604 |  0.0223 |   7.87 KB |        1.00 |
|                  |                      |       |            |            |            |       |         |         |         |           |             |
| **ArrayBuilder**     | **TaLib.Functions.Sma**  | **10000** | **111.612 μs** | **11.1449 μs** | **10.4249 μs** |  **4.24** |    **0.40** | **66.5037** | **22.4939** | **549.93 KB** |        **7.03** |
| ProjectedBuilder | TaLib.Functions.Sma  | 10000 | 281.700 μs | 25.7069 μs | 24.0463 μs | 10.69 |    0.93 | 86.1141 | 43.0571 | 712.43 KB |        9.11 |
| NativeOwned      | TaLib.Functions.Sma  | 10000 |  26.361 μs |  0.7910 μs |  0.7399 μs |  1.00 |    0.04 |  9.4312 |  1.5884 |  78.18 KB |        1.00 |
