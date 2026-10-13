```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation      | Count | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------ |--------------- |------ |------------:|----------:|----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **BuilderFull**       | **QuanTAlib.Rema** | **1000**  |   **417.77 μs** | **13.862 μs** |  **9.169 μs** | **15.84** |    **0.48** |   **3.9063** |       **-** |   **56.71 KB** |        **1.20** |
| BuilderLatestOnly | QuanTAlib.Rema | 1000  |   410.23 μs |  7.175 μs |  4.270 μs | 15.56 |    0.37 |        - |       - |    8.88 KB |        0.19 |
| NativeSingle      | QuanTAlib.Rema | 1000  |    26.38 μs |  1.029 μs |  0.612 μs |  1.00 |    0.03 |   5.6152 |       - |   47.22 KB |        1.00 |
| NativeParallel    | QuanTAlib.Rema | 1000  |    49.00 μs |  1.882 μs |  0.984 μs |  1.86 |    0.05 |  23.1934 |  0.7324 |  186.34 KB |        3.95 |
|                   |                |       |             |           |           |       |         |          |         |            |             |
| **BuilderFull**       | **QuanTAlib.Rema** | **10000** | **4,146.92 μs** | **67.887 μs** | **44.903 μs** | **16.01** |    **0.33** |  **62.5000** |       **-** |  **549.53 KB** |        **1.17** |
| BuilderLatestOnly | QuanTAlib.Rema | 10000 | 4,085.35 μs | 61.547 μs | 32.190 μs | 15.77 |    0.31 |        - |       - |   79.43 KB |        0.17 |
| NativeSingle      | QuanTAlib.Rema | 10000 |   259.08 μs |  9.451 μs |  4.943 μs |  1.00 |    0.03 |  56.6406 |  9.7656 |  469.11 KB |        1.00 |
| NativeParallel    | QuanTAlib.Rema | 10000 |   400.49 μs |  5.605 μs |  3.707 μs |  1.55 |    0.03 | 222.6563 | 42.9688 | 1838.71 KB |        3.92 |
