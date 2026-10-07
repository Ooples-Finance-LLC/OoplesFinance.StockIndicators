```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)owest [32]** |  **2.292 ms** |  **2.297 ms** | **0.1259 ms** |  **1.00** |    **0.07** |  **369.41 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)owest [32] |  2.579 ms |  2.121 ms | 0.1163 ms |  1.13 |    0.07 |  900.93 KB |        2.44 |
|            |       |                      |           |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)owest [32]** | **35.809 ms** | **80.269 ms** | **4.3998 ms** |  **1.01** |    **0.15** | **4732.82 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)owest [32] |  8.287 ms | 35.849 ms | 1.9650 ms |  0.23 |    0.05 | 8171.01 KB |        1.73 |
