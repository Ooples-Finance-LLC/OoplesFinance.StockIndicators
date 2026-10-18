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
| **BuilderFull**       | **1000**  | **TaLib(...)eDoji [28]** |  **77.40 μs** |  **5.230 μs** |  **3.459 μs** |  **1.31** |    **0.06** |  **5.8594** |       **-** |  **57.26 KB** |       **14.41** |
| BuilderLatestOnly | 1000  | TaLib(...)eDoji [28] |  69.94 μs |  2.943 μs |  1.751 μs |  1.18 |    0.04 |  0.9766 |       - |   9.38 KB |        2.36 |
| NativeSingle      | 1000  | TaLib(...)eDoji [28] |  59.19 μs |  2.171 μs |  1.292 μs |  1.00 |    0.03 |       - |       - |   3.97 KB |        1.00 |
| NativeParallel    | 1000  | TaLib(...)eDoji [28] |  17.28 μs |  0.288 μs |  0.190 μs |  0.29 |    0.01 |  1.0986 |       - |    8.8 KB |        2.21 |
|                   |       |                      |           |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **TaLib(...)eDoji [28]** | **755.71 μs** | **10.756 μs** |  **5.626 μs** |  **1.26** |    **0.03** | **62.5000** | **46.8750** | **549.88 KB** |       **14.04** |
| BuilderLatestOnly | 10000 | TaLib(...)eDoji [28] | 697.71 μs | 30.653 μs | 20.275 μs |  1.17 |    0.04 |  7.8125 |       - |  79.76 KB |        2.04 |
| NativeSingle      | 10000 | TaLib(...)eDoji [28] | 598.72 μs | 20.740 μs | 12.342 μs |  1.00 |    0.03 |  3.9063 |       - |  39.16 KB |        1.00 |
| NativeParallel    | 10000 | TaLib(...)eDoji [28] | 153.85 μs |  4.617 μs |  3.054 μs |  0.26 |    0.01 | 11.7188 |  1.9531 |  79.09 KB |        2.02 |
