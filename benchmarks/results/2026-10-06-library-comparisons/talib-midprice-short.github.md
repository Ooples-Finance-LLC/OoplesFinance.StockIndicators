```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |------------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib(...)Price [24]** |  **2,429.2 μs** |    **577.3 μs** |    **31.64 μs** |  **1.00** |    **0.02** |  **374.61 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)Price [24] |    196.7 μs |    119.0 μs |     6.52 μs |  0.08 |    0.00 |   33.48 KB |        0.09 |
|            |       |                      |             |             |             |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)Price [24]** | **11,517.4 μs** | **79,319.0 μs** | **4,347.74 μs** |  **1.14** |    **0.62** | **4806.08 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)Price [24] |  1,148.4 μs |  5,452.8 μs |   298.89 μs |  0.11 |    0.05 |  279.29 KB |        0.06 |
