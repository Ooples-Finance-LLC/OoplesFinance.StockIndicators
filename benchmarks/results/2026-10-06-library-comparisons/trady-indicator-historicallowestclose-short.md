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
| **Ooples**     | **1000**  | **Trady(...)Close [37]** |  **2.275 ms** |  **0.1983 ms** | **0.0109 ms** |  **1.00** |    **0.01** |  **369.41 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Close [37] |  2.381 ms |  0.8068 ms | 0.0442 ms |  1.05 |    0.02 |  899.95 KB |        2.44 |
|            |       |                      |           |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)Close [37]** | **34.240 ms** | **89.5276 ms** | **4.9073 ms** |  **1.01** |    **0.17** | **4732.82 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Close [37] |  9.052 ms | 10.3037 ms | 0.5648 ms |  0.27 |    0.03 | 8173.68 KB |        1.73 |
