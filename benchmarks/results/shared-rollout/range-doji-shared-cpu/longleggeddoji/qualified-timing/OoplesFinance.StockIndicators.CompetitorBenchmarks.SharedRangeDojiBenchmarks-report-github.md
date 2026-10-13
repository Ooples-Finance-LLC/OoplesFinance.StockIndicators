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
| **BuilderFull**       | **1000**  | **TaLib(...)dDoji [28]** |  **70.99 μs** |  **4.131 μs** |  **2.458 μs** |  **0.98** |    **0.12** |  **5.8594** |       **-** |  **57.26 KB** |       **14.41** |
| BuilderLatestOnly | 1000  | TaLib(...)dDoji [28] |  65.75 μs |  2.453 μs |  1.460 μs |  0.90 |    0.11 |  0.9766 |       - |   9.38 KB |        2.36 |
| NativeSingle      | 1000  | TaLib(...)dDoji [28] |  73.70 μs | 14.297 μs |  9.457 μs |  1.01 |    0.17 |       - |       - |   3.97 KB |        1.00 |
| NativeParallel    | 1000  | TaLib(...)dDoji [28] |  19.61 μs |  0.547 μs |  0.286 μs |  0.27 |    0.03 |  1.0986 |       - |    8.8 KB |        2.21 |
|                   |       |                      |           |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **TaLib(...)dDoji [28]** | **699.20 μs** | **30.743 μs** | **18.294 μs** |  **1.01** |    **0.07** | **66.4063** | **58.5938** | **549.84 KB** |       **14.04** |
| BuilderLatestOnly | 10000 | TaLib(...)dDoji [28] | 649.04 μs | 12.740 μs |  7.582 μs |  0.94 |    0.06 |  7.8125 |       - |  79.73 KB |        2.04 |
| NativeSingle      | 10000 | TaLib(...)dDoji [28] | 695.85 μs | 72.575 μs | 48.004 μs |  1.00 |    0.09 |  3.9063 |       - |  39.16 KB |        1.00 |
| NativeParallel    | 10000 | TaLib(...)dDoji [28] | 177.94 μs |  3.055 μs |  1.598 μs |  0.26 |    0.02 | 11.7188 |  1.9531 |  79.09 KB |        2.02 |
