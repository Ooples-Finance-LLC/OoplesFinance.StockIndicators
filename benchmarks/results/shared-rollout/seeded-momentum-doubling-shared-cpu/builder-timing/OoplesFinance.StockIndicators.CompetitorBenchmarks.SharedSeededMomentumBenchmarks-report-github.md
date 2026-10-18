```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean        | Error     | StdDev    | Gen0    | Allocated |
|------------------ |------ |--------------------- |------------:|----------:|----------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **Skender.GetPmo**       |    **949.0 μs** | **118.74 μs** |  **70.66 μs** |       **-** |  **80.96 KB** |
| BuilderLatestOnly | 1000  | Skender.GetPmo       |    959.0 μs | 190.42 μs | 125.95 μs |       - |  33.33 KB |
| **BuilderFull**       | **1000**  | **Skender.GetTrix**      |    **852.2 μs** | **104.40 μs** |  **69.05 μs** |  **7.8125** |  **89.12 KB** |
| BuilderLatestOnly | 1000  | Skender.GetTrix      |    800.8 μs |  12.92 μs |   8.55 μs |       - |   41.5 KB |
| **BuilderFull**       | **1000**  | **Skender.GetTsi**       |  **1,186.1 μs** |  **43.03 μs** |  **25.61 μs** |  **7.8125** |  **81.01 KB** |
| BuilderLatestOnly | 1000  | Skender.GetTsi       |  1,182.0 μs |  20.56 μs |  12.24 μs |       - |   33.4 KB |
| **BuilderFull**       | **1000**  | **TaLib.Functions.Trix** |    **838.8 μs** |  **50.47 μs** |  **33.38 μs** |  **7.8125** |  **89.11 KB** |
| BuilderLatestOnly | 1000  | TaLib.Functions.Trix |    807.7 μs |  38.68 μs |  20.23 μs |       - |   41.5 KB |
| **BuilderFull**       | **10000** | **Skender.GetPmo**       |  **8,902.3 μs** | **149.97 μs** |  **78.44 μs** | **83.3333** | **785.14 KB** |
| BuilderLatestOnly | 10000 | Skender.GetPmo       |  8,838.4 μs | 150.68 μs |  89.66 μs |       - | 315.34 KB |
| **BuilderFull**       | **10000** | **Skender.GetTrix**      |  **8,432.5 μs** | **118.65 μs** |  **78.48 μs** | **83.3333** | **863.62 KB** |
| BuilderLatestOnly | 10000 | Skender.GetTrix      |  8,152.2 μs | 299.89 μs | 178.46 μs |       - | 393.82 KB |
| **BuilderFull**       | **10000** | **Skender.GetTsi**       | **12,505.0 μs** | **627.61 μs** | **415.13 μs** |       **-** | **785.91 KB** |
| BuilderLatestOnly | 10000 | Skender.GetTsi       | 12,163.4 μs | 210.00 μs | 109.84 μs |       - | 315.56 KB |
| **BuilderFull**       | **10000** | **TaLib.Functions.Trix** |  **8,428.1 μs** | **139.43 μs** |  **72.93 μs** | **83.3333** | **863.59 KB** |
| BuilderLatestOnly | 10000 | TaLib.Functions.Trix |  8,385.4 μs | 236.98 μs | 141.02 μs |       - | 393.75 KB |
