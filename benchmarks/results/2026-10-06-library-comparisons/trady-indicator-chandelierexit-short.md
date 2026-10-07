```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)rExit [30]** |  **8.649 ms** |   **9.749 ms** | **0.5344 ms** |  **1.00** |    **0.07** |         **-** |         **-** |         **-** |   **1.97 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)rExit [30] |  8.028 ms |   2.736 ms | 0.1500 ms |  0.93 |    0.05 |         - |         - |         - |   3.45 MB |        1.75 |
|            |       |                      |           |            |           |       |         |           |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)rExit [30]** | **23.466 ms** |   **6.169 ms** | **0.3381 ms** |  **1.00** |    **0.02** | **2000.0000** | **1000.0000** |         **-** |  **20.28 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)rExit [30] | 31.991 ms | 153.161 ms | 8.3953 ms |  1.36 |    0.31 | 3000.0000 | 2000.0000 | 1000.0000 |  28.87 MB |        1.42 |
