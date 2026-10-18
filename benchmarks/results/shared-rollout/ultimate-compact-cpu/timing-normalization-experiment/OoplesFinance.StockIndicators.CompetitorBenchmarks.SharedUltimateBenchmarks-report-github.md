```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=10  LaunchCount=1  UnrollFactor=1  
WarmupCount=10  

```
| Method            | Operation            | Count | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------------- |------ |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetUltimate**  | **1000**  |    **61.953 μs** |  **1.8899 μs** |  **1.2501 μs** |  **0.51** |    **0.02** |   **7.8125** |   **0.9766** |   **65.42 KB** |        **0.43** |
| BuilderLatestOnly | Skender.GetUltimate  | 1000  |    53.107 μs |  1.8149 μs |  1.2005 μs |  0.44 |    0.01 |   1.9531 |        - |   17.81 KB |        0.12 |
| NativeSingle      | Skender.GetUltimate  | 1000  |   120.963 μs |  5.5576 μs |  3.3072 μs |  1.00 |    0.04 |  18.5547 |   3.9063 |  152.83 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 1000  |    48.366 μs |  6.0231 μs |  3.9839 μs |  0.40 |    0.03 |  23.4375 |   5.8594 |  186.31 KB |        1.22 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetUltimate**  | **10000** |   **424.644 μs** |  **7.4240 μs** |  **4.9105 μs** |  **0.32** |    **0.01** |  **74.2188** |  **31.2500** |  **628.33 KB** |        **0.41** |
| BuilderLatestOnly | Skender.GetUltimate  | 10000 |   379.652 μs |  3.1205 μs |  1.8570 μs |  0.29 |    0.00 |  17.5781 |   3.9063 |  158.46 KB |        0.10 |
| NativeSingle      | Skender.GetUltimate  | 10000 | 1,331.002 μs | 38.4731 μs | 22.8947 μs |  1.00 |    0.02 | 179.6875 | 109.3750 | 1523.99 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 10000 |   519.609 μs | 35.5506 μs | 18.5937 μs |  0.39 |    0.01 | 203.1250 | 144.5313 | 1557.44 KB |        1.02 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **1000**  |    **61.688 μs** |  **0.7684 μs** |  **0.5083 μs** |  **3.31** |    **0.09** |   **7.8125** |   **0.9766** |   **65.42 KB** |        **8.54** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 1000  |    55.913 μs |  1.5435 μs |  1.0209 μs |  3.00 |    0.09 |   1.9531 |        - |   17.81 KB |        2.33 |
| NativeSingle      | TaLib(...)ltOsc [22] | 1000  |    18.661 μs |  0.7691 μs |  0.5087 μs |  1.00 |    0.04 |   0.8545 |        - |    7.66 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 1000  |     7.523 μs |  0.2759 μs |  0.1825 μs |  0.40 |    0.01 |   0.9766 |        - |    8.09 KB |        1.06 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **10000** |   **441.064 μs** |  **9.1052 μs** |  **6.0225 μs** |  **2.43** |    **0.05** |  **74.2188** |  **31.2500** |  **628.33 KB** |        **8.06** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 10000 |   393.135 μs |  3.8998 μs |  2.5795 μs |  2.17 |    0.03 |  15.6250 |   3.9063 |  158.48 KB |        2.03 |
| NativeSingle      | TaLib(...)ltOsc [22] | 10000 |   181.257 μs |  5.3196 μs |  2.7822 μs |  1.00 |    0.02 |   8.7891 |   0.9766 |   77.98 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 10000 |    47.796 μs |  1.0891 μs |  0.5696 μs |  0.26 |    0.00 |   9.2773 |   1.4648 |    78.4 KB |        1.01 |
