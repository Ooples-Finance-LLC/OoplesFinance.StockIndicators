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
| **BuilderFull**       | **Skender.GetUltimate**  | **1000**  |    **57.682 μs** |  **3.0429 μs** |  **2.0127 μs** |  **0.51** |    **0.02** |   **7.8125** |   **0.9766** |   **65.39 KB** |        **0.43** |
| BuilderLatestOnly | Skender.GetUltimate  | 1000  |    52.530 μs |  2.1585 μs |  1.4277 μs |  0.47 |    0.02 |   1.9531 |        - |   17.81 KB |        0.12 |
| NativeSingle      | Skender.GetUltimate  | 1000  |   112.308 μs |  4.1626 μs |  2.4771 μs |  1.00 |    0.03 |  18.5547 |   3.9063 |  152.83 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 1000  |    48.632 μs |  6.7332 μs |  4.0068 μs |  0.43 |    0.04 |  23.4375 |   5.8594 |  186.31 KB |        1.22 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetUltimate**  | **10000** |   **416.264 μs** |  **8.8477 μs** |  **5.2651 μs** |  **0.33** |    **0.01** |  **74.2188** |  **31.2500** |  **628.29 KB** |        **0.41** |
| BuilderLatestOnly | Skender.GetUltimate  | 10000 |   374.542 μs |  5.4097 μs |  3.5782 μs |  0.29 |    0.01 |  17.5781 |   3.9063 |  158.46 KB |        0.10 |
| NativeSingle      | Skender.GetUltimate  | 10000 | 1,278.303 μs | 53.5428 μs | 31.8625 μs |  1.00 |    0.03 | 179.6875 | 109.3750 | 1523.99 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 10000 |   517.304 μs | 60.7901 μs | 40.2089 μs |  0.40 |    0.03 | 203.1250 | 136.7188 | 1557.44 KB |        1.02 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **1000**  |    **57.735 μs** |  **2.4886 μs** |  **1.6461 μs** |  **3.30** |    **0.10** |   **7.8125** |   **0.9766** |   **65.39 KB** |        **8.54** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 1000  |    52.887 μs |  2.6021 μs |  1.7212 μs |  3.02 |    0.10 |   1.9531 |        - |   17.81 KB |        2.33 |
| NativeSingle      | TaLib(...)ltOsc [22] | 1000  |    17.523 μs |  0.4611 μs |  0.2412 μs |  1.00 |    0.02 |   0.8545 |        - |    7.66 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 1000  |     7.191 μs |  0.1811 μs |  0.1198 μs |  0.41 |    0.01 |   0.9766 |        - |    8.09 KB |        1.06 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **10000** |   **418.290 μs** | **11.0013 μs** |  **7.2767 μs** |  **2.36** |    **0.07** |  **74.2188** |  **31.2500** |  **628.29 KB** |        **8.06** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 10000 |   380.389 μs |  6.2357 μs |  4.1245 μs |  2.14 |    0.06 |  17.5781 |   3.9063 |  158.46 KB |        2.03 |
| NativeSingle      | TaLib(...)ltOsc [22] | 10000 |   177.522 μs |  7.2862 μs |  4.8194 μs |  1.00 |    0.04 |   8.7891 |   0.9766 |   77.98 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 10000 |    46.751 μs |  0.5718 μs |  0.3782 μs |  0.26 |    0.01 |   9.5215 |   1.7090 |    78.4 KB |        1.01 |
