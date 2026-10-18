```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Count | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0      | Gen1     | Allocated   | Alloc Ratio |
|------------------ |------ |-------------:|-------------:|-----------:|------:|--------:|----------:|---------:|------------:|------------:|
| **BuilderFull**       | **1000**  |     **82.81 μs** |     **4.270 μs** |   **2.541 μs** |  **0.03** |    **0.00** |    **3.9063** |        **-** |    **59.41 KB** |       **0.012** |
| BuilderLatestOnly | 1000  |     76.99 μs |     1.020 μs |   0.607 μs |  0.03 |    0.00 |    0.9766 |        - |    11.53 KB |       0.002 |
| NativeSingle      | 1000  |  2,518.72 μs |   347.417 μs | 206.742 μs |  1.01 |    0.11 |  609.3750 |  15.6250 |  5031.89 KB |       1.000 |
| NativeParallel    | 1000  |    790.28 μs |    18.223 μs |   9.531 μs |  0.32 |    0.02 |  710.9375 |  70.3125 |  5787.12 KB |       1.150 |
|                   |       |              |              |            |       |         |           |          |             |             |
| **BuilderFull**       | **10000** |    **917.98 μs** |    **12.420 μs** |   **7.391 μs** |  **0.04** |    **0.00** |   **62.5000** |  **23.4375** |   **552.03 KB** |       **0.011** |
| BuilderLatestOnly | 10000 |    963.39 μs |   200.326 μs | 132.503 μs |  0.04 |    0.01 |    7.8125 |        - |     81.9 KB |       0.002 |
| NativeSingle      | 10000 | 24,228.03 μs | 1,126.216 μs | 744.922 μs |  1.00 |    0.04 | 6200.0000 | 200.0000 | 51584.95 KB |       1.000 |
| NativeParallel    | 10000 |  6,504.41 μs |   157.638 μs |  93.808 μs |  0.27 |    0.01 | 6400.0000 | 666.6667 | 52176.96 KB |       1.011 |
