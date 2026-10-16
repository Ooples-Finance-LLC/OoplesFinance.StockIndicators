```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation            | Count | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------------- |------ |------------:|-----------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skend(...)Index [21]** | **1000**  |   **143.77 Î¼s** |   **5.435 Î¼s** |   **3.234 Î¼s** |  **2.47** |    **0.08** |   **7.8125** |        **-** |   **64.81 KB** |        **0.47** |
| BuilderLatestOnly | Skend(...)Index [21] | 1000  |   146.50 Î¼s |  19.783 Î¼s |  13.085 Î¼s |  2.52 |    0.22 |   1.9531 |        - |    17.2 KB |        0.13 |
| NativeSingle      | Skend(...)Index [21] | 1000  |    58.23 Î¼s |   2.433 Î¼s |   1.609 Î¼s |  1.00 |    0.04 |  16.6016 |   3.4180 |  137.15 KB |        1.00 |
| NativeParallel    | Skend(...)Index [21] | 1000  |   151.50 Î¼s |   4.678 Î¼s |   2.784 Î¼s |  2.60 |    0.08 |  83.0078 |  31.2500 |  627.43 KB |        4.57 |
|                   |                      |       |             |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skend(...)Index [21]** | **10000** | **1,453.90 Î¼s** |  **63.570 Î¼s** |  **42.048 Î¼s** |  **2.08** |    **0.06** |  **70.3125** |  **23.4375** |  **627.74 KB** |        **0.46** |
| BuilderLatestOnly | Skend(...)Index [21] | 10000 | 1,368.24 Î¼s |  15.400 Î¼s |  10.186 Î¼s |  1.96 |    0.02 |  15.6250 |        - |  157.89 KB |        0.12 |
| NativeSingle      | Skend(...)Index [21] | 10000 |   699.21 Î¼s |   9.059 Î¼s |   5.391 Î¼s |  1.00 |    0.01 | 164.0625 | 160.1563 | 1367.65 KB |        1.00 |
| NativeParallel    | Skend(...)Index [21] | 10000 | 2,224.61 Î¼s | 152.340 Î¼s | 100.764 Î¼s |  3.18 |    0.14 | 781.2500 | 687.5000 | 6234.92 KB |        4.56 |
