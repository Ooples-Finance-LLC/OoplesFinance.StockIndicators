```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |------------------- |------------:|-------------:|------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.T3** | **21,270.2 μs** | **12,251.13 μs** |   **671.53 μs** | **1.001** |    **0.04** |  **1000.0000** |         **-** |   **9776.73 KB** |       **1.000** |
| Competitor | 1000  | TaLib.Functions.T3 |    110.2 μs |     14.75 μs |     0.81 μs | 0.005 |    0.00 |          - |         - |     38.25 KB |       0.004 |
|            |       |                    |             |              |             |       |         |            |           |              |             |
| **Ooples**     | **10000** | **TaLib.Functions.T3** | **68,062.7 μs** | **54,568.52 μs** | **2,991.08 μs** | **1.001** |    **0.05** | **12000.0000** | **1000.0000** | **106609.66 KB** |       **1.000** |
| Competitor | 10000 | TaLib.Functions.T3 |    212.3 μs |    753.41 μs |    41.30 μs | 0.003 |    0.00 |          - |         - |    328.34 KB |       0.003 |
