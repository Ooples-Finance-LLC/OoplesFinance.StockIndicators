```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=6

```
| Method                        | Case       | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------------------------------ |----------- |-----------:|----------:|----------:|------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| **TalibInPlaceLatestOnlyPayload** | **Asin**       | **2,365.4 μs** | **867.07 μs** | **573.52 μs** |  **1.48** |    **0.35** | **203.1250** | **203.1250** | **203.1250** |  **1564.8 KB** |        **2.00** |
| TalibValues                   | Asin       | 1,603.1 μs |  87.36 μs |  57.79 μs |  1.00 |    0.05 |  93.7500 |  93.7500 |  93.7500 |  782.88 KB |        1.00 |
| OoplesLatestOnlyBuilder       | Asin       | 2,555.7 μs | 125.30 μs |  82.88 μs |  1.60 |    0.08 | 109.3750 | 109.3750 | 109.3750 | 1567.46 KB |        2.00 |
|                               |            |            |           |           |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaDecimal** |   **991.9 μs** | **219.60 μs** | **145.25 μs** |  **2.81** |    **0.44** | **101.5625** | **101.5625** | **101.5625** |  **782.58 KB** |        **1.00** |
| TalibValues                   | SmaDecimal |   354.8 μs |  37.77 μs |  24.99 μs |  1.00 |    0.10 |  91.7969 |  91.7969 |  91.7969 |  782.78 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaDecimal | 1,881.4 μs | 113.73 μs |  75.22 μs |  5.33 |    0.43 |  93.7500 |  93.7500 |  93.7500 |  785.97 KB |        1.00 |
|                               |            |            |           |           |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaGrid**    | **1,149.0 μs** |  **62.58 μs** |  **41.39 μs** |  **4.40** |    **0.16** |  **93.7500** |  **93.7500** |  **93.7500** |  **782.88 KB** |        **1.00** |
| TalibValues                   | SmaGrid    |   261.0 μs |   5.29 μs |   3.15 μs |  1.00 |    0.02 | 101.5625 | 101.5625 | 101.5625 |  782.94 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaGrid    | 1,726.2 μs |  56.45 μs |  37.34 μs |  6.61 |    0.16 |  93.7500 |  93.7500 |  93.7500 |  785.83 KB |        1.00 |
