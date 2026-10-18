```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------ |------ |--------------------- |----------:|----------:|----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **BuilderFull**       | **1000**  | **TaLib.Candles.InNeck** |  **28.77 μs** |  **1.884 μs** |  **1.246 μs** |  **0.67** |    **0.03** |  **9.2773** |  **1.4648** |  **75.99 KB** |       **19.14** |
| BuilderLatestOnly | 1000  | TaLib.Candles.InNeck |  24.15 μs |  1.450 μs |  0.959 μs |  0.56 |    0.02 |  3.4180 |       - |  28.11 KB |        7.08 |
| NativeSingle      | 1000  | TaLib.Candles.InNeck |  42.85 μs |  1.104 μs |  0.657 μs |  1.00 |    0.02 |  0.2441 |       - |   3.97 KB |        1.00 |
| NativeParallel    | 1000  | TaLib.Candles.InNeck |  13.71 μs |  0.459 μs |  0.304 μs |  0.32 |    0.01 |  1.0986 |       - |    8.8 KB |        2.22 |
|                   |       |                      |           |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **1000**  | **TaLib.Candles.OnNeck** |  **28.77 μs** |  **0.909 μs** |  **0.601 μs** |  **0.67** |    **0.03** |  **9.2773** |  **1.4648** |  **75.99 KB** |       **19.14** |
| BuilderLatestOnly | 1000  | TaLib.Candles.OnNeck |  25.03 μs |  2.226 μs |  1.473 μs |  0.58 |    0.04 |  3.5400 |  0.1221 |   28.1 KB |        7.08 |
| NativeSingle      | 1000  | TaLib.Candles.OnNeck |  43.18 μs |  2.998 μs |  1.784 μs |  1.00 |    0.06 |  0.2441 |       - |   3.97 KB |        1.00 |
| NativeParallel    | 1000  | TaLib.Candles.OnNeck |  13.49 μs |  0.251 μs |  0.149 μs |  0.31 |    0.01 |  1.0986 |       - |    8.8 KB |        2.22 |
|                   |       |                      |           |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **1000**  | **TaLib(...)sting [23]** |  **28.32 μs** |  **1.162 μs** |  **0.768 μs** |  **0.68** |    **0.02** |  **9.2773** |  **1.4648** |  **75.99 KB** |       **19.14** |
| BuilderLatestOnly | 1000  | TaLib(...)sting [23] |  24.78 μs |  1.313 μs |  0.869 μs |  0.59 |    0.02 |  3.4180 |       - |  28.11 KB |        7.08 |
| NativeSingle      | 1000  | TaLib(...)sting [23] |  41.81 μs |  1.771 μs |  1.054 μs |  1.00 |    0.03 |  0.2441 |       - |   3.97 KB |        1.00 |
| NativeParallel    | 1000  | TaLib(...)sting [23] |  13.87 μs |  0.684 μs |  0.453 μs |  0.33 |    0.01 |  1.0986 |       - |    8.8 KB |        2.22 |
|                   |       |                      |           |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **TaLib.Candles.InNeck** | **208.03 μs** |  **9.621 μs** |  **6.363 μs** |  **0.47** |    **0.02** | **68.3594** | **42.9688** | **568.56 KB** |       **14.52** |
| BuilderLatestOnly | 10000 | TaLib.Candles.InNeck | 171.56 μs |  3.865 μs |  2.021 μs |  0.39 |    0.01 | 11.7188 |  1.9531 |  98.43 KB |        2.51 |
| NativeSingle      | 10000 | TaLib.Candles.InNeck | 441.86 μs | 26.144 μs | 17.293 μs |  1.00 |    0.05 |  3.9063 |       - |  39.16 KB |        1.00 |
| NativeParallel    | 10000 | TaLib.Candles.InNeck | 119.34 μs |  1.360 μs |  0.900 μs |  0.27 |    0.01 | 11.7188 |  1.9531 |  79.14 KB |        2.02 |
|                   |       |                      |           |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **TaLib.Candles.OnNeck** | **205.95 μs** |  **2.417 μs** |  **1.598 μs** |  **0.49** |    **0.01** | **68.3594** | **42.9688** | **568.56 KB** |       **14.52** |
| BuilderLatestOnly | 10000 | TaLib.Candles.OnNeck | 170.50 μs |  1.480 μs |  0.880 μs |  0.41 |    0.01 | 11.7188 |  1.9531 |  98.43 KB |        2.51 |
| NativeSingle      | 10000 | TaLib.Candles.OnNeck | 420.56 μs | 11.708 μs |  7.744 μs |  1.00 |    0.02 |  3.9063 |       - |  39.16 KB |        1.00 |
| NativeParallel    | 10000 | TaLib.Candles.OnNeck | 121.66 μs |  4.511 μs |  2.983 μs |  0.29 |    0.01 | 11.7188 |  1.9531 |  79.14 KB |        2.02 |
|                   |       |                      |           |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **TaLib(...)sting [23]** | **205.07 μs** |  **5.735 μs** |  **3.793 μs** |  **0.47** |    **0.04** | **68.3594** | **42.9688** | **568.56 KB** |       **14.52** |
| BuilderLatestOnly | 10000 | TaLib(...)sting [23] | 176.80 μs |  6.729 μs |  4.451 μs |  0.41 |    0.04 | 11.7188 |  1.9531 |  98.43 KB |        2.51 |
| NativeSingle      | 10000 | TaLib(...)sting [23] | 439.50 μs | 64.435 μs | 42.620 μs |  1.01 |    0.13 |  3.9063 |       - |  39.16 KB |        1.00 |
| NativeParallel    | 10000 | TaLib(...)sting [23] | 119.69 μs |  4.451 μs |  2.944 μs |  0.27 |    0.02 | 11.7188 |  1.9531 |  79.14 KB |        2.02 |
