```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  IterationCount=10
LaunchCount=1  UnrollFactor=1  WarmupCount=10

```
| Method            | Job        | InvocationCount | Operation            | Count | Mean            | Error          | StdDev        | Ratio | RatioSD | Gen0     | Gen1     | Allocated   | Alloc Ratio |
|------------------ |----------- |---------------- |--------------------- |------ |----------------:|---------------:|--------------:|------:|--------:|---------:|---------:|------------:|------------:|
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwap**      | **1000**  |        **40.56 μs** |       **3.513 μs** |      **1.837 μs** |  **0.68** |    **0.03** |   **8.3008** |   **0.9766** |    **68.65 KB** |        **0.50** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwap      | 1000  |        37.18 μs |       4.161 μs |      2.752 μs |  0.62 |    0.05 |   2.4414 |        - |    21.04 KB |        0.15 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwap      | 1000  |        60.12 μs |       2.436 μs |      1.611 μs |  1.00 |    0.04 |  16.6016 |   3.9063 |   137.15 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwap      | 1000  |       148.05 μs |       2.601 μs |      1.548 μs |  2.46 |    0.07 |  83.0078 |  31.2500 |   627.15 KB |        4.57 |
|                   |            |                 |                      |       |                 |                |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwap**      | **10000** |       **331.63 μs** |      **18.323 μs** |     **12.120 μs** |  **0.45** |    **0.02** |  **76.1719** |  **74.2188** |   **631.54 KB** |        **0.46** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwap      | 10000 |       272.87 μs |      14.557 μs |      7.614 μs |  0.37 |    0.01 |  19.5313 |   3.9063 |   161.69 KB |        0.12 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwap      | 10000 |       737.63 μs |      24.145 μs |     15.970 μs |  1.00 |    0.03 | 164.0625 | 160.1563 |  1367.65 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwap      | 10000 |     2,201.62 μs |      77.448 μs |     51.227 μs |  2.99 |    0.09 | 781.2500 | 703.1250 |  6234.64 KB |        4.56 |
|                   |            |                 |                      |       |                 |                |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwma**      | **1000**  |        **40.62 μs** |       **2.688 μs** |      **1.778 μs** |  **0.51** |    **0.02** |  **11.2305** |   **1.7090** |    **90.21 KB** |        **0.66** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwma      | 1000  |        37.37 μs |       5.423 μs |      3.587 μs |  0.47 |    0.04 |   5.3711 |   0.2441 |     42.6 KB |        0.31 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwma      | 1000  |        79.93 μs |       2.044 μs |      1.069 μs |  1.00 |    0.02 |  16.6016 |   3.9063 |   137.15 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwma      | 1000  |        35.37 μs |       3.957 μs |      2.355 μs |  0.44 |    0.03 |  21.7285 |   5.3711 |   167.17 KB |        1.22 |
|                   |            |                 |                      |       |                 |                |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwma**      | **10000** |       **305.61 μs** |       **3.746 μs** |      **2.229 μs** |  **0.30** |    **0.00** |  **78.1250** |  **39.0625** |    **653.1 KB** |        **0.48** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwma      | 10000 |       272.83 μs |       4.136 μs |      2.735 μs |  0.26 |    0.00 |  21.4844 |   3.9063 |   183.25 KB |        0.13 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwma      | 10000 |     1,035.40 μs |      23.081 μs |     12.072 μs |  1.00 |    0.02 | 164.0625 | 156.2500 |  1367.68 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwma      | 10000 |       393.40 μs |      44.434 μs |     29.390 μs |  0.38 |    0.03 | 195.3125 | 148.4375 |  1467.99 KB |        1.07 |
|                   |            |                 |                      |       |                 |                |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Trady(...)Price [42]** | **1000**  |        **41.44 μs** |       **1.235 μs** |      **0.817 μs** | **0.001** |    **0.00** |   **8.3008** |   **1.2207** |    **68.65 KB** |        **0.11** |
| BuilderLatestOnly | Adaptive   | Default         | Trady(...)Price [42] | 1000  |        34.93 μs |       1.633 μs |      0.972 μs | 0.001 |    0.00 |   2.4414 |        - |    21.04 KB |        0.03 |
| NativeSingle      | Adaptive   | Default         | Trady(...)Price [42] | 1000  |    62,755.00 μs |   3,957.597 μs |  2,617.705 μs | 1.002 |    0.06 |        - |        - |   604.42 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Trady(...)Price [42] | 1000  |    15,985.02 μs |     480.671 μs |    317.934 μs | 0.255 |    0.01 |        - |        - |   1034.4 KB |        1.71 |
|                   |            |                 |                      |       |                 |                |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Trady(...)Price [42]** | **10000** |       **322.26 μs** |      **11.483 μs** |      **7.595 μs** |     **?** |       **?** |  **76.1719** |  **74.2188** |   **631.54 KB** |           **?** |
| BuilderLatestOnly | Adaptive   | Default         | Trady(...)Price [42] | 10000 |       326.25 μs |      17.949 μs |     11.872 μs |     ? |       ? |  19.5313 |   3.9063 |   161.69 KB |           ? |
|                   |            |                 |                      |       |                 |                |               |       |         |          |          |             |             |
| NativeSingle      | SlowNative | 1               | Trady(...)Price [42] | 10000 | 6,603,666.57 μs |  57,952.028 μs | 38,331.678 μs |  1.00 |    0.01 |        - |        - |  5905.98 KB |        1.00 |
| NativeParallel    | SlowNative | 1               | Trady(...)Price [42] | 10000 | 1,736,074.51 μs | 105,079.744 μs | 69,503.745 μs |  0.26 |    0.01 |        - |        - | 10325.39 KB |        1.75 |
