```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=5  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                  | Case       | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------ |----------- |-----------:|----------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **TalibLatestOnlyPayload**  | **Asin**       | **1,642.7 μs** |  **21.86 μs** |   **3.38 μs** |  **1.74** |    **0.02** | **203.1250** | **203.1250** | **203.1250** | **2346.02 KB** |        **3.00** |
| TalibValues             | Asin       |   946.8 μs |  45.03 μs |  11.69 μs |  1.00 |    0.02 | 109.3750 | 109.3750 | 109.3750 |  783.02 KB |        1.00 |
| OoplesLatestOnlyBuilder | Asin       | 1,254.2 μs |  43.45 μs |  11.28 μs |  1.32 |    0.02 | 195.3125 | 195.3125 | 195.3125 | 1568.51 KB |        2.00 |
|                         |            |            |           |           |       |         |          |          |          |            |             |
| **TalibLatestOnlyPayload**  | **SmaDecimal** |   **692.1 μs** | **122.59 μs** |  **31.84 μs** |  **2.69** |    **0.14** | **203.1250** | **203.1250** | **203.1250** | **1565.28 KB** |        **2.00** |
| TalibValues             | SmaDecimal |   257.8 μs |  32.71 μs |   8.50 μs |  1.00 |    0.04 | 111.3281 | 111.3281 | 111.3281 |  783.01 KB |        1.00 |
| OoplesLatestOnlyBuilder | SmaDecimal | 2,979.4 μs | 902.80 μs | 139.71 μs | 11.57 |    0.60 |  93.7500 |  93.7500 |  93.7500 |   785.9 KB |        1.00 |
|                         |            |            |           |           |       |         |          |          |          |            |             |
| **TalibLatestOnlyPayload**  | **SmaGrid**    |   **779.7 μs** | **139.00 μs** |  **36.10 μs** |  **3.35** |    **0.15** | **214.8438** | **214.8438** | **214.8438** | **1565.21 KB** |        **2.00** |
| TalibValues             | SmaGrid    |   232.8 μs |  28.17 μs |   4.36 μs |  1.00 |    0.02 | 115.2344 | 115.2344 | 115.2344 |  783.07 KB |        1.00 |
| OoplesLatestOnlyBuilder | SmaGrid    |   868.2 μs | 108.25 μs |  16.75 μs |  3.73 |    0.09 | 109.3750 | 109.3750 | 109.3750 |  785.99 KB |        1.00 |
