```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Range [25]** | **1.848 ms** |  **2.3760 ms** | **0.1302 ms** |  **1.00** |    **0.09** |  **299.04 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Range [25] | 2.182 ms |  0.4434 ms | 0.0243 ms |  1.18 |    0.07 |  939.91 KB |        3.14 |
|            |       |                      |          |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)Range [25]** | **9.741 ms** |  **4.9927 ms** | **0.2737 ms** |  **1.00** |    **0.03** | **4029.63 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Range [25] | 6.824 ms | 17.0959 ms | 0.9371 ms |  0.70 |    0.09 | 8564.17 KB |        2.13 |
