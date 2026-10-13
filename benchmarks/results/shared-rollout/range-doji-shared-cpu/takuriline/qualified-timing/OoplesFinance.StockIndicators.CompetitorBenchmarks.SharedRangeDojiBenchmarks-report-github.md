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
| **BuilderFull**       | **1000**  | **TaLib(...)iLine [24]** |  **82.97 μs** |  **2.545 μs** |  **1.515 μs** |  **1.08** |    **0.02** |  **5.8594** |       **-** |  **57.26 KB** |       **14.41** |
| BuilderLatestOnly | 1000  | TaLib(...)iLine [24] |  76.23 μs |  2.947 μs |  1.950 μs |  0.99 |    0.03 |  0.9766 |       - |   9.38 KB |        2.36 |
| NativeSingle      | 1000  | TaLib(...)iLine [24] |  77.01 μs |  1.065 μs |  0.634 μs |  1.00 |    0.01 |       - |       - |   3.97 KB |        1.00 |
| NativeParallel    | 1000  | TaLib(...)iLine [24] |  23.15 μs |  0.283 μs |  0.169 μs |  0.30 |    0.00 |  1.0986 |       - |    8.8 KB |        2.21 |
|                   |       |                      |           |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **TaLib(...)iLine [24]** | **857.50 μs** | **33.914 μs** | **22.432 μs** |  **1.08** |    **0.04** | **62.5000** | **46.8750** | **549.88 KB** |       **14.04** |
| BuilderLatestOnly | 10000 | TaLib(...)iLine [24] | 772.68 μs | 28.124 μs | 14.709 μs |  0.97 |    0.03 |  7.8125 |       - |  79.72 KB |        2.04 |
| NativeSingle      | 10000 | TaLib(...)iLine [24] | 796.56 μs | 34.849 μs | 20.738 μs |  1.00 |    0.03 |  3.9063 |       - |  39.15 KB |        1.00 |
| NativeParallel    | 10000 | TaLib(...)iLine [24] | 210.80 μs |  2.654 μs |  1.579 μs |  0.26 |    0.01 | 11.7188 |  1.9531 |  79.09 KB |        2.02 |
