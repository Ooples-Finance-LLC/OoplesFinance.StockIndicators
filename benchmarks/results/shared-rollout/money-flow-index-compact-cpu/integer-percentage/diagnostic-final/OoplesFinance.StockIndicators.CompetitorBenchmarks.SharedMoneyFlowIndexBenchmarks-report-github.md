```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation           | Count | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |-------------------- |------ |-------------:|------------:|------------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetMfi**      | **1000**  |    **24.256 Î¼s** |   **2.5542 Î¼s** |   **1.6895 Î¼s** |  **0.29** |    **0.03** |   **7.8125** |   **0.9766** |   **65.35 KB** |        **0.42** |
| BuilderLatestOnly | Skender.GetMfi      | 1000  |    17.090 Î¼s |   1.5345 Î¼s |   1.0150 Î¼s |  0.21 |    0.02 |   2.0752 |   0.1221 |   17.77 KB |        0.11 |
| NativeSingle      | Skender.GetMfi      | 1000  |    82.997 Î¼s |   9.2103 Î¼s |   4.8171 Î¼s |  1.00 |    0.08 |  18.5547 |   3.9063 |  156.76 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 1000  |    34.141 Î¼s |   0.9805 Î¼s |   0.5835 Î¼s |  0.41 |    0.02 |  22.2168 |   6.5918 |  175.88 KB |        1.12 |
|                   |                     |       |              |             |             |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetMfi**      | **10000** |   **158.943 Î¼s** |  **15.1280 Î¼s** |  **10.0062 Î¼s** |  **0.16** |    **0.03** |  **76.1719** |  **37.1094** |  **628.23 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetMfi      | 10000 |   115.124 Î¼s |   4.6314 Î¼s |   3.0634 Î¼s |  0.11 |    0.02 |  18.5547 |   5.8594 |  158.41 KB |        0.10 |
| NativeSingle      | Skender.GetMfi      | 10000 | 1,044.678 Î¼s | 311.9045 Î¼s | 206.3055 Î¼s |  1.03 |    0.26 | 187.5000 | 140.6250 | 1563.06 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 10000 |   341.265 Î¼s |   9.5936 Î¼s |   5.7090 Î¼s |  0.34 |    0.06 | 203.1250 | 144.5313 | 1582.13 KB |        1.01 |
|                   |                     |       |              |             |             |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **1000**  |    **26.784 Î¼s** |   **1.3976 Î¼s** |   **0.9244 Î¼s** |  **2.75** |    **0.35** |   **7.8125** |   **1.2207** |   **65.35 KB** |        **8.16** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 1000  |    25.615 Î¼s |  10.8418 Î¼s |   7.1712 Î¼s |  2.63 |    0.78 |   2.0752 |        - |   17.77 KB |        2.22 |
| NativeSingle      | TaLib.Functions.Mfi | 1000  |     9.869 Î¼s |   1.8064 Î¼s |   1.1948 Î¼s |  1.01 |    0.17 |   0.9766 |        - |    8.01 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 1000  |     6.713 Î¼s |   1.1312 Î¼s |   0.5917 Î¼s |  0.69 |    0.10 |   0.9766 |        - |   10.14 KB |        1.27 |
|                   |                     |       |              |             |             |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **10000** |   **197.299 Î¼s** |  **28.6216 Î¼s** |  **17.0322 Î¼s** |  **2.01** |    **0.20** |  **76.1719** |  **37.1094** |  **628.24 KB** |        **8.02** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 10000 |   129.523 Î¼s |   5.1348 Î¼s |   3.3964 Î¼s |  1.32 |    0.09 |  18.5547 |   5.8594 |  158.41 KB |        2.02 |
| NativeSingle      | TaLib.Functions.Mfi | 10000 |    98.348 Î¼s |   9.2421 Î¼s |   6.1131 Î¼s |  1.00 |    0.08 |   8.7891 |   0.9766 |   78.33 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 10000 |    23.378 Î¼s |   0.8479 Î¼s |   0.5608 Î¼s |  0.24 |    0.02 |   9.8877 |   1.9531 |   80.45 KB |        1.03 |
