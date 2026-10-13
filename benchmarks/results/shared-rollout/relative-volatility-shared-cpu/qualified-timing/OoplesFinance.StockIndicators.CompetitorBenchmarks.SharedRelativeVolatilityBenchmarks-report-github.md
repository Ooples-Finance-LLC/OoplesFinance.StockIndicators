```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation     | Count | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Allocated   | Alloc Ratio |
|------------------ |-------------- |------ |-----------:|----------:|----------:|------:|--------:|----------:|---------:|------------:|------------:|
| **BuilderFull**       | **QuanTAlib.Rvi** | **1000**  |   **927.9 μs** |  **10.84 μs** |   **7.17 μs** |  **1.06** |    **0.04** |         **-** |        **-** |    **62.79 KB** |        **0.06** |
| BuilderLatestOnly | QuanTAlib.Rvi | 1000  | 1,014.9 μs | 152.07 μs | 100.59 μs |  1.16 |    0.12 |         - |        - |    14.97 KB |        0.01 |
| NativeSingle      | QuanTAlib.Rvi | 1000  |   875.1 μs |  46.05 μs |  30.46 μs |  1.00 |    0.05 |  117.1875 |        - |  1002.95 KB |        1.00 |
| NativeParallel    | QuanTAlib.Rvi | 1000  |   276.4 μs |   5.91 μs |   3.52 μs |  0.32 |    0.01 |  140.6250 |   9.7656 |   1151.7 KB |        1.15 |
|                   |               |       |            |           |           |       |         |           |          |             |             |
| **BuilderFull**       | **QuanTAlib.Rvi** | **10000** | **1,814.5 μs** |  **14.72 μs** |   **9.74 μs** |  **0.21** |    **0.00** |   **62.5000** |  **31.2500** |   **598.87 KB** |        **0.06** |
| BuilderLatestOnly | QuanTAlib.Rvi | 10000 | 1,817.1 μs |  58.65 μs |  38.79 μs |  0.21 |    0.00 |   15.6250 |        - |   128.75 KB |        0.01 |
| NativeSingle      | QuanTAlib.Rvi | 10000 | 8,767.9 μs | 185.02 μs | 110.10 μs |  1.00 |    0.02 | 1181.8182 | 181.8182 | 10083.91 KB |        1.00 |
| NativeParallel    | QuanTAlib.Rvi | 10000 | 2,366.2 μs | 342.73 μs | 226.70 μs |  0.27 |    0.02 | 1250.0000 | 250.0000 | 10229.44 KB |        1.01 |
