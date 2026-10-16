```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=10  LaunchCount=1  UnrollFactor=1  
WarmupCount=10  

```
| Method            | Operation            | Count | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------------- |------ |-------------:|------------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetUltimate**  | **1000**  |    **55.308 μs** |   **3.3853 μs** |  **2.0145 μs** |  **0.45** |    **0.03** |   **7.8125** |   **0.9766** |   **65.42 KB** |        **0.43** |
| BuilderLatestOnly | Skender.GetUltimate  | 1000  |    49.532 μs |   2.0129 μs |  1.3314 μs |  0.40 |    0.03 |   1.9531 |        - |   17.81 KB |        0.12 |
| NativeSingle      | Skender.GetUltimate  | 1000  |   124.794 μs |  14.2814 μs |  9.4462 μs |  1.00 |    0.10 |  18.5547 |   3.9063 |  152.83 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 1000  |    43.743 μs |   3.7871 μs |  2.5049 μs |  0.35 |    0.03 |  23.4375 |   5.8594 |  186.31 KB |        1.22 |
|                   |                      |       |              |             |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetUltimate**  | **10000** |   **387.140 μs** |   **5.1823 μs** |  **3.0839 μs** |  **0.29** |    **0.02** |  **76.1719** |  **37.1094** |  **628.31 KB** |        **0.41** |
| BuilderLatestOnly | Skender.GetUltimate  | 10000 |   355.504 μs |   2.9505 μs |  1.9516 μs |  0.27 |    0.02 |  17.5781 |   3.9063 |  158.46 KB |        0.10 |
| NativeSingle      | Skender.GetUltimate  | 10000 | 1,326.653 μs | 163.4674 μs | 97.2768 μs |  1.00 |    0.10 | 179.6875 | 109.3750 | 1523.99 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 10000 |   508.840 μs |  17.5318 μs |  9.1695 μs |  0.39 |    0.03 | 203.1250 | 136.7188 | 1557.44 KB |        1.02 |
|                   |                      |       |              |             |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **1000**  |    **56.116 μs** |   **1.3561 μs** |  **0.8970 μs** |  **3.17** |    **0.08** |   **7.8125** |   **0.9766** |   **65.42 KB** |        **8.54** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 1000  |    50.361 μs |   2.3203 μs |  1.5347 μs |  2.85 |    0.10 |   1.9531 |        - |   17.81 KB |        2.33 |
| NativeSingle      | TaLib(...)ltOsc [22] | 1000  |    17.697 μs |   0.6051 μs |  0.3601 μs |  1.00 |    0.03 |   0.8545 |        - |    7.66 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 1000  |     7.245 μs |   0.3446 μs |  0.2051 μs |  0.41 |    0.01 |   0.9766 |        - |    8.09 KB |        1.06 |
|                   |                      |       |              |             |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **10000** |   **410.096 μs** |   **9.8752 μs** |  **5.8766 μs** |  **2.34** |    **0.05** |  **74.2188** |  **31.2500** |  **628.33 KB** |        **8.06** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 10000 |   362.436 μs |   2.2969 μs |  1.3669 μs |  2.07 |    0.04 |  17.5781 |   3.9063 |  158.46 KB |        2.03 |
| NativeSingle      | TaLib(...)ltOsc [22] | 10000 |   175.404 μs |   5.4354 μs |  3.2345 μs |  1.00 |    0.02 |   8.7891 |   0.9766 |   77.98 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 10000 |    47.704 μs |   0.7540 μs |  0.4987 μs |  0.27 |    0.01 |   9.5215 |   1.7090 |    78.4 KB |        1.01 |
