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
| **BuilderFull**       | **TaLib.Functions.Mama** | **1000**  |  **1,289.90 μs** |  **21.047 μs** |  **13.921 μs** | **13.91** |    **0.19** |   **7.8125** |        **-** |        **-** |  **82.61 KB** |        **5.31** |
| BuilderLatestOnly | TaLib.Functions.Mama | 1000  |  1,291.56 μs |  26.572 μs |  15.813 μs | 13.93 |    0.20 |        - |        - |        - |  35.06 KB |        2.25 |
| NativeSingle      | TaLib.Functions.Mama | 1000  |     92.72 μs |   1.294 μs |   0.856 μs |  1.00 |    0.01 |   1.4648 |        - |        - |  15.55 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mama | 1000  |    128.40 μs |   2.248 μs |   1.487 μs |  1.38 |    0.02 |  14.6484 |        - |        - |  86.75 KB |        5.58 |
|                   |                      |       |              |            |            |       |         |          |          |          |           |             |
| **BuilderFull**       | **TaLib.Functions.Mama** | **10000** | **12,791.60 μs** | **615.546 μs** | **407.145 μs** | **12.60** |    **0.41** |        **-** |        **-** |        **-** |    **787 KB** |        **5.03** |
| BuilderLatestOnly | TaLib.Functions.Mama | 10000 | 12,724.50 μs | 417.170 μs | 218.188 μs | 12.53 |    0.25 |        - |        - |        - | 317.18 KB |        2.03 |
| NativeSingle      | TaLib.Functions.Mama | 10000 |  1,015.56 μs |  17.744 μs |  11.737 μs |  1.00 |    0.02 |  46.8750 |  46.8750 |  46.8750 |  156.6 KB |        1.00 |
| NativeParallel    | TaLib.Functions.Mama | 10000 |  1,295.45 μs |  18.713 μs |  11.136 μs |  1.28 |    0.02 | 218.7500 | 203.1250 | 195.3125 | 866.68 KB |        5.53 |
