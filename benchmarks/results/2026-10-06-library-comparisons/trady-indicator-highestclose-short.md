```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error      | StdDev    | Median   | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|-----------:|----------:|---------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Close [28]** | **1.922 ms** |  **0.4693 ms** | **0.0257 ms** | **1.912 ms** |  **1.00** |    **0.02** |  **369.38 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Close [28] | 2.815 ms |  0.3948 ms | 0.0216 ms | 2.811 ms |  1.46 |    0.02 |   833.2 KB |        2.26 |
|            |       |                      |          |            |           |          |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)Close [28]** | **4.523 ms** | **61.0601 ms** | **3.3469 ms** | **2.848 ms** |  **1.35** |    **1.13** | **4732.18 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Close [28] | 8.854 ms | 30.5737 ms | 1.6758 ms | 8.320 ms |  2.65 |    1.32 | 7541.38 KB |        1.59 |
