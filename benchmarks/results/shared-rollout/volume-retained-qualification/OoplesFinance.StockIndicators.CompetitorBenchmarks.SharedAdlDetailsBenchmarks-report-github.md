```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------- |------ |------------:|-----------:|----------:|------:|--------:|----------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetAdl** | **1000**  |    **67.49 Î¼s** |   **1.781 Î¼s** |  **1.060 Î¼s** |  **0.85** |    **0.02** |   **10.7422** |   **1.9531** |   **88.31 KB** |        **0.50** |
| BuilderLatestOnly | Skender.GetAdl | 1000  |    70.48 Î¼s |  12.713 Î¼s |  8.409 Î¼s |  0.88 |    0.10 |    4.8828 |   0.4883 |    40.7 KB |        0.23 |
| NativeSingle      | Skender.GetAdl | 1000  |    79.79 Î¼s |   1.959 Î¼s |  1.025 Î¼s |  1.00 |    0.02 |   21.4844 |   5.8594 |  176.22 KB |        1.00 |
| NativeParallel    | Skender.GetAdl | 1000  |   195.71 Î¼s |   4.618 Î¼s |  3.055 Î¼s |  2.45 |    0.05 |  103.5156 |  42.9688 |  803.22 KB |        4.56 |
|                   |                |       |             |            |           |       |         |           |          |            |             |
| **BuilderFull**       | **Skender.GetAdl** | **10000** |   **658.47 Î¼s** |  **12.737 Î¼s** |  **7.579 Î¼s** |  **0.64** |    **0.01** |  **101.5625** |  **50.7813** |  **862.17 KB** |        **0.49** |
| BuilderLatestOnly | Skender.GetAdl | 10000 |   610.15 Î¼s |  11.251 Î¼s |  7.442 Î¼s |  0.59 |    0.01 |   46.8750 |  15.6250 |  392.32 KB |        0.22 |
| NativeSingle      | Skender.GetAdl | 10000 | 1,028.78 Î¼s |  20.856 Î¼s | 10.908 Î¼s |  1.00 |    0.01 |  210.9375 | 140.6250 | 1758.31 KB |        1.00 |
| NativeParallel    | Skender.GetAdl | 10000 | 3,152.97 Î¼s | 138.272 Î¼s | 91.458 Î¼s |  3.07 |    0.09 | 1000.0000 | 968.7500 | 7992.91 KB |        4.55 |
