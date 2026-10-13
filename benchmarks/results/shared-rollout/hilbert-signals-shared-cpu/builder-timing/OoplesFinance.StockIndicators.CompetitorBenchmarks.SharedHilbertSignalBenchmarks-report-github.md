```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev    | Gen0     | Gen1     | Allocated  |
|------------------ |------ |--------------------- |----------:|----------:|----------:|---------:|---------:|-----------:|
| **BuilderFull**       | **1000**  | **TaLib(...)Phase [25]** |  **4.660 ms** | **0.4493 ms** | **0.2972 ms** |        **-** |        **-** |  **473.33 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)Phase [25] |  4.501 ms | 0.0954 ms | 0.0567 ms |  31.2500 |        - |  425.83 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)tSine [22]** |  **4.553 ms** | **0.0931 ms** | **0.0616 ms** |  **31.2500** |        **-** |  **489.33 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)tSine [22] |  4.450 ms | 0.0437 ms | 0.0289 ms |  31.2500 |        - |  441.72 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)dline [27]** |  **1.887 ms** | **0.0604 ms** | **0.0359 ms** |        **-** |        **-** |   **69.28 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)dline [27] |  1.939 ms | 0.0583 ms | 0.0385 ms |        - |        - |   21.67 KB |
| **BuilderFull**       | **1000**  | **TaLib(...)dMode [27]** |  **5.205 ms** | **0.2146 ms** | **0.1277 ms** |  **31.2500** |        **-** |  **476.05 KB** |
| BuilderLatestOnly | 1000  | TaLib(...)dMode [27] |  5.307 ms | 0.2451 ms | 0.1459 ms |  31.2500 |        - |  428.45 KB |
| **BuilderFull**       | **10000** | **TaLib(...)Phase [25]** | **47.040 ms** | **0.8892 ms** | **0.4651 ms** | **500.0000** | **250.0000** | **4699.92 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)Phase [25] | 55.973 ms | 6.9386 ms | 4.5895 ms | 500.0000 |        - | 4230.11 KB |
| **BuilderFull**       | **10000** | **TaLib(...)tSine [22]** | **46.890 ms** | **0.7048 ms** | **0.4662 ms** | **500.0000** | **250.0000** | **4856.43 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)tSine [22] | 51.366 ms | 6.0795 ms | 4.0212 ms | 500.0000 | 250.0000 | 4386.62 KB |
| **BuilderFull**       | **10000** | **TaLib(...)dline [27]** | **19.968 ms** | **1.4585 ms** | **0.8679 ms** |        **-** |        **-** |  **633.49 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)dline [27] | 19.679 ms | 1.0386 ms | 0.6870 ms |        - |        - |  163.63 KB |
| **BuilderFull**       | **10000** | **TaLib(...)dMode [27]** | **54.379 ms** | **3.3581 ms** | **1.7564 ms** | **500.0000** | **250.0000** | **4702.46 KB** |
| BuilderLatestOnly | 10000 | TaLib(...)dMode [27] | 54.157 ms | 1.5922 ms | 0.8328 ms | 500.0000 |        - | 4232.72 KB |
