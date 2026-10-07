```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)ation [33]** |  **21.360 ms** | **14.3862 ms** | **0.7886 ms** |  **1.00** |    **0.04** |  **1000.0000** |         **-** |   **8.46 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)ation [33] |   7.668 ms |  0.0253 ms | 0.0014 ms |  0.36 |    0.01 |          - |         - |   1.01 MB |        0.12 |
|            |       |                      |            |            |           |       |         |            |           |           |             |
| **Ooples**     | **10000** | **Trady(...)ation [33]** | **143.892 ms** | **27.0536 ms** | **1.4829 ms** |  **1.00** |    **0.01** | **10000.0000** | **1000.0000** |  **85.89 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)ation [33] |  17.117 ms | 30.4109 ms | 1.6669 ms |  0.12 |    0.01 |          - |         - |   9.35 MB |        0.11 |
