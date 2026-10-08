```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetWilliamsR** | **2,719.1 μs** |    **674.0 μs** |    **36.95 μs** |  **1.00** |    **0.02** |  **584.13 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetWilliamsR |   607.2 μs |    295.9 μs |    16.22 μs |  0.22 |    0.01 |  229.51 KB |        0.39 |
|            |       |                      |            |             |             |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetWilliamsR** | **9,902.9 μs** | **24,407.5 μs** | **1,337.86 μs** |  **1.01** |    **0.17** | **6891.38 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetWilliamsR | 2,190.7 μs |    781.2 μs |    42.82 μs |  0.22 |    0.03 |  2233.7 KB |        0.32 |
