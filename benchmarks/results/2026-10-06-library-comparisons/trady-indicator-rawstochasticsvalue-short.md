```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Value [35]** |  **4.004 ms** |  **9.766 ms** | **0.5353 ms** |  **1.01** |    **0.16** |         **-** |   **695.76 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Value [35] |  5.285 ms |  3.032 ms | 0.1662 ms |  1.34 |    0.15 |         - |   1732.8 KB |        2.49 |
|            |       |                      |           |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **Trady(...)Value [35]** | **11.294 ms** | **36.857 ms** | **2.0203 ms** |  **1.02** |    **0.21** |         **-** |  **8059.69 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Value [35] | 18.648 ms | 58.413 ms | 3.2018 ms |  1.68 |    0.35 | 1000.0000 | 13803.85 KB |        1.71 |
