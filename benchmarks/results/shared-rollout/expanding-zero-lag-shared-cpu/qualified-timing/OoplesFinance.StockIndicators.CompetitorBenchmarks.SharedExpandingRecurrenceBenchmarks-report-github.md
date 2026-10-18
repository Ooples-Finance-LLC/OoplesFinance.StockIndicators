```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation       | Count | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------ |---------------- |------ |------------:|-----------:|----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **BuilderFull**       | **QuanTAlib.Zlema** | **1000**  |   **264.04 μs** |   **5.350 μs** |  **3.184 μs** | **10.07** |    **0.34** |   **5.8594** |       **-** |      **57 KB** |        **1.20** |
| BuilderLatestOnly | QuanTAlib.Zlema | 1000  |   268.42 μs |   9.135 μs |  4.778 μs | 10.24 |    0.36 |        - |       - |    9.13 KB |        0.19 |
| NativeSingle      | QuanTAlib.Zlema | 1000  |    26.24 μs |   1.294 μs |  0.856 μs |  1.00 |    0.04 |   5.6152 |       - |   47.42 KB |        1.00 |
| NativeParallel    | QuanTAlib.Zlema | 1000  |    56.25 μs |   2.040 μs |  1.214 μs |  2.15 |    0.08 |  22.9492 |  0.4883 |  187.96 KB |        3.96 |
|                   |                 |       |             |            |           |       |         |          |         |            |             |
| **BuilderFull**       | **QuanTAlib.Zlema** | **10000** | **2,724.50 μs** | **110.421 μs** | **73.036 μs** |  **9.89** |    **0.49** |  **62.5000** | **31.2500** |  **549.68 KB** |        **1.17** |
| BuilderLatestOnly | QuanTAlib.Zlema | 10000 | 2,583.94 μs |  62.411 μs | 41.281 μs |  9.38 |    0.42 |        - |       - |   79.56 KB |        0.17 |
| NativeSingle      | QuanTAlib.Zlema | 10000 |   275.96 μs |  18.321 μs | 12.118 μs |  1.00 |    0.06 |  56.6406 |  9.7656 |  469.31 KB |        1.00 |
| NativeParallel    | QuanTAlib.Zlema | 10000 |   481.77 μs |  12.190 μs |  7.254 μs |  1.75 |    0.08 | 222.6563 | 42.9688 | 1840.33 KB |        3.92 |
