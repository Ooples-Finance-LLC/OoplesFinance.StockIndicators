```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)WillR [21]** | **2,880.1 μs** | **3,111.6 μs** | **170.56 μs** |  **1.00** |    **0.07** | **584.13 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)WillR [21] |   143.3 μs |   124.1 μs |   6.80 μs |  0.05 |    0.00 |  21.55 KB |        0.04 |
|            |       |                      |            |            |           |       |         |           |             |
| **Ooples**     | **10000** | **TaLib(...)WillR [21]** | **6,365.8 μs** | **9,080.6 μs** | **497.74 μs** |  **1.00** |    **0.09** |   **6891 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)WillR [21] |   197.9 μs |   371.5 μs |  20.36 μs |  0.03 |    0.00 | 162.51 KB |        0.02 |
