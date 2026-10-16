```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------ |--------------- |------ |------------:|-----------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **BuilderFull**       | **QuanTAlib.Hwma** | **1000**  |   **311.73 μs** |  **14.870 μs** |   **8.849 μs** | **10.92** |    **0.33** |        **-** |       **-** |   **56.84 KB** |        **1.20** |
| BuilderLatestOnly | QuanTAlib.Hwma | 1000  |   299.59 μs |   7.109 μs |   4.702 μs | 10.50 |    0.22 |        - |       - |    8.97 KB |        0.19 |
| NativeSingle      | QuanTAlib.Hwma | 1000  |    28.54 μs |   0.661 μs |   0.437 μs |  1.00 |    0.02 |   5.6152 |       - |   47.24 KB |        1.00 |
| NativeParallel    | QuanTAlib.Hwma | 1000  |    43.51 μs |   1.338 μs |   0.885 μs |  1.52 |    0.04 |  22.9492 |  0.4883 |  186.53 KB |        3.95 |
|                   |                |       |             |            |            |       |         |          |         |            |             |
| **BuilderFull**       | **QuanTAlib.Hwma** | **10000** | **3,240.86 μs** | **422.534 μs** | **279.480 μs** | **11.80** |    **0.98** |  **62.5000** | **31.2500** |  **549.53 KB** |        **1.17** |
| BuilderLatestOnly | QuanTAlib.Hwma | 10000 | 2,849.97 μs |  18.602 μs |  12.304 μs | 10.37 |    0.11 |        - |       - |   79.39 KB |        0.17 |
| NativeSingle      | QuanTAlib.Hwma | 10000 |   274.79 μs |   5.321 μs |   2.783 μs |  1.00 |    0.01 |  56.6406 |  9.7656 |  469.13 KB |        1.00 |
| NativeParallel    | QuanTAlib.Hwma | 10000 |   363.12 μs |   8.337 μs |   4.961 μs |  1.32 |    0.02 | 224.6094 | 44.9219 | 1838.88 KB |        3.92 |
