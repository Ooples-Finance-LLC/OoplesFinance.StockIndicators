```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation     | Count | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Gen2   | Allocated | Alloc Ratio |
|------------------ |-------------- |------ |----------:|----------:|----------:|------:|--------:|---------:|--------:|-------:|----------:|------------:|
| **BuilderFull**       | **QuanTAlib.Atr** | **1000**  |  **14.52 μs** |  **0.680 μs** |  **0.405 μs** |  **0.32** |    **0.02** |   **6.8359** |  **0.9766** |      **-** |  **56.74 KB** |        **0.65** |
| BuilderLatestOnly | QuanTAlib.Atr | 1000  |  10.25 μs |  0.544 μs |  0.324 μs |  0.23 |    0.01 |   0.9766 |       - |      - |   8.85 KB |        0.10 |
| GpuFull           | QuanTAlib.Atr | 1000  | 240.47 μs |  9.002 μs |  4.708 μs |  5.29 |    0.28 |   3.9063 |       - |      - |  56.69 KB |        0.65 |
| GpuLatestOnly     | QuanTAlib.Atr | 1000  | 235.40 μs |  5.760 μs |  3.013 μs |  5.18 |    0.26 |        - |       - |      - |   8.79 KB |        0.10 |
| NativeSingle      | QuanTAlib.Atr | 1000  |  45.54 μs |  3.789 μs |  2.255 μs |  1.00 |    0.07 |  10.4980 |  0.2441 |      - |  86.67 KB |        1.00 |
| NativeParallel    | QuanTAlib.Atr | 1000  |  19.13 μs |  1.315 μs |  0.688 μs |  0.42 |    0.03 |  11.7188 |  0.4883 | 0.1221 |  92.35 KB |        1.07 |
|                   |               |       |           |           |           |       |         |          |         |        |           |             |
| **BuilderFull**       | **QuanTAlib.Atr** | **10000** |  **73.87 μs** |  **5.560 μs** |  **3.308 μs** |  **0.17** |    **0.01** |  **67.3828** | **22.4609** |      **-** |  **550.3 KB** |        **0.64** |
| BuilderLatestOnly | QuanTAlib.Atr | 10000 |  46.40 μs |  3.052 μs |  1.816 μs |  0.10 |    0.00 |   9.7656 |  1.9531 |      - |  80.17 KB |        0.09 |
| GpuFull           | QuanTAlib.Atr | 10000 | 572.09 μs |  6.210 μs |  4.107 μs |  1.28 |    0.02 |  66.4063 | 58.5938 |      - | 549.25 KB |        0.64 |
| GpuLatestOnly     | QuanTAlib.Atr | 10000 | 546.09 μs | 28.159 μs | 18.625 μs |  1.22 |    0.04 |   7.8125 |       - |      - |  79.13 KB |        0.09 |
| NativeSingle      | QuanTAlib.Atr | 10000 | 446.71 μs | 13.241 μs |  7.880 μs |  1.00 |    0.02 | 101.5625 | 19.5313 |      - | 860.14 KB |        1.00 |
| NativeParallel    | QuanTAlib.Atr | 10000 | 122.85 μs |  2.653 μs |  1.387 μs |  0.28 |    0.01 | 106.4453 | 20.5078 |      - | 865.79 KB |        1.01 |
