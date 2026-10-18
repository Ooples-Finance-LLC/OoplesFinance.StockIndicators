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
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwap**      | **1000**  |        **89.28 μs** |      **6.503 μs** |      **4.301 μs** |  **1.50** |    **0.10** |   **7.8125** |        **-** |    **68.65 KB** |        **0.50** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwap      | 1000  |        90.32 μs |      5.953 μs |      3.938 μs |  1.52 |    0.10 |   1.9531 |        - |    21.04 KB |        0.15 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwap      | 1000  |        59.66 μs |      5.334 μs |      3.174 μs |  1.00 |    0.07 |  16.6016 |   3.9063 |   137.15 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwap      | 1000  |       164.59 μs |     24.966 μs |     16.513 μs |  2.77 |    0.30 |  83.0078 |  32.2266 |   627.15 KB |        4.57 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwap**      | **10000** |       **767.11 μs** |     **16.021 μs** |     **10.597 μs** |  **1.00** |    **0.07** |  **74.2188** |  **70.3125** |   **631.55 KB** |        **0.46** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwap      | 10000 |       720.33 μs |     11.408 μs |      6.789 μs |  0.94 |    0.06 |  19.5313 |   3.9063 |    161.7 KB |        0.12 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwap      | 10000 |       770.42 μs |     84.063 μs |     55.603 μs |  1.00 |    0.10 | 164.0625 | 160.1563 |  1367.65 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwap      | 10000 |     2,176.41 μs |     79.419 μs |     47.261 μs |  2.84 |    0.20 | 781.2500 | 703.1250 |  6234.67 KB |        4.56 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwma**      | **1000**  |        **87.51 μs** |      **3.083 μs** |      **2.039 μs** |  **1.07** |    **0.04** |  **10.7422** |   **0.9766** |    **90.22 KB** |        **0.66** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwma      | 1000  |        83.67 μs |      1.352 μs |      0.894 μs |  1.03 |    0.03 |   5.3711 |        - |     42.6 KB |        0.31 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwma      | 1000  |        81.55 μs |      3.519 μs |      2.328 μs |  1.00 |    0.04 |  16.6016 |   3.9063 |   137.15 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwma      | 1000  |        34.61 μs |      1.006 μs |      0.599 μs |  0.42 |    0.01 |  21.4844 |   5.3711 |   167.17 KB |        1.22 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwma**      | **10000** |       **726.74 μs** |     **18.973 μs** |     **12.549 μs** |  **0.67** |    **0.05** |  **78.1250** |  **39.0625** |   **653.12 KB** |        **0.48** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwma      | 10000 |       657.76 μs |      4.734 μs |      3.131 μs |  0.60 |    0.04 |  19.5313 |   3.9063 |   183.27 KB |        0.13 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwma      | 10000 |     1,097.24 μs |    126.116 μs |     83.418 μs |  1.00 |    0.10 | 164.0625 | 156.2500 |  1367.68 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwma      | 10000 |       384.98 μs |     18.363 μs |     12.146 μs |  0.35 |    0.03 | 195.3125 | 136.7188 |  1468.01 KB |        1.07 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Trady(...)Price [42]** | **1000**  |        **87.89 μs** |      **7.080 μs** |      **4.683 μs** | **0.001** |    **0.00** |   **8.3008** |   **0.9766** |    **68.65 KB** |        **0.11** |
| BuilderLatestOnly | Adaptive   | Default         | Trady(...)Price [42] | 1000  |        80.04 μs |      1.762 μs |      1.048 μs | 0.001 |    0.00 |   2.4414 |        - |    21.04 KB |        0.03 |
| NativeSingle      | Adaptive   | Default         | Trady(...)Price [42] | 1000  |    61,516.56 μs |  2,235.391 μs |  1,330.245 μs | 1.000 |    0.03 |        - |        - |   604.42 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Trady(...)Price [42] | 1000  |    16,564.56 μs |    592.891 μs |    392.161 μs | 0.269 |    0.01 |        - |        - |   1034.4 KB |        1.71 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Trady(...)Price [42]** | **10000** |       **780.82 μs** |     **24.362 μs** |     **16.114 μs** |     **?** |       **?** |  **74.2188** |  **70.3125** |   **631.55 KB** |           **?** |
| BuilderLatestOnly | Adaptive   | Default         | Trady(...)Price [42] | 10000 |       735.06 μs |     34.770 μs |     22.998 μs |     ? |       ? |  19.5313 |   3.9063 |    161.7 KB |           ? |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| NativeSingle      | SlowNative | 1               | Trady(...)Price [42] | 10000 | 6,761,813.75 μs | 93,722.323 μs | 61,991.514 μs |  1.00 |    0.01 |        - |        - |  5906.97 KB |        1.00 |
| NativeParallel    | SlowNative | 1               | Trady(...)Price [42] | 10000 | 1,809,146.32 μs | 61,068.214 μs | 40,392.842 μs |  0.27 |    0.01 |        - |        - | 10326.38 KB |        1.75 |
