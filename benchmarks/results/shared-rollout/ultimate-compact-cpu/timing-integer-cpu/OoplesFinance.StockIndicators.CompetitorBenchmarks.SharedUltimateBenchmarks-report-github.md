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
| **BuilderFull**       | **Skender.GetUltimate**  | **1000**  |    **79.540 μs** |  **1.7960 μs** |  **1.1879 μs** |  **0.70** |    **0.02** |   **7.8125** |   **0.9766** |   **65.42 KB** |        **0.43** |
| BuilderLatestOnly | Skender.GetUltimate  | 1000  |    75.287 μs |  2.7911 μs |  1.8461 μs |  0.66 |    0.02 |   1.9531 |        - |   17.81 KB |        0.12 |
| NativeSingle      | Skender.GetUltimate  | 1000  |   113.373 μs |  3.4883 μs |  2.3073 μs |  1.00 |    0.03 |  18.5547 |   3.9063 |  152.83 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 1000  |    46.071 μs |  4.5182 μs |  2.9885 μs |  0.41 |    0.03 |  23.4375 |   5.8594 |  186.31 KB |        1.22 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetUltimate**  | **10000** |   **633.317 μs** | **15.3056 μs** | **10.1237 μs** |  **0.49** |    **0.02** |  **74.2188** |  **31.2500** |  **628.33 KB** |        **0.41** |
| BuilderLatestOnly | Skender.GetUltimate  | 10000 |   587.002 μs |  5.0640 μs |  3.3495 μs |  0.45 |    0.01 |  15.6250 |   3.9063 |  158.48 KB |        0.10 |
| NativeSingle      | Skender.GetUltimate  | 10000 | 1,294.181 μs | 59.6228 μs | 39.4368 μs |  1.00 |    0.04 | 179.6875 | 109.3750 | 1523.99 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 10000 |   465.920 μs | 27.0113 μs | 17.8663 μs |  0.36 |    0.02 | 203.1250 | 136.7188 | 1557.44 KB |        1.02 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **1000**  |    **81.648 μs** |  **1.4403 μs** |  **0.8571 μs** |  **4.66** |    **0.07** |   **7.8125** |   **0.9766** |   **65.42 KB** |        **8.54** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 1000  |    76.747 μs |  2.9030 μs |  1.9202 μs |  4.38 |    0.11 |   1.9531 |        - |   17.81 KB |        2.33 |
| NativeSingle      | TaLib(...)ltOsc [22] | 1000  |    17.525 μs |  0.3013 μs |  0.1793 μs |  1.00 |    0.01 |   0.8545 |        - |    7.66 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 1000  |     7.029 μs |  0.3204 μs |  0.2119 μs |  0.40 |    0.01 |   0.9766 |        - |    8.09 KB |        1.06 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **10000** |   **634.285 μs** |  **9.3913 μs** |  **4.9119 μs** |  **3.60** |    **0.12** |  **74.2188** |  **31.2500** |  **628.33 KB** |        **8.06** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 10000 |   594.865 μs |  9.7669 μs |  6.4602 μs |  3.38 |    0.12 |  15.6250 |   3.9063 |  158.48 KB |        2.03 |
| NativeSingle      | TaLib(...)ltOsc [22] | 10000 |   176.231 μs |  9.5521 μs |  6.3181 μs |  1.00 |    0.05 |   8.7891 |   0.9766 |   77.98 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 10000 |    46.935 μs |  1.1576 μs |  0.7657 μs |  0.27 |    0.01 |   9.5215 |   1.7090 |    78.4 KB |        1.01 |
