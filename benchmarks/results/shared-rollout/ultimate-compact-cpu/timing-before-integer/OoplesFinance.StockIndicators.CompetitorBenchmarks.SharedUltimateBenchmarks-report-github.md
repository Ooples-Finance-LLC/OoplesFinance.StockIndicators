```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain  
IterationCount=10  LaunchCount=1  UnrollFactor=1  
WarmupCount=10  

```
| Method            | Operation            | Count | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |--------------------- |------ |------------:|-------------:|-------------:|------------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **Skender.GetUltimate**  | **1000**  |   **222.59 μs** |     **7.458 μs** |     **4.933 μs** |   **220.80 μs** |  **1.42** |    **0.13** |   **7.8125** |        **-** |   **90.11 KB** |        **0.59** |
| BuilderLatestOnly | Skender.GetUltimate  | 1000  |   210.62 μs |     0.943 μs |     0.493 μs |   210.69 μs |  1.34 |    0.12 |   5.8594 |        - |   42.51 KB |        0.28 |
| NativeSingle      | Skender.GetUltimate  | 1000  |   158.38 μs |    21.850 μs |    14.452 μs |   156.85 μs |  1.01 |    0.13 |  18.5547 |   3.9063 |  152.83 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 1000  |    55.86 μs |    12.168 μs |     7.241 μs |    55.67 μs |  0.36 |    0.05 |  23.4375 |   5.8594 |  186.31 KB |        1.22 |
|                   |                      |       |             |              |              |             |       |         |          |          |            |             |
| **BuilderFull**       | **Skender.GetUltimate**  | **10000** | **1,873.14 μs** |    **45.527 μs** |    **23.811 μs** | **1,866.43 μs** |  **1.03** |    **0.08** |  **78.1250** |  **46.8750** |  **653.12 KB** |        **0.43** |
| BuilderLatestOnly | Skender.GetUltimate  | 10000 | 3,005.13 μs | 2,158.253 μs | 1,427.551 μs | 2,253.20 μs |  1.66 |    0.76 |        - |        - |  183.59 KB |        0.12 |
| NativeSingle      | Skender.GetUltimate  | 10000 | 1,822.12 μs |   197.913 μs |   130.907 μs | 1,845.46 μs |  1.00 |    0.10 | 156.2500 |  93.7500 | 1524.16 KB |        1.00 |
| NativeParallel    | Skender.GetUltimate  | 10000 |   579.94 μs |   145.330 μs |    96.127 μs |   526.20 μs |  0.32 |    0.06 | 203.1250 | 144.5313 | 1557.43 KB |        1.02 |
|                   |                      |       |             |              |              |             |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **1000**  |   **224.99 μs** |     **3.893 μs** |     **2.575 μs** |   **225.65 μs** |  **8.75** |    **0.35** |  **11.7188** |   **1.9531** |   **90.12 KB** |       **11.77** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 1000  |   219.34 μs |     6.928 μs |     4.123 μs |   218.52 μs |  8.53 |    0.36 |   5.8594 |        - |   42.51 KB |        5.55 |
| NativeSingle      | TaLib(...)ltOsc [22] | 1000  |    25.75 μs |     1.857 μs |     0.971 μs |    26.06 μs |  1.00 |    0.05 |   0.7324 |        - |    7.66 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 1000  |    17.16 μs |    18.618 μs |    11.080 μs |    10.26 μs |  0.67 |    0.41 |   0.9766 |        - |    8.09 KB |        1.06 |
|                   |                      |       |             |              |              |             |       |         |          |          |            |             |
| **BuilderFull**       | **TaLib(...)ltOsc [22]** | **10000** | **1,906.60 μs** |    **41.597 μs** |    **27.514 μs** | **1,901.86 μs** |  **9.14** |    **1.07** |  **62.5000** |  **31.2500** |  **653.24 KB** |        **8.38** |
| BuilderLatestOnly | TaLib(...)ltOsc [22] | 10000 | 1,900.30 μs |   106.697 μs |    70.573 μs | 1,861.13 μs |  9.11 |    1.11 |  15.6250 |        - |  183.25 KB |        2.35 |
| NativeSingle      | TaLib(...)ltOsc [22] | 10000 |   211.44 μs |    38.675 μs |    25.581 μs |   210.29 μs |  1.01 |    0.17 |   7.8125 |        - |   77.98 KB |        1.00 |
| NativeParallel    | TaLib(...)ltOsc [22] | 10000 |    51.48 μs |     1.444 μs |     0.955 μs |    51.57 μs |  0.25 |    0.03 |   9.2773 |   1.4648 |    78.4 KB |        1.01 |
