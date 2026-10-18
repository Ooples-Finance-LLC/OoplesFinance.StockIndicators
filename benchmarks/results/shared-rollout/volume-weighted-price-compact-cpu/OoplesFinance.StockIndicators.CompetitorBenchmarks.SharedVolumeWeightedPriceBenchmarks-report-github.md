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
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwap**      | **1000**  |       **245.41 μs** |     **10.222 μs** |      **6.083 μs** |  **3.82** |    **0.11** |        **-** |        **-** |    **64.91 KB** |        **0.47** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwap      | 1000  |       243.84 μs |     10.123 μs |      6.024 μs |  3.80 |    0.11 |   1.9531 |        - |    17.26 KB |        0.13 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwap      | 1000  |        64.24 μs |      1.545 μs |      1.022 μs |  1.00 |    0.02 |  16.6016 |   3.9063 |   137.15 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwap      | 1000  |       174.03 μs |     11.761 μs |      6.999 μs |  2.71 |    0.11 |  83.9844 |  31.2500 |   627.46 KB |        4.57 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwap**      | **10000** |     **2,474.00 μs** |     **67.287 μs** |     **40.041 μs** |  **3.16** |    **0.08** |  **62.5000** |  **15.6250** |   **627.87 KB** |        **0.46** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwap      | 10000 |     2,337.67 μs |     34.912 μs |     20.776 μs |  2.99 |    0.07 |        - |        - |   158.16 KB |        0.12 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwap      | 10000 |       783.21 μs |     25.456 μs |     16.838 μs |  1.00 |    0.03 | 164.0625 | 156.2500 |  1367.68 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwap      | 10000 |     2,553.27 μs |    166.938 μs |     99.342 μs |  3.26 |    0.14 | 781.2500 | 703.1250 |  6234.99 KB |        4.56 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwma**      | **1000**  |       **336.99 μs** |      **8.598 μs** |      **5.687 μs** |  **3.71** |    **0.10** |   **7.8125** |        **-** |    **67.79 KB** |        **0.49** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwma      | 1000  |       328.45 μs |      7.225 μs |      4.779 μs |  3.62 |    0.10 |   1.9531 |        - |    20.18 KB |        0.15 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwma      | 1000  |        90.83 μs |      3.225 μs |      2.133 μs |  1.00 |    0.03 |  16.6016 |   3.9063 |   137.15 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwma      | 1000  |        42.82 μs |      5.802 μs |      3.837 μs |  0.47 |    0.04 |  21.4844 |   5.1270 |   167.17 KB |        1.22 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Skender.GetVwma**      | **10000** |     **3,350.96 μs** |    **147.418 μs** |     **87.726 μs** |  **3.18** |    **0.10** |  **62.5000** |  **31.2500** |   **630.92 KB** |        **0.46** |
| BuilderLatestOnly | Adaptive   | Default         | Skender.GetVwma      | 10000 |     3,255.74 μs |     38.120 μs |     22.685 μs |  3.09 |    0.06 |        - |        - |   161.06 KB |        0.12 |
| NativeSingle      | Adaptive   | Default         | Skender.GetVwma      | 10000 |     1,054.70 μs |     36.949 μs |     21.988 μs |  1.00 |    0.03 | 164.0625 | 156.2500 |  1367.68 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Skender.GetVwma      | 10000 |       436.25 μs |     28.903 μs |     15.117 μs |  0.41 |    0.02 | 195.3125 | 148.4375 |  1468.01 KB |        1.07 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Trady(...)Price [42]** | **1000**  |       **245.13 μs** |      **5.198 μs** |      **3.093 μs** | **0.004** |    **0.00** |   **7.8125** |        **-** |    **64.88 KB** |        **0.11** |
| BuilderLatestOnly | Adaptive   | Default         | Trady(...)Price [42] | 1000  |       243.84 μs |      9.244 μs |      6.115 μs | 0.004 |    0.00 |   1.9531 |        - |    17.27 KB |        0.03 |
| NativeSingle      | Adaptive   | Default         | Trady(...)Price [42] | 1000  |    61,388.32 μs |    574.523 μs |    380.011 μs | 1.000 |    0.01 |        - |        - |   604.42 KB |        1.00 |
| NativeParallel    | Adaptive   | Default         | Trady(...)Price [42] | 1000  |    16,426.53 μs |    345.011 μs |    228.203 μs | 0.268 |    0.00 |        - |        - |  1034.03 KB |        1.71 |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| **BuilderFull**       | **Adaptive**   | **Default**         | **Trady(...)Price [42]** | **10000** |     **2,432.23 μs** |     **37.616 μs** |     **24.881 μs** |     **?** |       **?** |  **62.5000** |  **15.6250** |   **627.87 KB** |           **?** |
| BuilderLatestOnly | Adaptive   | Default         | Trady(...)Price [42] | 10000 |     2,407.46 μs |     28.733 μs |     17.099 μs |     ? |       ? |  15.6250 |        - |   158.02 KB |           ? |
|                   |            |                 |                      |       |                 |               |               |       |         |          |          |             |             |
| NativeSingle      | SlowNative | 1               | Trady(...)Price [42] | 10000 | 6,608,878.14 μs | 53,118.759 μs | 35,134.770 μs |  1.00 |    0.01 |        - |        - |  5906.97 KB |        1.00 |
| NativeParallel    | SlowNative | 1               | Trady(...)Price [42] | 10000 | 1,755,212.28 μs | 63,977.165 μs | 42,316.933 μs |  0.27 |    0.01 |        - |        - | 10326.75 KB |        1.75 |
