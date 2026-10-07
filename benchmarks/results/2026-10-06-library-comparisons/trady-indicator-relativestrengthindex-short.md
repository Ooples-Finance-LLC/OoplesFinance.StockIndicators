```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Index [37]** |  **8.464 ms** | **12.344 ms** | **0.6766 ms** |  **1.00** |    **0.10** |         **-** |         **-** |         **-** |   **2.73 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)Index [37] |  7.665 ms |  1.183 ms | 0.0649 ms |  0.91 |    0.07 |         - |         - |         - |   4.15 MB |        1.52 |
|            |       |                      |           |           |           |       |         |           |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)Index [37]** | **30.374 ms** | **35.817 ms** | **1.9632 ms** |  **1.00** |    **0.08** | **3000.0000** | **1000.0000** |         **-** |  **28.63 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)Index [37] | 38.543 ms | 65.982 ms | 3.6167 ms |  1.27 |    0.13 | 3000.0000 | 2000.0000 | 1000.0000 |  29.01 MB |        1.01 |
