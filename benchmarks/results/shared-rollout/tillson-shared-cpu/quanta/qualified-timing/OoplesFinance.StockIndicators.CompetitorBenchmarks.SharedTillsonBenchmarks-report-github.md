```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation    | Count | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1     | Gen2   | Allocated  | Alloc Ratio |
|------------------ |------------- |------ |-------------:|-------------:|-------------:|------:|--------:|---------:|---------:|-------:|-----------:|------------:|
| **BuilderFull**       | **QuanTAlib.T3** | **1000**  |  **2,143.82 μs** |   **577.416 μs** |   **381.925 μs** | **68.78** |   **11.71** |        **-** |        **-** |      **-** |  **767.16 KB** |       **15.76** |
| BuilderLatestOnly | QuanTAlib.T3 | 1000  |  1,819.07 μs |    35.787 μs |    21.296 μs | 58.36 |    0.81 |  78.1250 |        - |      - |  719.61 KB |       14.78 |
| NativeSingle      | QuanTAlib.T3 | 1000  |     31.17 μs |     0.532 μs |     0.278 μs |  1.00 |    0.01 |   5.8594 |   0.2441 | 0.2441 |   48.69 KB |        1.00 |
| NativeParallel    | QuanTAlib.T3 | 1000  |     79.49 μs |     5.309 μs |     3.160 μs |  2.55 |    0.10 |  24.9023 |   2.4414 | 2.4414 |  198.13 KB |        4.07 |
|                   |              |       |              |              |              |       |         |          |          |        |            |             |
| **BuilderFull**       | **QuanTAlib.T3** | **10000** | **19,534.49 μs** | **2,135.132 μs** | **1,412.257 μs** | **69.53** |    **4.91** | **833.3333** | **666.6667** |      **-** | **7659.78 KB** |       **16.28** |
| BuilderLatestOnly | QuanTAlib.T3 | 10000 | 18,235.33 μs |   244.571 μs |   127.916 μs | 64.90 |    1.05 | 833.3333 | 166.6667 |      - | 7190.07 KB |       15.28 |
| NativeSingle      | QuanTAlib.T3 | 10000 |    281.02 μs |     8.510 μs |     4.451 μs |  1.00 |    0.02 |  56.6406 |   9.7656 |      - |  470.58 KB |        1.00 |
| NativeParallel    | QuanTAlib.T3 | 10000 |    482.62 μs |    32.978 μs |    21.813 μs |  1.72 |    0.08 | 222.6563 |  42.9688 |      - | 1850.48 KB |        3.93 |
