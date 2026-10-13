```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------ |------ |----------:|----------:|----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **BuilderFull**       | **1000**  |  **80.27 μs** |  **1.693 μs** |  **1.120 μs** |  **0.94** |    **0.01** |  **6.8359** |  **0.9766** |  **56.92 KB** |       **14.33** |
| BuilderLatestOnly | 1000  |  80.80 μs | 11.844 μs |  7.048 μs |  0.95 |    0.08 |  0.9766 |       - |   9.04 KB |        2.27 |
| NativeSingle      | 1000  |  85.11 μs |  1.182 μs |  0.618 μs |  1.00 |    0.01 |       - |       - |   3.97 KB |        1.00 |
| NativeParallel    | 1000  |  23.70 μs |  1.142 μs |  0.756 μs |  0.28 |    0.01 |  1.0986 |       - |    8.8 KB |        2.21 |
|                   |       |           |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **10000** | **786.46 μs** | **15.953 μs** | **10.552 μs** |  **0.92** |    **0.02** | **62.5000** | **46.8750** | **549.53 KB** |       **14.02** |
| BuilderLatestOnly | 10000 | 783.12 μs | 38.217 μs | 25.278 μs |  0.91 |    0.03 |  7.8125 |       - |  79.38 KB |        2.03 |
| NativeSingle      | 10000 | 856.48 μs | 17.651 μs | 10.504 μs |  1.00 |    0.02 |       - |       - |  39.18 KB |        1.00 |
| NativeParallel    | 10000 | 212.30 μs |  2.022 μs |  1.338 μs |  0.25 |    0.00 | 11.7188 |  1.9531 |  79.09 KB |        2.02 |
