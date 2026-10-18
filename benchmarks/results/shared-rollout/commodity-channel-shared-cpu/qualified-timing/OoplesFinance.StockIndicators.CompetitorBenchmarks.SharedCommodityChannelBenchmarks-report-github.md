```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation           | Count | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------ |-------------------- |------ |-------------:|----------:|----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **BuilderFull**       | **TaLib.Functions.Cci** | **1000**  |   **167.989 μs** | **3.6803 μs** | **2.1901 μs** |  **5.48** |    **0.13** |  **7.8125** |       **-** |  **68.69 KB** |        **8.89** |
| BuilderLatestOnly | TaLib.Functions.Cci | 1000  |   160.235 μs | 1.4775 μs | 0.9773 μs |  5.23 |    0.11 |  1.9531 |       - |  21.08 KB |        2.73 |
| NativeSingle      | TaLib.Functions.Cci | 1000  |    30.663 μs | 1.0212 μs | 0.6755 μs |  1.00 |    0.03 |  0.7324 |       - |   7.73 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Cci | 1000  |     7.533 μs | 0.1763 μs | 0.1166 μs |  0.25 |    0.01 |  0.9766 |       - |   8.16 KB |        1.06 |
|                   |                     |       |              |           |           |       |         |         |         |           |             |
| **BuilderFull**       | **TaLib.Functions.Cci** | **10000** | **1,504.961 μs** | **7.1792 μs** | **4.7486 μs** |  **4.84** |    **0.07** | **70.3125** | **62.5000** | **631.62 KB** |        **8.09** |
| BuilderLatestOnly | TaLib.Functions.Cci | 10000 | 1,478.865 μs | 8.4387 μs | 5.5817 μs |  4.75 |    0.07 | 15.6250 |       - | 161.76 KB |        2.07 |
| NativeSingle      | TaLib.Functions.Cci | 10000 |   311.265 μs | 6.6108 μs | 4.3726 μs |  1.00 |    0.02 |  7.8125 |       - |  78.05 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Cci | 10000 |    56.948 μs | 0.7610 μs | 0.4529 μs |  0.18 |    0.00 |  9.2773 |  1.4648 |  78.47 KB |        1.01 |
