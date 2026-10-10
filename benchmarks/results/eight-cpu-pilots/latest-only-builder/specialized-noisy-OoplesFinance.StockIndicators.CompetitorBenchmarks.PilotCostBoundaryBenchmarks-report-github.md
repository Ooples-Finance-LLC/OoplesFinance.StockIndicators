```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=5  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                  | Case       | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------ |----------- |-----------:|----------:|---------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **TalibLatestOnlyPayload**  | **Asin**       | **1,703.5 μs** | **107.43 μs** | **27.90 μs** |  **1.26** |    **0.03** | **250.0000** | **250.0000** | **250.0000** | **2346.96 KB** |        **3.00** |
| TalibValues             | Asin       | 1,353.8 μs | 111.01 μs | 28.83 μs |  1.00 |    0.03 | 101.5625 | 101.5625 | 101.5625 |  782.93 KB |        1.00 |
| OoplesLatestOnlyBuilder | Asin       | 2,068.1 μs | 143.09 μs | 37.16 μs |  1.53 |    0.04 | 140.6250 | 140.6250 | 140.6250 | 1567.82 KB |        2.00 |
|                         |            |            |           |          |       |         |          |          |          |            |             |
| **TalibLatestOnlyPayload**  | **SmaDecimal** | **1,334.3 μs** | **122.66 μs** | **31.85 μs** |  **2.99** |    **0.09** | **132.8125** | **132.8125** | **132.8125** | **1564.74 KB** |        **2.00** |
| TalibValues             | SmaDecimal |   446.0 μs |  67.52 μs | 10.45 μs |  1.00 |    0.03 |  85.9375 |  85.9375 |  85.9375 |  782.63 KB |        1.00 |
| OoplesLatestOnlyBuilder | SmaDecimal | 1,839.4 μs | 163.12 μs | 42.36 μs |  4.13 |    0.12 |  62.5000 |  62.5000 |  62.5000 |   785.4 KB |        1.00 |
|                         |            |            |           |          |       |         |          |          |          |            |             |
| **TalibLatestOnlyPayload**  | **SmaGrid**    | **1,339.7 μs** | **225.28 μs** | **34.86 μs** |  **3.24** |    **0.09** | **171.8750** | **171.8750** | **171.8750** | **1565.23 KB** |        **2.00** |
| TalibValues             | SmaGrid    |   413.9 μs |  49.49 μs |  7.66 μs |  1.00 |    0.02 |  89.8438 |  89.8438 |  89.8438 |  782.69 KB |        1.00 |
| OoplesLatestOnlyBuilder | SmaGrid    |   954.6 μs | 134.39 μs | 20.80 μs |  2.31 |    0.06 | 117.1875 | 117.1875 | 117.1875 |  786.17 KB |        1.00 |
