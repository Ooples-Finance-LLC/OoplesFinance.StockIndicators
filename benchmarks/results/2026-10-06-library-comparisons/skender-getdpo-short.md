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
| **Ooples**     | **1000**  | **Skender.GetDpo** |   **536.4 μs** |    **593.4 μs** |    **32.52 μs** |  **1.00** |    **0.07** |  **150.63 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetDpo | 1,015.7 μs |    912.3 μs |    50.00 μs |  1.90 |    0.13 |  258.49 KB |        1.72 |
|            |       |                |            |             |             |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetDpo** | **3,570.8 μs** | **27,171.6 μs** | **1,489.36 μs** |  **1.14** |    **0.62** | **1505.46 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetDpo | 2,301.8 μs |  1,388.2 μs |    76.09 μs |  0.73 |    0.28 |  2527.1 KB |        1.68 |
