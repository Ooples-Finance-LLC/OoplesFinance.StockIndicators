```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |---------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)ttern [41]** | **2.024 ms** |  **0.8462 ms** | **0.0464 ms** |  **1.00** |    **0.03** | **264.53 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)ttern [41] | 2.803 ms |  2.1438 ms | 0.1175 ms |  1.39 |    0.06 | 925.82 KB |        3.50 |
|            |       |                      |          |            |           |       |         |           |             |
| **Ooples**     | **10000** | **Trady(...)ttern [41]** | **5.562 ms** | **76.0759 ms** | **4.1700 ms** |  **1.52** |    **1.53** | **3714.2 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)ttern [41] | 7.396 ms | 12.0548 ms | 0.6608 ms |  2.02 |    1.34 | 7809.8 KB |        2.10 |
