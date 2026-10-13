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
| **BuilderFull**       | **1000**  | **TaLib(...)tside [26]** | **11.591 μs** | **1.8870 μs** | **1.2482 μs** |  **3.82** |    **0.40** |  **6.8359** |  **0.9766** |  **57.44 KB** |       **14.47** |
| BuilderLatestOnly | 1000  | TaLib(...)tside [26] |  6.342 μs | 0.2992 μs | 0.1979 μs |  2.09 |    0.08 |  1.1597 |       - |   9.59 KB |        2.42 |
| NativeSingle      | 1000  | TaLib(...)tside [26] |  3.036 μs | 0.1323 μs | 0.0787 μs |  1.00 |    0.03 |  0.4730 |       - |   3.97 KB |        1.00 |
| NativeParallel    | 1000  | TaLib(...)tside [26] |  3.522 μs | 0.2993 μs | 0.1980 μs |  1.16 |    0.07 |  1.0681 |       - |   8.58 KB |        2.16 |
|                   |       |                      |           |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **TaLib(...)tside [26]** | **58.412 μs** | **6.7404 μs** | **4.0111 μs** |  **2.08** |    **0.14** | **66.4063** | **65.4297** |    **550 KB** |       **14.06** |
| BuilderLatestOnly | 10000 | TaLib(...)tside [26] | 22.387 μs | 1.4415 μs | 0.9535 μs |  0.80 |    0.04 |  9.6436 |  1.8311 |  79.91 KB |        2.04 |
| NativeSingle      | 10000 | TaLib(...)tside [26] | 28.108 μs | 1.0627 μs | 0.6324 μs |  1.00 |    0.03 |  4.6387 |       - |  39.13 KB |        1.00 |
| NativeParallel    | 10000 | TaLib(...)tside [26] | 21.182 μs | 2.9553 μs | 1.9547 μs |  0.75 |    0.07 | 12.5732 |  1.4648 |  78.92 KB |        2.02 |
