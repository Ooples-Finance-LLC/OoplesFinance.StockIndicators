```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=6

```
| Method                        | Case       | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------------ |----------- |-----------:|----------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **TalibInPlaceLatestOnlyPayload** | **Asin**       | **1,494.1 μs** |  **22.41 μs** | **11.72 μs** |  **1.46** |    **0.04** | **218.7500** | **218.7500** | **218.7500** | **1564.66 KB** |        **2.00** |
| TalibValues                   | Asin       | 1,024.2 μs |  41.63 μs | 27.53 μs |  1.00 |    0.04 | 109.3750 | 109.3750 | 109.3750 |  783.09 KB |        1.00 |
| OoplesLatestOnlyBuilder       | Asin       | 1,632.8 μs | 113.55 μs | 75.11 μs |  1.60 |    0.08 | 179.6875 | 179.6875 | 179.6875 | 1568.46 KB |        2.00 |
|                               |            |            |           |          |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaDecimal** |   **700.6 μs** |  **40.03 μs** | **26.48 μs** |  **2.76** |    **0.11** | **125.0000** | **125.0000** | **125.0000** |   **782.8 KB** |        **1.00** |
| TalibValues                   | SmaDecimal |   254.3 μs |   6.04 μs |  3.99 μs |  1.00 |    0.02 | 113.2813 | 113.2813 | 113.2813 |  783.13 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaDecimal |   927.3 μs |  36.36 μs | 19.02 μs |  3.65 |    0.09 | 117.1875 | 117.1875 | 117.1875 |  786.31 KB |        1.00 |
|                               |            |            |           |          |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaGrid**    |   **631.2 μs** |  **40.75 μs** | **24.25 μs** |  **2.76** |    **0.11** | **125.0000** | **125.0000** | **125.0000** |  **782.59 KB** |        **1.00** |
| TalibValues                   | SmaGrid    |   228.7 μs |   4.75 μs |  2.83 μs |  1.00 |    0.02 | 117.1875 | 117.1875 | 117.1875 |  783.18 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaGrid    |   799.7 μs |   9.93 μs |  5.19 μs |  3.50 |    0.05 | 117.1875 | 117.1875 | 117.1875 |  786.09 KB |        1.00 |
