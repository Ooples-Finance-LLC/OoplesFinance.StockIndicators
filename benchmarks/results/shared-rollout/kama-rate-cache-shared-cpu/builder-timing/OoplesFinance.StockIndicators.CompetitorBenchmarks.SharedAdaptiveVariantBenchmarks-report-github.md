```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean       | Error     | StdDev    | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |-----------:|----------:|----------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **TaLib.Functions.Kama** |   **390.6 μs** |  **13.93 μs** |   **9.21 μs** |       **-** |       **-** |  **82.03 KB** |
| BuilderLatestOnly | 1000  | TaLib.Functions.Kama |   385.0 μs |   2.40 μs |   1.25 μs |  3.9063 |       - |  34.43 KB |
| **BuilderFull**       | **1000**  | **Trady(...)erage [44]** |   **417.7 μs** |  **55.84 μs** |  **33.23 μs** |  **7.8125** |       **-** |  **82.05 KB** |
| BuilderLatestOnly | 1000  | Trady(...)erage [44] |   387.2 μs |   5.92 μs |   3.52 μs |  3.9063 |       - |  34.42 KB |
| **BuilderFull**       | **10000** | **TaLib.Functions.Kama** | **3,982.3 μs** | **265.10 μs** | **138.65 μs** | **93.7500** | **62.5000** | **785.78 KB** |
| BuilderLatestOnly | 10000 | TaLib.Functions.Kama | 3,890.9 μs |  75.26 μs |  49.78 μs | 31.2500 |       - | 315.94 KB |
| **BuilderFull**       | **10000** | **Trady(...)erage [44]** | **3,954.4 μs** |  **75.65 μs** |  **39.57 μs** | **93.7500** | **62.5000** | **785.76 KB** |
| BuilderLatestOnly | 10000 | Trady(...)erage [44] | 3,865.6 μs |  71.69 μs |  37.49 μs | 31.2500 |       - | 315.94 KB |
