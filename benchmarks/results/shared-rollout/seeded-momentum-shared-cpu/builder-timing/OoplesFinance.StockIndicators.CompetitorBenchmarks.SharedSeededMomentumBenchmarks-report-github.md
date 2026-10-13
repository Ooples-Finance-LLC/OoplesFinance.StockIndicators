```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean        | Error     | StdDev    | Gen0     | Allocated |
|------------------ |------ |--------------------- |------------:|----------:|----------:|---------:|----------:|
| **BuilderFull**       | **1000**  | **Skender.GetPmo**       |    **932.1 μs** | **102.32 μs** |  **60.89 μs** |        **-** |  **80.97 KB** |
| BuilderLatestOnly | 1000  | Skender.GetPmo       |    867.7 μs |  40.86 μs |  27.03 μs |        - |  33.32 KB |
| **BuilderFull**       | **1000**  | **Skender.GetTrix**      |    **878.0 μs** |  **34.61 μs** |  **18.10 μs** |   **7.8125** |  **89.12 KB** |
| BuilderLatestOnly | 1000  | Skender.GetTrix      |    886.4 μs |  51.22 μs |  33.88 μs |        - |   41.5 KB |
| **BuilderFull**       | **1000**  | **Skender.GetTsi**       |  **1,431.5 μs** |  **25.40 μs** |  **16.80 μs** |   **7.8125** |  **81.01 KB** |
| BuilderLatestOnly | 1000  | Skender.GetTsi       |  1,433.3 μs |  59.61 μs |  39.43 μs |        - |   33.4 KB |
| **BuilderFull**       | **1000**  | **TaLib.Functions.Trix** |    **912.4 μs** |  **29.28 μs** |  **17.42 μs** |   **7.8125** |  **89.12 KB** |
| BuilderLatestOnly | 1000  | TaLib.Functions.Trix |    905.8 μs |  21.17 μs |  11.07 μs |        - |   41.5 KB |
| **BuilderFull**       | **10000** | **Skender.GetPmo**       |  **9,009.3 μs** | **311.38 μs** | **185.30 μs** |  **90.9091** | **785.16 KB** |
| BuilderLatestOnly | 10000 | Skender.GetPmo       |  8,881.3 μs | 321.71 μs | 191.44 μs |        - | 315.34 KB |
| **BuilderFull**       | **10000** | **Skender.GetTrix**      |  **8,965.7 μs** | **132.31 μs** |  **69.20 μs** |  **83.3333** | **863.59 KB** |
| BuilderLatestOnly | 10000 | Skender.GetTrix      |  8,789.1 μs | 328.24 μs | 195.33 μs |        - |  393.8 KB |
| **BuilderFull**       | **10000** | **Skender.GetTsi**       | **14,835.8 μs** | **441.96 μs** | **292.33 μs** |        **-** | **785.65 KB** |
| BuilderLatestOnly | 10000 | Skender.GetTsi       | 14,524.0 μs | 842.29 μs | 501.23 μs |        - | 315.84 KB |
| **BuilderFull**       | **10000** | **TaLib.Functions.Trix** |  **9,504.4 μs** | **343.27 μs** | **204.28 μs** | **100.0000** | **863.69 KB** |
| BuilderLatestOnly | 10000 | TaLib.Functions.Trix |  9,532.2 μs | 479.24 μs | 316.98 μs |        - |  393.8 KB |
