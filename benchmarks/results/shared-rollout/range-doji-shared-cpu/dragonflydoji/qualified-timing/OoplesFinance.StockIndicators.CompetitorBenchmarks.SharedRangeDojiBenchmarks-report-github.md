```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Operation            | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------ |------ |--------------------- |----------:|----------:|---------:|------:|--------:|--------:|--------:|----------:|------------:|
| **BuilderFull**       | **1000**  | **TaLib(...)yDoji [27]** |  **81.84 μs** |  **3.808 μs** | **1.992 μs** |  **1.26** |    **0.11** |  **5.8594** |       **-** |  **57.26 KB** |       **14.41** |
| BuilderLatestOnly | 1000  | TaLib(...)yDoji [27] |  78.33 μs |  4.678 μs | 3.094 μs |  1.21 |    0.11 |  0.9766 |       - |   9.38 KB |        2.36 |
| NativeSingle      | 1000  | TaLib(...)yDoji [27] |  65.30 μs | 10.252 μs | 6.101 μs |  1.01 |    0.13 |       - |       - |   3.97 KB |        1.00 |
| NativeParallel    | 1000  | TaLib(...)yDoji [27] |  17.14 μs |  0.251 μs | 0.149 μs |  0.26 |    0.02 |  1.0986 |       - |    8.8 KB |        2.21 |
|                   |       |                      |           |           |          |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **TaLib(...)yDoji [27]** | **814.73 μs** | **14.782 μs** | **7.731 μs** |  **1.39** |    **0.02** | **62.5000** | **46.8750** | **549.88 KB** |       **14.04** |
| BuilderLatestOnly | 10000 | TaLib(...)yDoji [27] | 762.82 μs |  9.582 μs | 6.338 μs |  1.30 |    0.02 |  7.8125 |       - |  79.72 KB |        2.04 |
| NativeSingle      | 10000 | TaLib(...)yDoji [27] | 586.23 μs | 11.304 μs | 6.727 μs |  1.00 |    0.02 |  3.9063 |       - |  39.15 KB |        1.00 |
| NativeParallel    | 10000 | TaLib(...)yDoji [27] | 155.34 μs |  4.195 μs | 2.775 μs |  0.27 |    0.01 | 11.7188 |  1.9531 |  79.14 KB |        2.02 |
