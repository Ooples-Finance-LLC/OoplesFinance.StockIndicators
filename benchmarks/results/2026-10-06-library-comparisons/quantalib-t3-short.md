```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId       | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |------------- |------------:|-------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.T3** | **22,651.3 μs** |  **19,954.9 μs** | **1,093.80 μs** |  **1.00** |    **0.06** |  **1000.0000** |         **-** |  **10595.08 KB** |       **1.000** |
| Competitor | 1000  | QuanTAlib.T3 |    300.8 μs |     141.4 μs |     7.75 μs |  0.01 |    0.00 |          - |         - |     71.38 KB |       0.007 |
|            |       |              |             |              |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **QuanTAlib.T3** | **70,400.7 μs** | **136,779.4 μs** | **7,497.34 μs** |  **1.01** |    **0.13** | **12000.0000** | **1000.0000** | **107427.73 KB** |       **1.000** |
| Competitor | 10000 | QuanTAlib.T3 |    932.2 μs |   3,200.8 μs |   175.45 μs |  0.01 |    0.00 |          - |         - |    643.34 KB |       0.006 |
