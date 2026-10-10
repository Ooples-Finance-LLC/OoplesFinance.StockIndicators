```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=6

```
| Method                        | Case       | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------------ |----------- |-----------:|---------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **TalibInPlaceLatestOnlyPayload** | **Asin**       | **1,417.6 μs** | **21.36 μs** | **14.13 μs** |  **1.47** |    **0.04** | **218.7500** | **218.7500** | **218.7500** | **1564.66 KB** |        **2.00** |
| TalibValues                   | Asin       |   965.6 μs | 44.59 μs | 29.49 μs |  1.00 |    0.04 | 109.3750 | 109.3750 | 109.3750 |  783.13 KB |        1.00 |
| OoplesLatestOnlyBuilder       | Asin       | 1,177.3 μs | 19.61 μs | 12.97 μs |  1.22 |    0.04 | 195.3125 | 195.3125 | 195.3125 | 1568.65 KB |        2.00 |
|                               |            |            |          |          |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaDecimal** |   **575.1 μs** | **11.36 μs** |  **6.76 μs** |  **2.37** |    **0.16** | **125.0000** | **125.0000** | **125.0000** |  **782.75 KB** |        **1.00** |
| TalibValues                   | SmaDecimal |   243.7 μs | 25.75 μs | 17.03 μs |  1.00 |    0.09 | 119.1406 | 119.1406 | 119.1406 |  783.22 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaDecimal |   616.2 μs | 43.09 μs | 28.50 μs |  2.54 |    0.20 | 109.3750 | 109.3750 | 109.3750 |  785.81 KB |        1.00 |
|                               |            |            |          |          |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaGrid**    |   **536.0 μs** |  **3.16 μs** |  **2.09 μs** |  **2.06** |    **0.11** | **121.0938** | **121.0938** | **121.0938** |  **782.87 KB** |        **1.00** |
| TalibValues                   | SmaGrid    |   260.9 μs | 23.32 μs | 15.43 μs |  1.00 |    0.08 | 111.3281 | 111.3281 | 111.3281 |   783.1 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaGrid    |   556.7 μs |  3.20 μs |  2.11 μs |  2.14 |    0.12 | 125.0000 | 125.0000 | 125.0000 |  786.09 KB |        1.00 |
