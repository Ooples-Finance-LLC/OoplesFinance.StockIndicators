```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  IterationTime=500ms  LaunchCount=1
UnrollFactor=1  WarmupCount=6

```
| Method                        | Case       | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------------ |----------- |-----------:|---------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **TalibInPlaceLatestOnlyPayload** | **Asin**       | **1,364.6 μs** | **13.63 μs** |  **8.11 μs** |  **1.48** |    **0.01** | **230.3523** | **230.3523** | **230.3523** | **1564.66 KB** |        **2.00** |
| TalibValues                   | Asin       |   920.7 μs |  4.15 μs |  2.17 μs |  1.00 |    0.00 | 125.6932 | 125.6932 | 125.6932 |  783.33 KB |        1.00 |
| OoplesLatestOnlyBuilder       | Asin       | 1,185.7 μs | 17.13 μs | 11.33 μs |  1.29 |    0.01 | 207.2289 | 207.2289 | 207.2289 |  1568.9 KB |        2.00 |
|                               |            |            |          |          |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaDecimal** |   **574.3 μs** | **17.60 μs** | **10.47 μs** |  **2.43** |    **0.07** | **130.1370** | **130.1370** | **130.1370** |   **782.8 KB** |        **1.00** |
| TalibValues                   | SmaDecimal |   236.5 μs |  9.74 μs |  5.80 μs |  1.00 |    0.03 | 117.0213 | 117.0213 | 117.0213 |  783.18 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaDecimal |   569.3 μs |  9.38 μs |  4.90 μs |  2.41 |    0.06 | 129.8405 | 129.8405 | 129.8405 |  786.17 KB |        1.00 |
|                               |            |            |          |          |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaGrid**    |   **564.5 μs** |  **2.66 μs** |  **1.39 μs** |  **2.25** |    **0.03** | **125.0000** | **125.0000** | **125.0000** |  **782.87 KB** |        **1.00** |
| TalibValues                   | SmaGrid    |   250.6 μs |  5.14 μs |  3.06 μs |  1.00 |    0.02 | 113.8790 | 113.8790 | 113.8790 |  783.13 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaGrid    |   543.5 μs |  6.07 μs |  4.02 μs |  2.17 |    0.03 | 126.4865 | 126.4865 | 126.4865 |  786.09 KB |        1.00 |
