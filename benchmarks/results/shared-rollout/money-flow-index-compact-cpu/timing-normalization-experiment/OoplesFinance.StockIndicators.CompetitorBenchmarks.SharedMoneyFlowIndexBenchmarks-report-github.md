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
| **BuilderFull**       | **Skender.GetMfi**      | **1000**  |    **38.029 μs** |  **3.8235 μs** |  **2.5290 μs** |  **0.44** |    **0.03** |   **7.8125** |   **0.9766** |   **65.38 KB** |        **0.42** |
| BuilderLatestOnly | Skender.GetMfi      | 1000  |    30.378 μs |  0.9318 μs |  0.6163 μs |  0.35 |    0.02 |   1.9531 |        - |   17.77 KB |        0.11 |
| NativeSingle      | Skender.GetMfi      | 1000  |    87.464 μs |  5.5607 μs |  3.6781 μs |  1.00 |    0.06 |  19.0430 |   3.9063 |  156.75 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 1000  |    41.416 μs |  5.9580 μs |  3.5455 μs |  0.47 |    0.04 |  22.2168 |   6.1035 |  175.88 KB |        1.12 |
|                   |                     |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetMfi**      | **10000** |   **270.218 μs** |  **8.8131 μs** |  **5.2445 μs** |  **0.25** |    **0.01** |  **76.1719** |  **37.1094** |  **628.27 KB** |        **0.40** |
| BuilderLatestOnly | Skender.GetMfi      | 10000 |   228.775 μs |  5.8429 μs |  3.8647 μs |  0.21 |    0.01 |  17.5781 |   3.9063 |  158.42 KB |        0.10 |
| NativeSingle      | Skender.GetMfi      | 10000 | 1,066.203 μs | 65.2930 μs | 38.8548 μs |  1.00 |    0.05 | 187.5000 | 140.6250 | 1563.07 KB |        1.00 |
| NativeParallel    | Skender.GetMfi      | 10000 |   426.605 μs | 23.6570 μs | 15.6477 μs |  0.40 |    0.02 | 203.1250 | 148.4375 | 1582.13 KB |        1.01 |
|                   |                     |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **1000**  |    **36.423 μs** |  **2.0606 μs** |  **1.3629 μs** |  **4.27** |    **0.18** |   **7.8125** |   **1.2207** |   **65.38 KB** |        **8.16** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 1000  |    31.817 μs |  0.9666 μs |  0.6393 μs |  3.73 |    0.11 |   1.9531 |        - |   17.77 KB |        2.22 |
| NativeSingle      | TaLib.Functions.Mfi | 1000  |     8.534 μs |  0.3151 μs |  0.1875 μs |  1.00 |    0.03 |   0.9766 |        - |    8.01 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 1000  |     6.144 μs |  0.5769 μs |  0.3816 μs |  0.72 |    0.05 |   1.2817 |        - |   10.13 KB |        1.27 |
|                   |                     |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib.Functions.Mfi** | **10000** |   **276.738 μs** | **17.6332 μs** | **11.6633 μs** |  **3.45** |    **0.19** |  **76.1719** |  **37.1094** |  **628.27 KB** |        **8.02** |
| BuilderLatestOnly | TaLib.Functions.Mfi | 10000 |   227.705 μs |  3.2662 μs |  2.1604 μs |  2.84 |    0.11 |  17.5781 |   3.9063 |  158.42 KB |        2.02 |
| NativeSingle      | TaLib.Functions.Mfi | 10000 |    80.356 μs |  5.3272 μs |  3.1701 μs |  1.00 |    0.05 |   9.2773 |   1.4648 |   78.32 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mfi | 10000 |    22.993 μs |  0.5665 μs |  0.3747 μs |  0.29 |    0.01 |   9.8877 |   1.9531 |   80.45 KB |        1.03 |
