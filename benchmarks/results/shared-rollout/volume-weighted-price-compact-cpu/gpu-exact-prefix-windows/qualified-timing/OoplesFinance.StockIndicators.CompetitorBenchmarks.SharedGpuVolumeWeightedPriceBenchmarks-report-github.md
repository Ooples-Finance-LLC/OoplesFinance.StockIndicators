```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  IterationCount=10
LaunchCount=1  UnrollFactor=1  WarmupCount=10

```
| Method            | Job        | InvocationCount | Operation            | Count | Mean            | Error         | StdDev        | Ratio | RatioSD | Gen0     | Gen1     | Allocated   | Alloc Ratio |
|------------------ |----------- |---------------- |--------------------- |------ |----------------:|--------------:|--------------:|------:|--------:|---------:|---------:|------------:|------------:|
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwap**      | **1000**  |        **40.91 μs** |      **4.081 μs** |      **2.699 μs** |  **0.73** |    **0.05** |   **7.8125** |   **0.9766** |    **68.62 KB** |        **0.50** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwap      | 1000  |        35.96 μs |      2.284 μs |      1.510 μs |  0.64 |    0.03 |   2.4414 |        - |    21.04 KB |        0.15 |
| GpuFull           | Adaptive   | Default         | Skender.GetVwap      | 1000  |       595.88 μs |     24.751 μs |     16.371 μs | 10.56 |    0.34 |   7.8125 |        - |    64.66 KB |        0.47 |
| GpuLatestOnly     | Adaptive   | Default         | Skender.GetVwap      | 1000  |       582.85 μs |     19.206 μs |     12.704 μs | 10.33 |    0.29 |        - |        - |    17.08 KB |        0.12 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwap      | 1000  |        56.44 μs |      1.919 μs |      1.142 μs |  1.00 |    0.03 |  16.6016 |   3.9063 |   137.15 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwap      | 1000  |       142.02 μs |      1.659 μs |      0.987 μs |  2.52 |    0.05 |  83.0078 |  31.2500 |   627.15 KB |        4.57 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwap**      | **10000** |       **362.19 μs** |     **11.871 μs** |      **7.852 μs** |  **0.49** |    **0.02** |  **76.1719** |  **74.2188** |    **631.5 KB** |        **0.46** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwap      | 10000 |       282.85 μs |     15.721 μs |     10.399 μs |  0.39 |    0.02 |  19.5313 |   3.9063 |   161.68 KB |        0.12 |
| GpuFull           | Adaptive   | Default         | Skender.GetVwap      | 10000 |     1,204.00 μs |     36.150 μs |     21.512 μs |  1.64 |    0.05 |  70.3125 |  23.4375 |   627.57 KB |        0.46 |
| GpuLatestOnly     | Adaptive   | Default         | Skender.GetVwap      | 10000 |     1,167.16 μs |     27.826 μs |     18.405 μs |  1.59 |    0.05 |  15.6250 |        - |   157.75 KB |        0.12 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwap      | 10000 |       734.27 μs |     31.759 μs |     21.007 μs |  1.00 |    0.04 | 164.0625 | 156.2500 |  1367.68 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwap      | 10000 |     2,654.48 μs |    240.395 μs |    159.006 μs |  3.62 |    0.23 | 781.2500 | 703.1250 |  6234.68 KB |        4.56 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwma**      | **1000**  |        **40.67 μs** |      **1.756 μs** |      **1.045 μs** |  **0.50** |    **0.02** |  **11.2305** |   **1.7090** |    **90.18 KB** |        **0.66** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwma      | 1000  |        34.87 μs |      1.381 μs |      0.913 μs |  0.43 |    0.01 |   5.3711 |   0.2441 |     42.6 KB |        0.31 |
| GpuFull           | Adaptive   | Default         | Skender.GetVwma      | 1000  |       613.94 μs |     22.628 μs |     14.967 μs |  7.59 |    0.25 |   7.8125 |        - |    64.67 KB |        0.47 |
| GpuLatestOnly     | Adaptive   | Default         | Skender.GetVwma      | 1000  |       580.00 μs |     12.645 μs |      8.364 μs |  7.17 |    0.19 |        - |        - |    17.08 KB |        0.12 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwma      | 1000  |        80.96 μs |      2.886 μs |      1.909 μs |  1.00 |    0.03 |  16.6016 |   3.9063 |   137.15 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwma      | 1000  |        37.09 μs |      4.449 μs |      2.943 μs |  0.46 |    0.04 |  21.4844 |   5.3711 |   167.17 KB |        1.22 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwma**      | **10000** |       **312.96 μs** |     **19.064 μs** |     **12.610 μs** |  **0.31** |    **0.02** |  **78.1250** |  **39.0625** |   **653.07 KB** |        **0.48** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwma      | 10000 |       279.29 μs |      5.158 μs |      3.069 μs |  0.27 |    0.02 |  21.4844 |   5.8594 |   183.25 KB |        0.13 |
| GpuFull           | Adaptive   | Default         | Skender.GetVwma      | 10000 |     1,215.73 μs |     42.027 μs |     27.798 μs |  1.19 |    0.08 |  70.3125 |  23.4375 |   627.57 KB |        0.46 |
| GpuLatestOnly     | Adaptive   | Default         | Skender.GetVwma      | 10000 |     1,120.65 μs |     16.775 μs |      8.773 μs |  1.10 |    0.07 |  15.6250 |        - |   157.75 KB |        0.12 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwma      | 10000 |     1,023.07 μs |    127.975 μs |     76.156 μs |  1.00 |    0.10 | 164.0625 | 156.2500 |  1367.68 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwma      | 10000 |       371.92 μs |      4.103 μs |      2.442 μs |  0.37 |    0.02 | 195.3125 | 146.4844 |  1467.99 KB |        1.07 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Trady(...)Price [42]** | **1000**  |        **43.16 μs** |      **3.888 μs** |      **2.572 μs** | **0.001** |    **0.00** |   **8.3008** |   **1.2207** |    **68.62 KB** |        **0.11** |
| BuilderLatestOnly | Adaptive   | Default         | Trady(...)Price [42] | 1000  |        37.85 μs |      2.517 μs |      1.665 μs | 0.001 |    0.00 |   2.4414 |        - |    21.04 KB |        0.03 |
| GpuFull           | Adaptive   | Default         | Trady(...)Price [42] | 1000  |       583.36 μs |     15.769 μs |     10.430 μs | 0.009 |    0.00 |   7.8125 |        - |    64.67 KB |        0.11 |
| GpuLatestOnly     | Adaptive   | Default         | Trady(...)Price [42] | 1000  |       588.95 μs |     12.924 μs |      8.548 μs | 0.010 |    0.00 |        - |        - |    17.08 KB |        0.03 |
| NativeSingle      | Adaptive   | Default         | Trady(...)Price [42] | 1000  |    61,515.63 μs |  2,442.437 μs |  1,615.521 μs | 1.001 |    0.04 |        - |        - |   604.35 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Trady(...)Price [42] | 1000  |    16,420.72 μs |    418.292 μs |    276.674 μs | 0.267 |    0.01 |        - |        - |  1034.41 KB |        1.71 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Trady(...)Price [42]** | **10000** |       **324.35 μs** |     **11.459 μs** |      **6.819 μs** |     **?** |       **?** |  **76.1719** |  **74.2188** |    **631.5 KB** |           **?** |
| BuilderLatestOnly | Adaptive   | Default         | Trady(...)Price [42] | 10000 |       294.87 μs |     26.292 μs |     17.391 μs |     ? |       ? |  19.5313 |   3.9063 |   161.69 KB |           ? |
| GpuFull           | Adaptive   | Default         | Trady(...)Price [42] | 10000 |     1,204.34 μs |     35.815 μs |     23.689 μs |     ? |       ? |  70.3125 |  23.4375 |   627.57 KB |           ? |
| GpuLatestOnly     | Adaptive   | Default         | Trady(...)Price [42] | 10000 |     1,169.08 μs |     28.752 μs |     19.017 μs |     ? |       ? |  15.6250 |        - |   157.75 KB |           ? |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| NativeSingle      | SlowNative | 1               | Trady(...)Price [42] | 10000 | 6,626,117.47 μs | 26,146.832 μs | 15,559.560 μs |  1.00 |    0.00 |        - |        - |  5907.25 KB |        1.00 |
| NativeParallel    | SlowNative | 1               | Trady(...)Price [42] | 10000 | 1,701,128.67 μs | 72,959.848 μs | 48,258.422 μs |  0.26 |    0.01 |        - |        - | 10327.03 KB |        1.75 |
