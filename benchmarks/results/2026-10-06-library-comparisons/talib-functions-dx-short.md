```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean         | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |------------------- |-------------:|-------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Dx** |  **33,466.9 μs** |  **19,715.0 μs** | **1,080.65 μs** | **1.001** |    **0.04** |  **1000.0000** |         **-** |  **15000.45 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.Dx |     233.4 μs |     110.0 μs |     6.03 μs | 0.007 |    0.00 |          - |         - |     38.91 KB |       0.003 |
|            |       |                    |              |              |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib.Functions.Dx** | **108,110.4 μs** | **123,579.3 μs** | **6,773.80 μs** | **1.003** |    **0.08** | **18000.0000** | **1000.0000** | **153000.57 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.Dx |     389.4 μs |   1,395.5 μs |    76.49 μs | 0.004 |    0.00 |          - |         - |    328.95 KB |       0.002 |
