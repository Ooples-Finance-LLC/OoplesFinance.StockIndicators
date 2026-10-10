```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=10  LaunchCount=1  UnrollFactor=1  
WarmupCount=6  

```
| Method                  | Case       | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------ |----------- |-----------:|----------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **TalibLatestOnlyPayload**  | **Asin**       | **1,716.9 μs** | **125.34 μs** | **82.91 μs** |  **1.64** |    **0.09** | **265.6250** | **265.6250** | **265.6250** | **2347.16 KB** |        **3.00** |
| TalibValues             | Asin       | 1,050.1 μs |  56.56 μs | 29.58 μs |  1.00 |    0.04 | 117.1875 | 117.1875 | 117.1875 |  783.25 KB |        1.00 |
| OoplesValues            | Asin       |   887.1 μs |  24.33 μs | 14.48 μs |  0.85 |    0.03 | 109.3750 | 109.3750 | 109.3750 |  782.21 KB |        1.00 |
| OoplesLatestOnlyBuilder | Asin       | 1,437.3 μs |  46.24 μs | 30.59 μs |  1.37 |    0.05 | 187.5000 | 187.5000 | 187.5000 | 1568.67 KB |        2.00 |
|                         |            |            |           |          |       |         |          |          |          |            |             |
| **TalibLatestOnlyPayload**  | **SmaDecimal** |   **690.6 μs** |  **16.33 μs** | **10.80 μs** |  **2.72** |    **0.05** | **203.1250** | **203.1250** | **203.1250** | **1565.54 KB** |        **2.00** |
| TalibValues             | SmaDecimal |   253.9 μs |   4.29 μs |  2.84 μs |  1.00 |    0.02 | 103.5156 | 103.5156 | 103.5156 |  782.97 KB |        1.00 |
| OoplesValues            | SmaDecimal | 2,141.4 μs |  57.49 μs | 34.21 μs |  8.44 |    0.16 |  93.7500 |  93.7500 |  93.7500 |  782.14 KB |        1.00 |
| OoplesLatestOnlyBuilder | SmaDecimal | 1,100.9 μs |  50.14 μs | 33.16 μs |  4.34 |    0.13 | 117.1875 | 117.1875 | 117.1875 |  786.24 KB |        1.00 |
|                         |            |            |           |          |       |         |          |          |          |            |             |
| **TalibLatestOnlyPayload**  | **SmaGrid**    |   **690.6 μs** |  **16.27 μs** |  **9.68 μs** |  **2.55** |    **0.16** | **214.8438** | **214.8438** | **214.8438** | **1565.16 KB** |        **2.00** |
| TalibValues             | SmaGrid    |   271.5 μs |  25.98 μs | 17.19 μs |  1.00 |    0.09 | 111.3281 | 111.3281 | 111.3281 |  783.09 KB |        1.00 |
| OoplesValues            | SmaGrid    |   730.8 μs | 108.99 μs | 72.09 μs |  2.70 |    0.30 | 121.0938 | 121.0938 | 121.0938 |  782.29 KB |        1.00 |
| OoplesLatestOnlyBuilder | SmaGrid    | 1,007.0 μs |  69.33 μs | 45.85 μs |  3.72 |    0.28 | 117.1875 | 117.1875 | 117.1875 |  786.21 KB |        1.00 |
