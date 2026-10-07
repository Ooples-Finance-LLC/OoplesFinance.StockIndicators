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
| **Ooples**     | **1000**  | **Skend(...)oints [22]** | **1.106 ms** |  **0.1987 ms** | **0.0109 ms** |  **1.00** |    **0.01** |  **330.98 KB** |        **1.00** |
| Competitor | 1000  | Skend(...)oints [22] | 1.561 ms |  0.8958 ms | 0.0491 ms |  1.41 |    0.04 |  510.87 KB |        1.54 |
|            |       |                      |          |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Skend(...)oints [22]** | **2.820 ms** | **13.6033 ms** | **0.7456 ms** |  **1.05** |    **0.36** | **3317.63 KB** |        **1.00** |
| Competitor | 10000 | Skend(...)oints [22] | 3.801 ms | 16.2219 ms | 0.8892 ms |  1.42 |    0.46 | 5019.66 KB |        1.51 |
