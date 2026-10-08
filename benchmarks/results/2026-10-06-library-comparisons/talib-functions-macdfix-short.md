```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|------------:|------------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)cdFix [23]** |  **28,204.7 μs** | **14,213.9 μs** |   **779.11 μs** | **1.001** |    **0.03** |         **-** |         **-** |  **7953.19 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)cdFix [23] |     251.6 μs |    126.6 μs |     6.94 μs | 0.009 |    0.00 |         - |         - |   119.72 KB |        0.02 |
|            |       |                      |              |             |             |       |         |           |           |             |             |
| **Ooples**     | **10000** | **TaLib(...)cdFix [23]** | **105,398.2 μs** | **46,091.8 μs** | **2,526.45 μs** | **1.000** |    **0.03** | **9000.0000** | **1000.0000** | **82375.78 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)cdFix [23] |     399.5 μs |  1,858.2 μs |   101.85 μs | 0.004 |    0.00 |         - |         - |  1124.98 KB |        0.01 |
