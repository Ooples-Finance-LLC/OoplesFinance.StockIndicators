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
| **BuilderFull**       | **Skender.GetUltimate**  | **1000**  |    **59.328 μs** |  **2.6361 μs** |  **1.7436 μs** |  **0.50** |    **0.02** |   **7.8125** |   **0.9766** |   **65.39 KB** |        **0.43** |
| BuilderLatestOnly | Skender.GetUltimate  | 1000  |    53.169 μs |  2.2894 μs |  1.3624 μs |  0.44 |    0.01 |   1.9531 |        - |   17.81 KB |        0.12 |
| NativeSingle      | Skender.GetUltimate  | 1000  |   119.604 μs |  4.9617 μs |  2.9526 μs |  1.00 |    0.03 |  18.5547 |   3.9063 |  152.83 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 1000  |    45.146 μs |  5.6420 μs |  3.7319 μs |  0.38 |    0.03 |  23.4375 |   5.8594 |  186.31 KB |        1.22 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetUltimate**  | **10000** |   **414.960 μs** |  **4.7230 μs** |  **2.8106 μs** |  **0.33** |    **0.00** |  **74.2188** |  **31.2500** |  **628.29 KB** |        **0.41** |
| BuilderLatestOnly | Skender.GetUltimate  | 10000 |   375.711 μs |  4.6327 μs |  3.0643 μs |  0.29 |    0.00 |  17.5781 |   3.9063 |  158.46 KB |        0.10 |
| NativeSingle      | Skender.GetUltimate  | 10000 | 1,275.842 μs | 27.5761 μs | 14.4229 μs |  1.00 |    0.02 | 179.6875 | 109.3750 |    1524 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 10000 |   514.056 μs | 70.7697 μs | 46.8098 μs |  0.40 |    0.04 | 203.1250 | 144.5313 | 1557.44 KB |        1.02 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **1000**  |    **60.004 μs** |  **2.3659 μs** |  **1.5649 μs** |  **3.41** |    **0.13** |   **7.8125** |   **0.9766** |   **65.39 KB** |        **8.54** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 1000  |    57.946 μs |  2.2247 μs |  1.4715 μs |  3.29 |    0.12 |   1.9531 |        - |   17.81 KB |        2.33 |
| NativeSingle      | TaLib(...)ltOsc [22] | 1000  |    17.632 μs |  0.7863 μs |  0.5201 μs |  1.00 |    0.04 |   0.8545 |        - |    7.66 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 1000  |     7.583 μs |  0.2337 μs |  0.1391 μs |  0.43 |    0.01 |   0.9766 |        - |    8.09 KB |        1.06 |
|                   |                      |       |              |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **10000** |   **421.074 μs** |  **7.7646 μs** |  **5.1358 μs** |  **2.40** |    **0.05** |  **74.2188** |  **31.2500** |  **628.29 KB** |        **8.06** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 10000 |   389.656 μs |  4.1049 μs |  2.7151 μs |  2.23 |    0.04 |  17.5781 |   3.9063 |  158.46 KB |        2.03 |
| NativeSingle      | TaLib(...)ltOsc [22] | 10000 |   175.142 μs |  5.0837 μs |  3.3626 μs |  1.00 |    0.03 |   8.7891 |   0.9766 |   77.98 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 10000 |    47.269 μs |  1.0233 μs |  0.6768 μs |  0.27 |    0.01 |   9.5215 |   1.7090 |    78.4 KB |        1.01 |
