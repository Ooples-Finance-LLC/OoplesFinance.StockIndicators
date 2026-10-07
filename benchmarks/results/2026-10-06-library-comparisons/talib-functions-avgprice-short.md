```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error        | StdDev       | Median       | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-------------:|-------------:|-------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Price [24]** |  **1,882.00 μs** |  **1,343.09 μs** |    **73.619 μs** |  **1,865.10 μs** |  **1.00** |    **0.05** |  **291.23 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Price [24] |     35.53 μs |    133.69 μs |     7.328 μs |     34.30 μs |  0.02 |    0.00 |    20.9 KB |        0.07 |
|            |       |                      |              |              |              |              |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)Price [24]** | **11,034.07 μs** | **28,336.84 μs** | **1,553.238 μs** | **10,207.60 μs** |  **1.01** |    **0.17** | **3950.91 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Price [24] |    275.47 μs |  6,070.23 μs |   332.730 μs |    104.80 μs |  0.03 |    0.03 |  161.52 KB |        0.04 |
