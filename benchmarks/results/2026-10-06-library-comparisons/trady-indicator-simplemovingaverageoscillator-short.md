```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)lator [45]** |  **2.854 ms** |  **8.551 ms** | **0.4687 ms** |  **1.02** |    **0.20** |         **-** |  **306.26 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)lator [45] |  5.192 ms |  1.825 ms | 0.1000 ms |  1.85 |    0.24 |         - | 1700.65 KB |        5.55 |
|            |       |                      |           |           |           |       |         |           |            |             |
| **Ooples**     | **10000** | **Trady(...)lator [45]** |  **4.784 ms** | **17.592 ms** | **0.9643 ms** |  **1.03** |    **0.27** |         **-** | **4115.95 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)lator [45] | 19.680 ms | 34.027 ms | 1.8651 ms |  4.24 |    0.90 | 1000.0000 | 13486.8 KB |        3.28 |
