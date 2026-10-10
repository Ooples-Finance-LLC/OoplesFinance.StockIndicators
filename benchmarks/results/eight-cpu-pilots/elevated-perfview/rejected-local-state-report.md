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
| **TalibInPlaceLatestOnlyPayload** | **Asin**       | **1,504.0 Î¼s** |  **76.37 Î¼s** |  **45.45 Î¼s** |  **1.56** |    **0.05** | **171.8750** | **171.8750** | **171.8750** | **1564.69 KB** |        **2.00** |
| TalibValues                   | Asin       |   963.3 Î¼s |  13.55 Î¼s |   8.96 Î¼s |  1.00 |    0.01 | 109.3750 | 109.3750 | 109.3750 |  783.09 KB |        1.00 |
| OoplesLatestOnlyBuilder       | Asin       | 1,482.7 Î¼s | 208.51 Î¼s | 137.92 Î¼s |  1.54 |    0.14 | 140.6250 | 140.6250 | 140.6250 | 1567.94 KB |        2.00 |
|                               |            |            |           |           |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaDecimal** |   **609.3 Î¼s** |  **22.03 Î¼s** |  **14.57 Î¼s** |  **2.45** |    **0.10** |  **78.1250** |  **78.1250** |  **78.1250** |   **782.7 KB** |        **1.00** |
| TalibValues                   | SmaDecimal |   249.1 Î¼s |  13.91 Î¼s |   9.20 Î¼s |  1.00 |    0.05 | 105.4688 | 105.4688 | 105.4688 |     783 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaDecimal |   984.8 Î¼s |  63.09 Î¼s |  37.55 Î¼s |  3.96 |    0.20 | 117.1875 | 117.1875 | 117.1875 |  786.19 KB |        1.00 |
|                               |            |            |           |           |       |         |          |          |          |            |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaGrid**    |   **594.1 Î¼s** |  **28.05 Î¼s** |  **16.69 Î¼s** |  **2.45** |    **0.10** | **109.3750** | **109.3750** | **109.3750** |     **783 KB** |        **1.00** |
| TalibValues                   | SmaGrid    |   243.1 Î¼s |  10.59 Î¼s |   7.01 Î¼s |  1.00 |    0.04 | 113.2813 | 113.2813 | 113.2813 |  783.13 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaGrid    |   796.7 Î¼s |  17.29 Î¼s |  10.29 Î¼s |  3.28 |    0.10 | 128.9063 | 128.9063 | 128.9063 |  786.12 KB |        1.00 |
