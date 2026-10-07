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
| **Ooples**     | **1000**  | **TaLib(...)iLine [24]** |  **2,291.5 μs** |  **1,460.8 μs** |    **80.07 μs** |  **1.00** |    **0.04** |  **265.12 KB** |        **1.00** |
| Competitor | 1000  | TaLib(...)iLine [24] |  1,031.8 μs |    727.9 μs |    39.90 μs |  0.45 |    0.02 |   15.97 KB |        0.06 |
|            |       |                      |             |             |             |       |         |            |             |
| **Ooples**     | **10000** | **TaLib(...)iLine [24]** | **13,327.3 μs** | **29,994.1 μs** | **1,644.08 μs** |  **1.01** |    **0.16** | **3712.54 KB** |        **1.00** |
| Competitor | 10000 | TaLib(...)iLine [24] |    755.9 μs |    339.9 μs |    18.63 μs |  0.06 |    0.01 |  122.75 KB |        0.03 |
