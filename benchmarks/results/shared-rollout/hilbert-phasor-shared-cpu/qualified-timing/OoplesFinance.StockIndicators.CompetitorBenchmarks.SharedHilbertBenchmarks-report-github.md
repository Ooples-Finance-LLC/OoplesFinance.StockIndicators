```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9606)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  MinIterationTime=100ms  Toolchain=InProcessEmitToolchain
IterationCount=10  LaunchCount=1  UnrollFactor=1
WarmupCount=10

```
| Method            | Operation            | Count | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|------------------ |--------------------- |------ |-------------:|-----------:|-----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| **BuilderFull**       | **TaLib(...)hasor [24]** | **1000**  |  **1,250.40 μs** |  **24.842 μs** |  **16.431 μs** | **13.62** |    **0.20** |   **7.8125** |        **-** |        **-** |  **82.42 KB** |        **5.30** |
| BuilderLatestOnly | TaLib(...)hasor [24] | 1000  |  1,242.69 μs |  37.845 μs |  19.794 μs | 13.54 |    0.22 |        - |        - |        - |  34.86 KB |        2.24 |
| NativeSingle      | TaLib(...)hasor [24] | 1000  |     91.81 μs |   1.022 μs |   0.676 μs |  1.00 |    0.01 |   1.4648 |        - |        - |  15.55 KB |        1.00 |
| NativeParallel    | TaLib(...)hasor [24] | 1000  |    114.29 μs |   1.805 μs |   1.074 μs |  1.24 |    0.01 |  14.6484 |        - |        - |  86.75 KB |        5.58 |
|                   |                      |       |              |            |            |       |         |          |          |          |           |             |
| **BuilderFull**       | **TaLib(...)hasor [24]** | **10000** | **12,231.85 μs** | **281.479 μs** | **147.219 μs** | **12.89** |    **0.17** |        **-** |        **-** |        **-** | **786.88 KB** |        **5.01** |
| BuilderLatestOnly | TaLib(...)hasor [24] | 10000 | 12,342.77 μs | 436.311 μs | 228.199 μs | 13.00 |    0.24 |        - |        - |        - | 316.99 KB |        2.02 |
| NativeSingle      | TaLib(...)hasor [24] | 10000 |    949.32 μs |   9.154 μs |   6.055 μs |  1.00 |    0.01 |  46.8750 |  46.8750 |  46.8750 | 156.96 KB |        1.00 |
| NativeParallel    | TaLib(...)hasor [24] | 10000 |  1,151.16 μs |  31.360 μs |  20.743 μs |  1.21 |    0.02 | 218.7500 | 203.1250 | 195.3125 | 866.88 KB |        5.52 |
