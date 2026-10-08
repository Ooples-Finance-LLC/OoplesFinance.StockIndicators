```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean       | Error       | StdDev   | Median     | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------- |------ |-------------------- |-----------:|------------:|---------:|-----------:|------:|--------:|----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Tan** | **2,504.1 μs** | **10,341.6 μs** | **566.9 μs** | **2,210.5 μs** |  **1.03** |    **0.27** | **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Tan |   337.0 μs |  6,921.3 μs | 379.4 μs |   119.0 μs |  0.14 |    0.14 |  38.96 KB |        0.13 |
|            |       |                     |            |             |          |            |       |         |           |             |
| **Ooples**     | **10000** | **TaLib.Functions.Tan** | **2,529.0 μs** |  **4,300.4 μs** | **235.7 μs** | **2,482.8 μs** |  **1.01** |    **0.11** | **4111.3 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Tan |   875.4 μs | 12,299.0 μs | 674.2 μs |   499.1 μs |  0.35 |    0.23 |    329 KB |        0.08 |
