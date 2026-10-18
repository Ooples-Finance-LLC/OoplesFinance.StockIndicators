```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=10  LaunchCount=1  UnrollFactor=1  
WarmupCount=10  

```
| Method            | Operation           | Count | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |-------------------- |------ |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetMfi**      | **1000**  |    **34.927 μs** |  **4.3140 μs** |  **2.8534 μs** |  **0.47** |    **0.05** |   **7.8125** |        **-** |   **65.38 KB** |        **0.42** |
| BuilderLatestOnly | Skender.GetMfi      | 1000  |    26.575 μs |  2.3608 μs |  1.4049 μs |  0.36 |    0.03 |   1.9531 |        - |   17.77 KB |        0.11 |
| NativeSingle      | Skender.GetMfi      | 1000  |    74.864 μs |  7.3127 μs |  4.8369 μs |  1.00 |    0.09 |  19.0430 |   3.9063 |  156.75 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 1000  |    30.614 μs |  2.1010 μs |  1.3897 μs |  0.41 |    0.03 |  22.2168 |   6.1035 |  175.88 KB |        1.12 |
|                   |                     |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetMfi**      | **10000** |   **240.487 μs** |  **4.7494 μs** |  **2.4840 μs** |  **0.24** |    **0.01** |  **76.1719** |  **37.1094** |  **628.23 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetMfi      | 10000 |   230.600 μs |  9.6458 μs |  6.3801 μs |  0.23 |    0.01 |  17.5781 |   3.9063 |  158.42 KB |        0.10 |
| NativeSingle      | Skender.GetMfi      | 10000 | 1,002.539 μs | 53.9145 μs | 32.0837 μs |  1.00 |    0.04 | 187.5000 | 140.6250 | 1563.06 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 10000 |   396.620 μs | 20.4347 μs | 13.5163 μs |  0.40 |    0.02 | 203.1250 | 144.5313 | 1582.13 KB |        1.01 |
|                   |                     |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **1000**  |    **37.639 μs** |  **2.3319 μs** |  **1.5424 μs** |  **4.51** |    **0.21** |   **7.8125** |   **1.2207** |   **65.34 KB** |        **8.16** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 1000  |    29.052 μs |  0.7985 μs |  0.4752 μs |  3.48 |    0.11 |   1.9531 |        - |   17.77 KB |        2.22 |
| NativeSingle      | TaLib.Functions.Mfi | 1000  |     8.360 μs |  0.4060 μs |  0.2416 μs |  1.00 |    0.04 |   0.9766 |        - |    8.01 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 1000  |     6.200 μs |  0.9304 μs |  0.6154 μs |  0.74 |    0.07 |   1.2817 |        - |   10.13 KB |        1.27 |
|                   |                     |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **10000** |   **252.974 μs** |  **4.3043 μs** |  **2.5614 μs** |  **3.37** |    **0.06** |  **76.1719** |  **37.1094** |  **628.23 KB** |        **8.02** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 10000 |   219.270 μs |  4.1546 μs |  2.4724 μs |  2.92 |    0.05 |  17.5781 |   3.9063 |  158.42 KB |        2.02 |
| NativeSingle      | TaLib.Functions.Mfi | 10000 |    75.137 μs |  2.0636 μs |  1.0793 μs |  1.00 |    0.02 |   9.2773 |   1.4648 |   78.32 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 10000 |    22.568 μs |  0.7761 μs |  0.5133 μs |  0.30 |    0.01 |   9.8877 |   1.9531 |   80.45 KB |        1.03 |
