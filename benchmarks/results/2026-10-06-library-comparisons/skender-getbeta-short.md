```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId          | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated | Alloc Ratio |
|----------- |------ |---------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Skender.GetBeta** |  **88.306 ms** | **163.304 ms** | **8.9512 ms** |  **1.01** |    **0.12** |  **2000.0000** | **1000.0000** |  **23.67 MB** |        **1.00** |
| Competitor | 1000  | Skender.GetBeta |   3.927 ms |   4.355 ms | 0.2387 ms |  0.04 |    0.00 |          - |         - |   2.66 MB |        0.11 |
|            |       |                 |            |            |           |       |         |            |           |           |             |
| **Ooples**     | **10000** | **Skender.GetBeta** | **305.385 ms** |  **93.607 ms** | **5.1309 ms** |  **1.00** |    **0.02** | **29000.0000** | **1000.0000** | **242.07 MB** |        **1.00** |
| Competitor | 10000 | Skender.GetBeta |  11.533 ms |  66.077 ms | 3.6219 ms |  0.04 |    0.01 |  3000.0000 | 1000.0000 |  26.89 MB |        0.11 |
