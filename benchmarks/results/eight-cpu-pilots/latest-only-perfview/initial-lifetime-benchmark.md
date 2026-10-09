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
| **TalibInPlaceLatestOnlyPayload** | **Asin**       | **1,511.0 μs** |  **72.10 μs** |  **42.91 μs** |  **1.43** |    **0.06** | **218.7500** | **218.7500** | **218.7500** | **1564.73 KB** |        **2.00** |
| TalibValues                   | Asin       | 1,060.4 μs |  53.55 μs |  35.42 μs |  1.00 |    0.04 | 101.5625 | 101.5625 | 101.5625 |  782.97 KB |        1.00 |
| OoplesLatestOnlyBuilder       | Asin       | 1,442.4 μs | 217.59 μs | 143.92 μs |  1.36 |    0.14 | 148.4375 | 148.4375 | 148.4375 | 1568.02 KB |        2.00 |
|                               |            |            |           |           |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaDecimal** |   **622.6 μs** |  **15.20 μs** |   **9.04 μs** |  **2.13** |    **0.04** | **101.5625** | **101.5625** | **101.5625** |  **782.87 KB** |        **1.00** |
| TalibValues                   | SmaDecimal |   292.8 μs |   7.35 μs |   4.86 μs |  1.00 |    0.02 | 107.4219 | 107.4219 | 107.4219 |  783.03 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaDecimal | 1,327.0 μs | 155.02 μs | 102.54 μs |  4.53 |    0.34 | 117.1875 | 117.1875 | 117.1875 |  786.27 KB |        1.00 |
|                               |            |            |           |           |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaGrid**    |   **669.8 μs** |  **51.57 μs** |  **34.11 μs** |  **2.94** |    **0.15** | **125.0000** | **125.0000** | **125.0000** |  **782.73 KB** |        **1.00** |
| TalibValues                   | SmaGrid    |   228.1 μs |   4.65 μs |   2.77 μs |  1.00 |    0.02 | 115.2344 | 115.2344 | 115.2344 |  783.16 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaGrid    |   919.0 μs |  21.77 μs |  11.38 μs |  4.03 |    0.07 | 117.1875 | 117.1875 | 117.1875 |  786.26 KB |        1.00 |
