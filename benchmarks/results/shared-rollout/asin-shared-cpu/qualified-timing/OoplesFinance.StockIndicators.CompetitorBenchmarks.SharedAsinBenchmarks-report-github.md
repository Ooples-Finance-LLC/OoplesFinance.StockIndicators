```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|------------------ |------ |-----------:|----------:|----------:|------:|--------:|--------:|--------:|--------:|----------:|------------:|
| **BuilderFull**       | **1000**  |  **17.000 μs** | **0.5518 μs** | **0.3284 μs** |  **1.63** |    **0.06** |  **7.8125** |  **1.2207** |       **-** |  **64.31 KB** |        **8.17** |
| BuilderLatestOnly | 1000  |  13.103 μs | 0.5562 μs | 0.3310 μs |  1.26 |    0.05 |  1.9531 |       - |       - |  16.93 KB |        2.15 |
| NativeSingle      | 1000  |  10.435 μs | 0.6433 μs | 0.3365 μs |  1.00 |    0.04 |  0.9155 |       - |       - |   7.87 KB |        1.00 |
| NativeParallel    | 1000  |   5.159 μs | 0.4387 μs | 0.2294 μs |  0.49 |    0.03 |  1.0071 |       - |       - |   8.24 KB |        1.05 |
|                   |       |            |           |           |       |         |         |         |         |           |             |
| **BuilderFull**       | **10000** |  **98.081 μs** | **3.1061 μs** | **2.0545 μs** |  **0.97** |    **0.03** | **74.2188** | **60.5469** | **55.6641** | **628.24 KB** |        **8.04** |
| BuilderLatestOnly | 10000 |  38.526 μs | 1.0709 μs | 0.7083 μs |  0.38 |    0.01 | 19.0430 |  6.3477 |       - | 158.19 KB |        2.02 |
| NativeSingle      | 10000 | 101.255 μs | 4.5610 μs | 3.0168 μs |  1.00 |    0.04 |  8.7891 |  0.9766 |       - |  78.19 KB |        1.00 |
| NativeParallel    | 10000 |  29.458 μs | 1.5421 μs | 1.0200 μs |  0.29 |    0.01 |  9.5215 |  1.7090 |       - |  78.56 KB |        1.00 |
