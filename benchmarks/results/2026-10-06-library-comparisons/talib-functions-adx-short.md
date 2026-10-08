```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean        | Error         | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |-------------------- |------------:|--------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Adx** | **32,269.8 μs** |  **22,195.97 μs** | **1,216.64 μs** | **1.001** |    **0.05** |  **1000.0000** |         **-** |   **12693.9 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Adx |    188.6 μs |      86.27 μs |     4.73 μs | 0.006 |    0.00 |          - |         - |     37.93 KB |       0.003 |
|            |       |                     |             |               |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib.Functions.Adx** | **94,387.3 μs** | **154,436.93 μs** | **8,465.21 μs** | **1.005** |    **0.11** | **15000.0000** | **1000.0000** | **129599.88 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Adx |    372.0 μs |   1,930.82 μs |   105.83 μs | 0.004 |    0.00 |          - |         - |    328.67 KB |       0.003 |
