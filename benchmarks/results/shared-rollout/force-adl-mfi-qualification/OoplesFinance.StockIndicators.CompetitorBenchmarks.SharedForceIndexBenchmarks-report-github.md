```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation            | Count | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------------- |------ |------------:|----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skend(...)Index [21]** | **1000**  |   **305.43 Î¼s** | **76.853 Î¼s** | **50.834 Î¼s** |  **5.38** |    **0.86** |        **-** |        **-** |   **64.84 KB** |        **0.47** |
| BuilderLatestOnly | Skend(...)Index [21] | 1000  |   237.14 Î¼s | 10.993 Î¼s |  5.749 Î¼s |  4.17 |    0.13 |   1.9531 |        - |    17.2 KB |        0.13 |
| NativeSingle      | Skend(...)Index [21] | 1000  |    56.83 Î¼s |  1.993 Î¼s |  1.186 Î¼s |  1.00 |    0.03 |  16.6016 |   3.9063 |  137.15 KB |        1.00 |
| NativeParallel    | Skend(...)Index [21] | 1000  |   152.59 Î¼s |  6.560 Î¼s |  3.904 Î¼s |  2.69 |    0.08 |  83.0078 |  30.2734 |  627.43 KB |        4.57 |
|                   |                      |       |             |           |           |       |         |          |          |            |             |
| **BuilderFull**       | **Skend(...)Index [21]** | **10000** | **2,408.36 Î¼s** | **70.039 Î¼s** | **46.327 Î¼s** |  **2.53** |    **0.14** |  **62.5000** |  **15.6250** |   **627.8 KB** |        **0.46** |
| BuilderLatestOnly | Skend(...)Index [21] | 10000 | 2,369.52 Î¼s | 47.443 Î¼s | 31.381 Î¼s |  2.49 |    0.14 |  15.6250 |        - |  157.95 KB |        0.12 |
| NativeSingle      | Skend(...)Index [21] | 10000 |   956.12 Î¼s | 80.837 Î¼s | 53.469 Î¼s |  1.00 |    0.08 | 164.0625 | 160.1563 | 1367.65 KB |        1.00 |
| NativeParallel    | Skend(...)Index [21] | 10000 | 2,212.83 Î¼s | 84.723 Î¼s | 44.312 Î¼s |  2.32 |    0.13 | 781.2500 | 687.5000 | 6234.96 KB |        4.56 |
