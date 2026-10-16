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
| **BuilderFull**       | **Skender.GetMfi**      | **1000**  |  **33.384 μs** |  **1.8672 μs** |  **1.1112 μs** |  **0.42** |    **0.02** |   **7.8125** |   **0.9766** |   **65.38 KB** |        **0.42** |
| BuilderLatestOnly | Skender.GetMfi      | 1000  |  27.100 μs |  2.4373 μs |  1.6121 μs |  0.34 |    0.02 |   1.9531 |        - |   17.77 KB |        0.11 |
| NativeSingle      | Skender.GetMfi      | 1000  |  80.371 μs |  2.7336 μs |  1.6267 μs |  1.00 |    0.03 |  19.0430 |   3.9063 |  156.75 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 1000  |  33.197 μs |  2.2496 μs |  1.3387 μs |  0.41 |    0.02 |  22.2168 |   6.8359 |  175.88 KB |        1.12 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetMfi**      | **10000** | **230.209 μs** |  **3.8072 μs** |  **2.5182 μs** |  **0.26** |    **0.01** |  **76.1719** |  **37.1094** |  **628.27 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetMfi      | 10000 | 196.305 μs |  4.8238 μs |  2.8706 μs |  0.22 |    0.01 |  17.5781 |   3.9063 |  158.42 KB |        0.10 |
| NativeSingle      | Skender.GetMfi      | 10000 | 880.189 μs | 71.0637 μs | 42.2889 μs |  1.00 |    0.06 | 187.5000 | 140.6250 | 1563.07 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 10000 | 351.866 μs | 37.1458 μs | 22.1049 μs |  0.40 |    0.03 | 205.0781 | 144.5313 | 1582.12 KB |        1.01 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **1000**  |  **30.240 μs** |  **2.4287 μs** |  **1.6064 μs** |  **3.80** |    **0.36** |   **7.8125** |   **1.2207** |   **65.38 KB** |        **8.16** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 1000  |  25.898 μs |  1.8362 μs |  1.2145 μs |  3.25 |    0.30 |   1.9531 |        - |   17.77 KB |        2.22 |
| NativeSingle      | TaLib.Functions.Mfi | 1000  |   8.016 μs |  1.1013 μs |  0.7284 μs |  1.01 |    0.12 |   0.9766 |        - |    8.01 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 1000  |   4.987 μs |  0.3746 μs |  0.2478 μs |  0.63 |    0.06 |   1.2512 |   0.0305 |   10.13 KB |        1.27 |
|                   |                     |       |            |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **10000** | **233.335 μs** | **10.3511 μs** |  **6.8466 μs** |  **3.15** |    **0.12** |  **76.1719** |  **37.1094** |  **628.27 KB** |        **8.02** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 10000 | 203.298 μs | 10.1672 μs |  6.7250 μs |  2.75 |    0.11 |  17.5781 |   3.9063 |  158.42 KB |        2.02 |
| NativeSingle      | TaLib.Functions.Mfi | 10000 |  74.036 μs |  2.8589 μs |  1.8910 μs |  1.00 |    0.03 |   9.2773 |   1.4648 |   78.32 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 10000 |  20.541 μs |  1.8632 μs |  1.2324 μs |  0.28 |    0.02 |   9.8877 |   1.9531 |   80.45 KB |        1.03 |
