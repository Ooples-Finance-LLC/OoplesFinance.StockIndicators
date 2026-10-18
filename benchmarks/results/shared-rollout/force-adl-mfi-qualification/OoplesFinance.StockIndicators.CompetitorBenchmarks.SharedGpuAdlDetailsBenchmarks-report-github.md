```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0      | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------- |------ |------------:|-----------:|-----------:|------:|--------:|----------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetAdl** | **1000**  |    **68.11 Î¼s** |   **1.760 Î¼s** |   **0.921 Î¼s** |  **0.80** |    **0.04** |   **10.7422** |   **1.9531** |   **88.32 KB** |        **0.50** |
| BuilderLatestOnly | Skender.GetAdl | 1000  |    64.96 Î¼s |   3.365 Î¼s |   2.225 Î¼s |  0.76 |    0.04 |    4.8828 |   0.4883 |    40.7 KB |        0.23 |
| GpuFull           | Skender.GetAdl | 1000  |   444.74 Î¼s |  15.532 Î¼s |  10.273 Î¼s |  5.22 |    0.27 |    7.8125 |        - |   88.54 KB |        0.50 |
| GpuLatestOnly     | Skender.GetAdl | 1000  |   441.86 Î¼s |  21.431 Î¼s |  14.176 Î¼s |  5.18 |    0.29 |    3.9063 |        - |   40.93 KB |        0.23 |
| NativeSingle      | Skender.GetAdl | 1000  |    85.45 Î¼s |   6.368 Î¼s |   4.212 Î¼s |  1.00 |    0.07 |   21.4844 |   5.8594 |  176.22 KB |        1.00 |
| NativeParallel    | Skender.GetAdl | 1000  |   196.60 Î¼s |  16.012 Î¼s |  10.591 Î¼s |  2.31 |    0.16 |  103.5156 |  42.9688 |   802.9 KB |        4.56 |
|                   |                |       |             |            |            |       |         |           |          |            |             |
| **BuilderFull**       | **Skender.GetAdl** | **10000** |   **677.37 Î¼s** |  **29.139 Î¼s** |  **19.274 Î¼s** |  **0.70** |    **0.03** |  **101.5625** |  **50.7813** |  **862.17 KB** |        **0.49** |
| BuilderLatestOnly | Skender.GetAdl | 10000 |   634.06 Î¼s |  21.462 Î¼s |  14.196 Î¼s |  0.66 |    0.02 |   46.8750 |  15.6250 |  392.33 KB |        0.22 |
| GpuFull           | Skender.GetAdl | 10000 | 1,112.01 Î¼s |  16.025 Î¼s |   9.536 Î¼s |  1.16 |    0.03 |  101.5625 |  46.8750 |   862.4 KB |        0.49 |
| GpuLatestOnly     | Skender.GetAdl | 10000 | 1,072.93 Î¼s |  13.012 Î¼s |   8.607 Î¼s |  1.12 |    0.03 |   46.8750 |  23.4375 |  392.56 KB |        0.22 |
| NativeSingle      | Skender.GetAdl | 10000 |   961.91 Î¼s |  38.224 Î¼s |  25.283 Î¼s |  1.00 |    0.04 |  210.9375 | 140.6250 | 1758.32 KB |        1.00 |
| NativeParallel    | Skender.GetAdl | 10000 | 3,244.14 Î¼s | 204.361 Î¼s | 121.612 Î¼s |  3.37 |    0.15 | 1000.0000 | 968.7500 | 7992.61 KB |        4.55 |
