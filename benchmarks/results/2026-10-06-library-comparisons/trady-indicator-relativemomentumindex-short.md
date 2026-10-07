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
| **Ooples**     | **1000**  | **Trady(...)Index [37]** |  **8.552 ms** | **10.149 ms** | **0.5563 ms** |  **1.00** |    **0.08** |         **-** |         **-** |         **-** |    **2.9 MB** |        **1.00** |
| Competitor | 1000  | Trady(...)Index [37] |  8.463 ms |  3.027 ms | 0.1659 ms |  0.99 |    0.06 |         - |         - |         - |   4.07 MB |        1.40 |
|            |       |                      |           |           |           |       |         |           |           |           |           |             |
| **Ooples**     | **10000** | **Trady(...)Index [37]** | **30.227 ms** | **45.056 ms** | **2.4697 ms** |  **1.00** |    **0.10** | **3000.0000** | **1000.0000** |         **-** |  **30.43 MB** |        **1.00** |
| Competitor | 10000 | Trady(...)Index [37] | 41.569 ms | 35.729 ms | 1.9584 ms |  1.38 |    0.11 | 3000.0000 | 2000.0000 | 1000.0000 |  28.32 MB |        0.93 |
