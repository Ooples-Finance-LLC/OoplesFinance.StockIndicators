```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation          | Mean       | Error      | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------ |------ |------------------- |-----------:|-----------:|-----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **BuilderFull**       | **1000**  | **TaLib.Candles.Doji** |  **52.247 μs** |  **1.0177 μs** |  **0.5323 μs** |  **1.70** |    **0.12** |  **5.8594** |       **-** |  **57.26 KB** |       **14.42** |
| BuilderLatestOnly | 1000  | TaLib.Candles.Doji |  48.535 μs |  0.8854 μs |  0.5856 μs |  1.58 |    0.11 |  0.9766 |       - |   9.38 KB |        2.36 |
| NativeSingle      | 1000  | TaLib.Candles.Doji |  30.893 μs |  4.5691 μs |  2.3897 μs |  1.01 |    0.10 |  0.2441 |       - |   3.97 KB |        1.00 |
| NativeParallel    | 1000  | TaLib.Candles.Doji |   9.795 μs |  0.1483 μs |  0.0883 μs |  0.32 |    0.02 |  1.0986 |       - |    8.8 KB |        2.22 |
|                   |       |                    |            |            |            |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **TaLib.Candles.Doji** | **522.409 μs** | **17.9736 μs** | **11.8884 μs** |  **1.78** |    **0.05** | **66.4063** | **58.5938** | **549.85 KB** |       **14.05** |
| BuilderLatestOnly | 10000 | TaLib.Candles.Doji | 483.408 μs | 19.4272 μs | 10.1608 μs |  1.65 |    0.05 |  7.8125 |       - |  79.73 KB |        2.04 |
| NativeSingle      | 10000 | TaLib.Candles.Doji | 293.649 μs | 10.4388 μs |  6.2120 μs |  1.00 |    0.03 |  3.9063 |       - |  39.14 KB |        1.00 |
| NativeParallel    | 10000 | TaLib.Candles.Doji |  78.524 μs |  1.7832 μs |  1.1795 μs |  0.27 |    0.01 | 12.2070 |  1.9531 |  79.08 KB |        2.02 |
