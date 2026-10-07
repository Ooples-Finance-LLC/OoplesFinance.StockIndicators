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
| **Ooples**     | **1000**  | **Trady(...)ttern [41]** | **2.050 ms** |  **0.2583 ms** | **0.0142 ms** | **2.046 ms** |  **1.00** |    **0.01** |  **264.53 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)ttern [41] | 2.647 ms |  0.2986 ms | 0.0164 ms | 2.643 ms |  1.29 |    0.01 |  927.46 KB |        3.51 |
|            |       |                      |          |            |           |          |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)ttern [41]** | **5.416 ms** | **80.6562 ms** | **4.4210 ms** | **2.915 ms** |  **1.43** |    **1.31** | **3711.91 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)ttern [41] | 7.989 ms | 17.2139 ms | 0.9436 ms | 7.577 ms |  2.11 |    1.04 | 7806.57 KB |        2.10 |
