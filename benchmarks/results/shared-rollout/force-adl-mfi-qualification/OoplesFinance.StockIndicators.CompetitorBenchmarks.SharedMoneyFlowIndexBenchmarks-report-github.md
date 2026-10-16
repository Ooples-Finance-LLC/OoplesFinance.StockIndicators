```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation           | Count | Mean       | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |-------------------- |------ |-----------:|-----------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetMfi**      | **1000**  |  **22.455 Î¼s** |  **4.1194 Î¼s** |  **2.7248 Î¼s** |  **0.32** |    **0.04** |   **7.9346** |   **1.2207** |   **65.38 KB** |        **0.42** |
| BuilderLatestOnly | Skender.GetMfi      | 1000  |  16.985 Î¼s |  1.5151 Î¼s |  1.0022 Î¼s |  0.24 |    0.01 |   2.0752 |   0.1221 |   17.77 KB |        0.11 |
| NativeSingle      | Skender.GetMfi      | 1000  |  71.281 Î¼s |  1.4517 Î¼s |  0.9602 Î¼s |  1.00 |    0.02 |  19.0430 |   3.9063 |  156.75 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 1000  |  27.912 Î¼s |  1.1266 Î¼s |  0.6704 Î¼s |  0.39 |    0.01 |  22.2168 |   5.8594 |  175.88 KB |        1.12 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetMfi**      | **10000** | **153.262 Î¼s** |  **9.9461 Î¼s** |  **6.5788 Î¼s** |  **0.17** |    **0.01** |  **76.1719** |  **37.1094** |  **628.26 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetMfi      | 10000 | 116.800 Î¼s |  2.9867 Î¼s |  1.9755 Î¼s |  0.13 |    0.01 |  18.5547 |   5.8594 |  158.41 KB |        0.10 |
| NativeSingle      | Skender.GetMfi      | 10000 | 882.369 Î¼s | 78.2861 Î¼s | 46.5868 Î¼s |  1.00 |    0.07 | 187.5000 | 140.6250 | 1563.08 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 10000 | 358.715 Î¼s | 40.4611 Î¼s | 26.7625 Î¼s |  0.41 |    0.04 | 205.0781 | 140.6250 | 1582.12 KB |        1.01 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **1000**  |  **21.641 Î¼s** |  **1.6597 Î¼s** |  **1.0978 Î¼s** |  **2.89** |    **0.14** |   **7.9346** |   **1.2207** |   **65.38 KB** |        **8.16** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 1000  |  17.319 Î¼s |  3.2864 Î¼s |  2.1737 Î¼s |  2.31 |    0.28 |   2.0752 |   0.1221 |   17.77 KB |        2.22 |
| NativeSingle      | TaLib.Functions.Mfi | 1000  |   7.487 Î¼s |  0.1346 Î¼s |  0.0704 Î¼s |  1.00 |    0.01 |   0.9766 |        - |    8.01 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 1000  |   4.919 Î¼s |  0.4618 Î¼s |  0.3054 Î¼s |  0.66 |    0.04 |   1.2512 |   0.0305 |   10.13 KB |        1.27 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **10000** | **148.360 Î¼s** |  **4.1902 Î¼s** |  **2.7716 Î¼s** |  **2.01** |    **0.08** |  **76.1719** |  **37.1094** |  **628.26 KB** |        **8.02** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 10000 | 115.763 Î¼s |  2.4751 Î¼s |  1.4729 Î¼s |  1.57 |    0.06 |  18.5547 |   5.8594 |  158.41 KB |        2.02 |
| NativeSingle      | TaLib.Functions.Mfi | 10000 |  73.982 Î¼s |  3.9790 Î¼s |  2.6319 Î¼s |  1.00 |    0.05 |   9.2773 |   1.4648 |   78.33 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 10000 |  19.528 Î¼s |  0.7537 Î¼s |  0.4985 Î¼s |  0.26 |    0.01 |   9.8877 |   1.9531 |   80.45 KB |        1.03 |
