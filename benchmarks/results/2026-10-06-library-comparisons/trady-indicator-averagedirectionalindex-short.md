```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error      | StdDev   | Ratio | RatioSD | Gen0       | Gen1      | Gen2      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |---------:|-----------:|---------:|------:|--------:|-----------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Index [39]** | **27.78 ms** |  **15.729 ms** | **0.862 ms** |  **1.00** |    **0.04** |  **1000.0000** |         **-** |         **-** |   **9.79 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)Index [39] | 16.25 ms |  18.882 ms | 1.035 ms |  0.59 |    0.04 |  1000.0000 |         - |         - |   8.73 MB |        0.89 |
|            |       |                      |          |            |          |       |         |            |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)Index [39]** | **70.39 ms** |   **1.428 ms** | **0.078 ms** |  **1.00** |    **0.00** | **12000.0000** | **1000.0000** |         **-** | **100.05 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)Index [39] | 69.46 ms | 108.456 ms | 5.945 ms |  0.99 |    0.07 |  5000.0000 | 2000.0000 | 1000.0000 |  62.87 MB |        0.63 |
