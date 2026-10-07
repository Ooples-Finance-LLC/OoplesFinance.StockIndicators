```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **TaLib.Functions.Sqrt** | **2,857.6 μs** | **8,879.77 μs** | **486.73 μs** |  **1.02** |    **0.20** |  **305.16 KB** |        **1.00** |
| Competitor | 1000  | TaLib.Functions.Sqrt |   100.3 μs |    81.76 μs |   4.48 μs |  0.04 |    0.01 |   38.96 KB |        0.13 |
|            |       |                      |            |             |           |       |         |            |             |
| **Ooples**     | **10000** | **TaLib.Functions.Sqrt** | **3,221.1 μs** | **3,238.47 μs** | **177.51 μs** |  **1.00** |    **0.07** | **4111.24 KB** |        **1.00** |
| Competitor | 10000 | TaLib.Functions.Sqrt |   286.7 μs |   407.02 μs |  22.31 μs |  0.09 |    0.01 |  327.36 KB |        0.08 |
