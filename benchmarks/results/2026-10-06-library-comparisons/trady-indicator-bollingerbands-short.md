```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|----------:|----------:|------:|--------:|-----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Bands [30]** |  **69.799 ms** | **46.332 ms** | **2.5396 ms** |  **1.00** |    **0.04** |  **2000.0000** |         **-** |  **21.89 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)Bands [30] |   9.918 ms |  3.044 ms | 0.1669 ms |  0.14 |    0.00 |          - |         - |   2.23 MB |        0.10 |
|            |       |                      |            |           |           |       |         |            |           |           |             |
| **Ooples**     | **10000** | **Trady(...)Bands [30]** | **307.453 ms** | **18.870 ms** | **1.0343 ms** |  **1.00** |    **0.00** | **27000.0000** | **1000.0000** |  **222.4 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)Bands [30] |  25.065 ms | 68.808 ms | 3.7716 ms |  0.08 |    0.01 |  1000.0000 |         - |   18.7 MB |        0.08 |
