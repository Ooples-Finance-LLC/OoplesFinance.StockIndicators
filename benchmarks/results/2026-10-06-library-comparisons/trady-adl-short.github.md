```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)nLine [44]** |  **6.820 ms** |  **9.7521 ms** | **0.5345 ms** |  **1.00** |    **0.10** |  **774.09 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)nLine [44] |  3.168 ms |  0.4317 ms | 0.0237 ms |  0.47 |    0.03 |  963.99 KB |        1.25 |
|            |       |                      |           |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)nLine [44]** | **17.984 ms** | **29.5968 ms** | **1.6223 ms** |  **1.01** |    **0.11** |  **9724.7 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)nLine [44] | 12.868 ms | 54.0622 ms | 2.9633 ms |  0.72 |    0.15 | 8808.31 KB |        0.91 |
