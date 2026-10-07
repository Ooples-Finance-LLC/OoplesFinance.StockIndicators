```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId           | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |----------------- |-------------:|------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetSlope** |  **28,015.2 μs** | **19,046.3 μs** | **1,043.99 μs** |  **1.00** |    **0.05** |  **1000.0000** |         **-** |   **13446.3 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetSlope |     869.2 μs |    585.5 μs |    32.09 μs |  0.03 |    0.00 |          - |         - |    289.41 KB |        0.02 |
|            |       |                  |              |             |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **Skender.GetSlope** | **130,335.0 μs** | **31,123.4 μs** | **1,705.98 μs** |  **1.00** |    **0.02** | **16000.0000** | **1000.0000** | **134832.13 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetSlope |   1,689.1 μs |  2,276.0 μs |   124.76 μs |  0.01 |    0.00 |          - |         - |    2827.1 KB |        0.02 |
