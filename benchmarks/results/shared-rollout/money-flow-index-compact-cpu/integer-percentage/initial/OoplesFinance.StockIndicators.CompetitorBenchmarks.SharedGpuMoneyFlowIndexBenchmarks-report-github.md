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
| **BuilderFull**       | **Skender.GetMfi**      | **1000**  |  **22.900 Î¼s** |  **1.8172 Î¼s** |  **1.2019 Î¼s** |  **0.30** |    **0.02** |   **7.8125** |   **0.9766** |   **65.38 KB** |        **0.42** |
| BuilderLatestOnly | Skender.GetMfi      | 1000  |  20.422 Î¼s |  2.4664 Î¼s |  1.4677 Î¼s |  0.27 |    0.02 |   2.0752 |   0.1221 |   17.77 KB |        0.11 |
| GpuFull           | Skender.GetMfi      | 1000  | 441.000 Î¼s | 17.4131 Î¼s | 10.3623 Î¼s |  5.76 |    0.32 |   7.8125 |        - |   64.59 KB |        0.41 |
| GpuLatestOnly     | Skender.GetMfi      | 1000  | 433.594 Î¼s |  9.0488 Î¼s |  5.9852 Î¼s |  5.66 |    0.29 |        - |        - |   16.97 KB |        0.11 |
| NativeSingle      | Skender.GetMfi      | 1000  |  76.804 Î¼s |  6.8302 Î¼s |  4.0646 Î¼s |  1.00 |    0.07 |  19.0430 |   3.9063 |  156.75 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 1000  |  28.558 Î¼s |  2.8263 Î¼s |  1.8694 Î¼s |  0.37 |    0.03 |  22.2168 |   6.8359 |  175.88 KB |        1.12 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetMfi**      | **10000** | **173.646 Î¼s** |  **5.3019 Î¼s** |  **3.5069 Î¼s** |  **0.20** |    **0.01** |  **76.1719** |  **37.1094** |  **628.26 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetMfi      | 10000 | 130.937 Î¼s |  3.8054 Î¼s |  2.5170 Î¼s |  0.15 |    0.00 |  18.5547 |   5.8594 |  158.41 KB |        0.10 |
| GpuFull           | Skender.GetMfi      | 10000 | 824.232 Î¼s | 24.7260 Î¼s | 14.7141 Î¼s |  0.96 |    0.02 |  70.3125 |  23.4375 |  627.49 KB |        0.40 |
| GpuLatestOnly     | Skender.GetMfi      | 10000 | 785.225 Î¼s | 23.6622 Î¼s | 15.6511 Î¼s |  0.92 |    0.02 |  15.6250 |        - |  157.64 KB |        0.10 |
| NativeSingle      | Skender.GetMfi      | 10000 | 854.719 Î¼s | 24.2884 Î¼s | 14.4536 Î¼s |  1.00 |    0.02 | 187.5000 | 140.6250 | 1563.07 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 10000 | 363.244 Î¼s | 37.6209 Î¼s | 22.3876 Î¼s |  0.43 |    0.03 | 205.0781 | 146.4844 | 1582.12 KB |        1.01 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **1000**  |  **22.852 Î¼s** |  **1.1737 Î¼s** |  **0.7764 Î¼s** |  **2.95** |    **0.16** |   **7.9346** |   **1.2207** |   **65.38 KB** |        **8.16** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 1000  |  18.626 Î¼s |  1.5948 Î¼s |  0.9491 Î¼s |  2.40 |    0.16 |   2.0752 |   0.1221 |   17.77 KB |        2.22 |
| GpuFull           | TaLib.Functions.Mfi | 1000  | 429.580 Î¼s | 14.0965 Î¼s |  9.3239 Î¼s | 55.38 |    2.75 |   7.8125 |        - |   64.59 KB |        8.06 |
| GpuLatestOnly     | TaLib.Functions.Mfi | 1000  | 419.125 Î¼s | 11.9043 Î¼s |  7.8739 Î¼s | 54.03 |    2.63 |        - |        - |   16.97 KB |        2.12 |
| NativeSingle      | TaLib.Functions.Mfi | 1000  |   7.773 Î¼s |  0.5771 Î¼s |  0.3817 Î¼s |  1.00 |    0.07 |   0.9766 |        - |    8.01 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 1000  |   5.706 Î¼s |  0.9328 Î¼s |  0.6170 Î¼s |  0.74 |    0.08 |   1.2512 |   0.0305 |   10.13 KB |        1.27 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **10000** | **161.722 Î¼s** |  **1.1738 Î¼s** |  **0.6139 Î¼s** |  **2.24** |    **0.01** |  **76.1719** |  **37.1094** |  **628.26 KB** |        **8.02** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 10000 | 133.708 Î¼s |  8.1318 Î¼s |  5.3787 Î¼s |  1.85 |    0.07 |  18.5547 |   5.8594 |  158.41 KB |        2.02 |
| GpuFull           | TaLib.Functions.Mfi | 10000 | 842.121 Î¼s | 19.8076 Î¼s | 13.1015 Î¼s | 11.68 |    0.18 |  70.3125 |  23.4375 |  627.49 KB |        8.01 |
| GpuLatestOnly     | TaLib.Functions.Mfi | 10000 | 839.883 Î¼s | 42.6234 Î¼s | 28.1928 Î¼s | 11.65 |    0.38 |  15.6250 |        - |  157.64 KB |        2.01 |
| NativeSingle      | TaLib.Functions.Mfi | 10000 |  72.084 Î¼s |  0.6082 Î¼s |  0.4023 Î¼s |  1.00 |    0.01 |   9.2773 |   1.4648 |   78.32 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 10000 |  22.030 Î¼s |  1.7712 Î¼s |  1.0540 Î¼s |  0.31 |    0.01 |   9.8877 |   1.9531 |   80.45 KB |        1.03 |
