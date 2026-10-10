```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=6

```
| Method                        | Case       | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|------------------------------ |----------- |-----------:|---------:|---------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| **TalibInPlaceLatestOnlyPayload** | **Asin**       | **1,495.3 Î¼s** | **53.19 Î¼s** | **35.19 Î¼s** |  **1.57** |    **0.04** | **203.1250** | **203.1250** | **203.1250** | **1564.8 KB** |        **2.00** |
| TalibValues                   | Asin       |   954.3 Î¼s | 15.89 Î¼s | 10.51 Î¼s |  1.00 |    0.01 | 109.3750 | 109.3750 | 109.3750 | 783.11 KB |        1.00 |
| OoplesLatestOnlyBuilder       | Asin       | 1,343.9 Î¼s | 39.60 Î¼s | 26.19 Î¼s |  1.41 |    0.03 | 156.2500 | 156.2500 | 156.2500 | 1568.1 KB |        2.00 |
|                               |            |            |          |          |       |         |          |          |          |           |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaDecimal** |   **597.0 Î¼s** | **12.32 Î¼s** |  **6.44 Î¼s** |  **2.54** |    **0.04** |  **97.6563** |  **97.6563** |  **97.6563** | **782.95 KB** |        **1.00** |
| TalibValues                   | SmaDecimal |   234.9 Î¼s |  5.78 Î¼s |  3.44 Î¼s |  1.00 |    0.02 | 113.2813 | 113.2813 | 113.2813 | 783.13 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaDecimal |   875.0 Î¼s | 21.30 Î¼s | 14.09 Î¼s |  3.73 |    0.08 | 117.1875 | 117.1875 | 117.1875 | 786.22 KB |        1.00 |
|                               |            |            |          |          |       |         |          |          |          |           |             |
| **TalibInPlaceLatestOnlyPayload** | **SmaGrid**    |   **860.6 Î¼s** | **43.25 Î¼s** | **28.60 Î¼s** |  **3.12** |    **0.50** |  **93.7500** |  **93.7500** |  **93.7500** | **782.89 KB** |        **1.00** |
| TalibValues                   | SmaGrid    |   284.4 Î¼s | 85.53 Î¼s | 56.57 Î¼s |  1.03 |    0.26 | 105.4688 | 105.4688 | 105.4688 |    783 KB |        1.00 |
| OoplesLatestOnlyBuilder       | SmaGrid    | 1,115.4 Î¼s | 49.48 Î¼s | 29.45 Î¼s |  4.04 |    0.65 |  85.9375 |  85.9375 |  85.9375 |  785.5 KB |        1.00 |
