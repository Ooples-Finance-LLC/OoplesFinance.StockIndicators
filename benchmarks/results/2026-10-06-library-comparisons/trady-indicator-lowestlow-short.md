```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)stLow [25]** | **1.886 ms** |  **1.265 ms** | **0.0693 ms** |  **1.00** |    **0.05** |  **369.38 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)stLow [25] | 2.857 ms |  1.354 ms | 0.0742 ms |  1.52 |    0.06 |  834.18 KB |        2.26 |
|            |       |                      |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)stLow [25]** | **3.184 ms** | **18.027 ms** | **0.9881 ms** |  **1.06** |    **0.38** | **4732.78 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)stLow [25] | 8.829 ms | 30.050 ms | 1.6471 ms |  2.93 |    0.84 | 7541.38 KB |        1.59 |
