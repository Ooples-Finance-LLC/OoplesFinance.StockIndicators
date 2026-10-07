```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|------------:|-----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Index [27]** | **3,074.2 μs** |  **2,494.8 μs** |   **136.75 μs** | **3,078.4 μs** |  **1.00** |    **0.05** |  **390.65 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Index [27] |   209.8 μs |    175.0 μs |     9.59 μs |   211.3 μs |  0.07 |    0.00 |   39.43 KB |        0.10 |
|            |       |                      |            |             |             |            |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)Index [27]** | **5,603.1 μs** | **70,615.5 μs** | **3,870.67 μs** | **3,371.9 μs** |  **1.29** |    **1.00** | **4963.07 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Index [27] |   313.2 μs |    954.9 μs |    52.34 μs |   343.0 μs |  0.07 |    0.03 |  357.15 KB |        0.07 |
