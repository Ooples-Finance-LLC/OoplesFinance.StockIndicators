```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------ |------ |------------:|-----------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **BuilderFull**       | **1000**  |   **105.85 μs** |   **6.745 μs** |   **4.461 μs** |  **5.27** |    **0.24** |   **9.7656** |        **-** |   **91.76 KB** |        **1.94** |
| BuilderLatestOnly | 1000  |    96.34 μs |   1.750 μs |   1.157 μs |  4.79 |    0.12 |   4.8828 |        - |   43.88 KB |        0.93 |
| NativeSingle      | 1000  |    20.10 μs |   0.790 μs |   0.470 μs |  1.00 |    0.03 |   5.7373 |   0.1221 |    47.2 KB |        1.00 |
| NativeParallel    | 1000  |    36.76 μs |   1.393 μs |   0.922 μs |  1.83 |    0.06 |  23.1934 |   0.7324 |  186.15 KB |        3.94 |
|                   |       |             |            |            |       |         |          |          |            |             |
| **BuilderFull**       | **10000** | **4,540.46 μs** | **853.940 μs** | **564.829 μs** | **20.71** |    **3.13** | **593.7500** | **468.7500** | **5102.81 KB** |       **10.88** |
| BuilderLatestOnly | 10000 | 4,152.24 μs | 262.629 μs | 173.713 μs | 18.94 |    1.91 | 562.5000 |  93.7500 | 4632.72 KB |        9.88 |
| NativeSingle      | 10000 |   221.19 μs |  34.338 μs |  22.712 μs |  1.01 |    0.14 |  56.6406 |  10.7422 |  469.08 KB |        1.00 |
| NativeParallel    | 10000 |   304.37 μs |  39.195 μs |  25.925 μs |  1.39 |    0.17 | 224.6094 |  44.9219 | 1838.51 KB |        3.92 |
