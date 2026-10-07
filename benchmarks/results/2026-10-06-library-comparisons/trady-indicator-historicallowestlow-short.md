```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)stLow [35]** |  **2.416 ms** |  **0.8778 ms** | **0.0481 ms** |  **1.00** |    **0.02** |  **369.41 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)stLow [35] |  2.615 ms |  0.8871 ms | 0.0486 ms |  1.08 |    0.03 |  900.65 KB |        2.44 |
|            |       |                      |           |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)stLow [35]** | **31.136 ms** |  **4.7081 ms** | **0.2581 ms** |  **1.00** |    **0.01** | **4732.54 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)stLow [35] |  7.790 ms | 27.3001 ms | 1.4964 ms |  0.25 |    0.04 | 8168.76 KB |        1.73 |
