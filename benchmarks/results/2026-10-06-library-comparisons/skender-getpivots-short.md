```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId            | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |------------------ |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetPivots** | **1.213 ms** |  **1.725 ms** | **0.0945 ms** |  **1.00** |    **0.09** |  **628.04 KB** |        **1.00** |
| Competitor | 1000  | Skender.GetPivots | 1.435 ms |  1.116 ms | 0.0612 ms |  1.19 |    0.09 |  409.13 KB |        0.65 |
|            |       |                   |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Skender.GetPivots** | **8.881 ms** | **15.129 ms** | **0.8293 ms** |  **1.01** |    **0.11** | **6421.77 KB** |        **1.00** |
| Competitor | 10000 | Skender.GetPivots | 4.027 ms |  2.453 ms | 0.1345 ms |  0.46 |    0.04 | 4012.92 KB |        0.62 |
