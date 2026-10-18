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
| **BuilderFull**       | **Skender.GetUltimate**  | **1000**  |    **37.084 μs** |  **4.1937 μs** |  **2.4956 μs** |  **0.35** |    **0.02** |   **7.8125** |   **1.2207** |   **65.42 KB** |        **0.43** |
| BuilderLatestOnly | Skender.GetUltimate  | 1000  |    33.094 μs |  3.3495 μs |  2.2155 μs |  0.31 |    0.02 |   1.9531 |        - |   17.81 KB |        0.12 |
| NativeSingle      | Skender.GetUltimate  | 1000  |   107.270 μs |  3.7659 μs |  2.2410 μs |  1.00 |    0.03 |  18.5547 |   3.9063 |  152.83 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 1000  |    42.895 μs |  3.6948 μs |  2.4439 μs |  0.40 |    0.02 |  23.4375 |   5.8594 |  186.31 KB |        1.22 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetUltimate**  | **10000** |   **275.912 μs** |  **6.7208 μs** |  **3.9994 μs** |  **0.22** |    **0.01** |  **76.1719** |  **37.1094** |  **628.31 KB** |        **0.41** |
| BuilderLatestOnly | Skender.GetUltimate  | 10000 |   241.622 μs |  2.2242 μs |  1.3236 μs |  0.19 |    0.01 |  17.5781 |   3.9063 |  158.46 KB |        0.10 |
| NativeSingle      | Skender.GetUltimate  | 10000 | 1,252.488 μs | 69.1201 μs | 41.1322 μs |  1.00 |    0.04 | 179.6875 | 109.3750 | 1523.99 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 10000 |   488.588 μs | 31.7747 μs | 21.0170 μs |  0.39 |    0.02 | 203.1250 | 136.7188 | 1557.44 KB |        1.02 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **1000**  |    **35.690 μs** |  **2.1527 μs** |  **1.4239 μs** |  **1.73** |    **0.21** |   **7.8125** |   **1.2207** |   **65.42 KB** |        **8.54** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 1000  |    32.280 μs |  1.1017 μs |  0.6556 μs |  1.57 |    0.18 |   1.9531 |        - |   17.81 KB |        2.33 |
| NativeSingle      | TaLib(...)ltOsc [22] | 1000  |    20.892 μs |  3.7446 μs |  2.4768 μs |  1.01 |    0.16 |   0.8545 |        - |    7.66 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 1000  |     7.595 μs |  0.3105 μs |  0.2054 μs |  0.37 |    0.04 |   0.9766 |        - |    8.09 KB |        1.06 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **10000** |   **280.762 μs** |  **5.0599 μs** |  **3.3468 μs** |  **1.43** |    **0.18** |  **76.1719** |  **37.1094** |  **628.31 KB** |        **8.06** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 10000 |   248.168 μs |  5.1620 μs |  3.4143 μs |  1.26 |    0.16 |  17.5781 |   3.9063 |  158.46 KB |        2.03 |
| NativeSingle      | TaLib(...)ltOsc [22] | 10000 |   199.401 μs | 40.2031 μs | 26.5918 μs |  1.02 |    0.18 |   8.7891 |   0.9766 |   77.98 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 10000 |    44.767 μs |  0.6776 μs |  0.4032 μs |  0.23 |    0.03 |   9.5215 |   1.7090 |    78.4 KB |        1.01 |
