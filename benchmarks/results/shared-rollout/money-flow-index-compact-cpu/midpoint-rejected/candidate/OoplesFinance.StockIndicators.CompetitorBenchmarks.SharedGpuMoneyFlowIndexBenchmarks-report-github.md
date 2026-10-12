```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation           | Count | Mean       | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |-------------------- |------ |-----------:|-----------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetMfi**      | **1000**  |  **31.859 μs** |  **2.0471 μs** |  **1.2182 μs** |  **0.36** |    **0.04** |   **7.8125** |        **-** |   **65.38 KB** |        **0.42** |
| BuilderLatestOnly | Skender.GetMfi      | 1000  |  28.541 μs |  1.7100 μs |  1.1310 μs |  0.32 |    0.04 |   1.9531 |        - |   17.77 KB |        0.11 |
| GpuFull           | Skender.GetMfi      | 1000  | 406.527 μs | 11.3723 μs |  6.7675 μs |  4.61 |    0.48 |   7.8125 |        - |   64.59 KB |        0.41 |
| GpuLatestOnly     | Skender.GetMfi      | 1000  | 394.851 μs | 10.2296 μs |  6.7663 μs |  4.48 |    0.47 |        - |        - |   16.97 KB |        0.11 |
| NativeSingle      | Skender.GetMfi      | 1000  |  89.206 μs | 15.8165 μs | 10.4617 μs |  1.01 |    0.15 |  19.0430 |   3.9063 |  156.75 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 1000  |  27.450 μs |  0.8649 μs |  0.5147 μs |  0.31 |    0.03 |  22.2168 |   6.5918 |  175.88 KB |        1.12 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetMfi**      | **10000** | **260.530 μs** |  **5.6567 μs** |  **3.7416 μs** |  **0.29** |    **0.01** |  **76.1719** |  **37.1094** |  **628.27 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetMfi      | 10000 | 240.247 μs |  5.3162 μs |  3.5163 μs |  0.27 |    0.00 |  17.5781 |   3.9063 |  158.42 KB |        0.10 |
| GpuFull           | Skender.GetMfi      | 10000 | 852.246 μs | 21.6022 μs | 14.2885 μs |  0.95 |    0.02 |  70.3125 |  23.4375 |  627.49 KB |        0.40 |
| GpuLatestOnly     | Skender.GetMfi      | 10000 | 791.893 μs | 32.9951 μs | 21.8242 μs |  0.88 |    0.03 |  15.6250 |        - |  157.63 KB |        0.10 |
| NativeSingle      | Skender.GetMfi      | 10000 | 898.501 μs | 19.6690 μs | 10.2873 μs |  1.00 |    0.02 | 187.5000 | 140.6250 | 1563.06 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 10000 | 353.325 μs | 21.5581 μs | 14.2593 μs |  0.39 |    0.02 | 205.0781 | 142.5781 | 1582.12 KB |        1.01 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **1000**  |  **34.510 μs** |  **4.9003 μs** |  **3.2412 μs** |  **4.57** |    **0.42** |   **7.8125** |   **1.2207** |   **65.38 KB** |        **8.16** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 1000  |  27.356 μs |  1.1197 μs |  0.7406 μs |  3.63 |    0.12 |   1.9531 |        - |   17.77 KB |        2.22 |
| GpuFull           | TaLib.Functions.Mfi | 1000  | 433.698 μs | 14.4383 μs |  9.5500 μs | 57.47 |    1.62 |   7.8125 |        - |   64.59 KB |        8.06 |
| GpuLatestOnly     | TaLib.Functions.Mfi | 1000  | 450.148 μs | 28.8263 μs | 19.0668 μs | 59.65 |    2.66 |        - |        - |   16.97 KB |        2.12 |
| NativeSingle      | TaLib.Functions.Mfi | 1000  |   7.549 μs |  0.2237 μs |  0.1480 μs |  1.00 |    0.03 |   0.9766 |        - |    8.01 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 1000  |   5.113 μs |  0.4186 μs |  0.2769 μs |  0.68 |    0.04 |   1.2512 |   0.0305 |   10.13 KB |        1.27 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **10000** | **259.514 μs** |  **7.1852 μs** |  **4.7526 μs** |  **3.50** |    **0.17** |  **76.1719** |  **37.1094** |  **628.27 KB** |        **8.02** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 10000 | 233.971 μs |  2.5140 μs |  1.6629 μs |  3.16 |    0.14 |  17.5781 |   3.9063 |  158.42 KB |        2.02 |
| GpuFull           | TaLib.Functions.Mfi | 10000 | 856.835 μs | 42.4112 μs | 28.0524 μs | 11.55 |    0.63 |  70.3125 |  23.4375 |  627.48 KB |        8.01 |
| GpuLatestOnly     | TaLib.Functions.Mfi | 10000 | 791.183 μs | 22.0420 μs | 14.5794 μs | 10.67 |    0.51 |  15.6250 |        - |  157.64 KB |        2.01 |
| NativeSingle      | TaLib.Functions.Mfi | 10000 |  74.315 μs |  7.3931 μs |  3.8667 μs |  1.00 |    0.07 |   9.2773 |   1.4648 |   78.32 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 10000 |  19.766 μs |  0.7709 μs |  0.5099 μs |  0.27 |    0.01 |   9.8877 |   1.9531 |   80.45 KB |        1.03 |
