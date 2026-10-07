```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0       | Gen1      | Gen2      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |---------:|---------:|---------:|------:|--------:|-----------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Index [40]** | **27.41 ms** | **20.41 ms** | **1.119 ms** |  **1.00** |    **0.05** |  **1000.0000** |         **-** |         **-** |   **9.79 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)Index [40] | 13.96 ms | 22.98 ms | 1.260 ms |  0.51 |    0.04 |          - |         - |         - |   7.69 MB |        0.79 |
|            |       |                      |          |          |          |       |         |            |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)Index [40]** | **69.12 ms** | **31.17 ms** | **1.708 ms** |  **1.00** |    **0.03** | **12000.0000** | **1000.0000** |         **-** | **100.04 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)Index [40] | 56.58 ms | 62.22 ms | 3.411 ms |  0.82 |    0.05 |  5000.0000 | 2000.0000 | 1000.0000 |   55.7 MB |        0.56 |
