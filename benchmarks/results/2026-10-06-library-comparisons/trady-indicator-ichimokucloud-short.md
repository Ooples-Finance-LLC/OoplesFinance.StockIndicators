```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Cloud [29]** |  **1.904 ms** |   **3.247 ms** | **0.1780 ms** |  **1.01** |    **0.11** |         **-** |         **-** |         **-** |   **747.31 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Cloud [29] | 14.327 ms |   4.177 ms | 0.2289 ms |  7.57 |    0.59 |         - |         - |         - |  5137.98 KB |        6.88 |
|            |       |                      |           |            |           |       |         |           |           |           |             |             |
| **Ooples**     | **10000** | **Trady(...)Cloud [29]** |  **6.977 ms** |  **11.366 ms** | **0.6230 ms** |  **1.01** |    **0.11** |         **-** |         **-** |         **-** |  **7332.01 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Cloud [29] | 51.048 ms | 148.207 ms | 8.1238 ms |  7.35 |    1.15 | 3000.0000 | 2000.0000 | 1000.0000 | 36870.98 KB |        5.03 |
