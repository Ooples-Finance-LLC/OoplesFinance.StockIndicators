```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean       | Error     | StdDev    | Gen0    | Gen1    | Allocated |
|------------------ |------ |--------------------- |-----------:|----------:|----------:|--------:|--------:|----------:|
| **BuilderFull**       | **1000**  | **TaLib.Functions.Kama** |   **416.7 μs** |  **20.70 μs** |  **13.69 μs** |       **-** |       **-** |  **82.03 KB** |
| BuilderLatestOnly | 1000  | TaLib.Functions.Kama |   430.1 μs |  30.53 μs |  20.19 μs |  3.9063 |       - |  34.44 KB |
| **BuilderFull**       | **1000**  | **Trady(...)erage [44]** |   **486.5 μs** |  **95.57 μs** |  **63.22 μs** |  **7.8125** |       **-** |  **82.05 KB** |
| BuilderLatestOnly | 1000  | Trady(...)erage [44] |   423.6 μs |  21.40 μs |  11.19 μs |  3.9063 |       - |  34.44 KB |
| **BuilderFull**       | **10000** | **TaLib.Functions.Kama** | **4,304.2 μs** | **383.82 μs** | **253.87 μs** | **93.7500** | **62.5000** | **785.76 KB** |
| BuilderLatestOnly | 10000 | TaLib.Functions.Kama | 4,130.8 μs | 119.13 μs |  70.89 μs | 31.2500 |       - | 315.94 KB |
| **BuilderFull**       | **10000** | **Trady(...)erage [44]** | **4,397.8 μs** | **301.20 μs** | **179.24 μs** | **93.7500** | **62.5000** | **785.76 KB** |
| BuilderLatestOnly | 10000 | Trady(...)erage [44] | 4,188.2 μs | 134.69 μs |  89.09 μs | 31.2500 |       - | 315.93 KB |
