```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean       | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |-----------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetCmo** | **5,541.2 μs** | **10,155.8 μs** |   **556.67 μs** |  **1.01** |    **0.12** |  **594.95 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetCmo |   565.8 μs |    133.8 μs |     7.34 μs |  0.10 |    0.01 |  129.36 KB |        0.22 |
|            |       |                |            |             |             |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetCmo** | **8,817.7 μs** | **40,700.1 μs** | **2,230.91 μs** |  **1.05** |    **0.34** | **7036.38 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetCmo | 1,643.6 μs |  3,473.8 μs |   190.41 μs |  0.20 |    0.05 | 1227.34 KB |        0.17 |
