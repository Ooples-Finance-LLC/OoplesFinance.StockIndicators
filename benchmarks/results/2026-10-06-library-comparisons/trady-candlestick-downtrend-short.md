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
| **Ooples**     | **1000**  | **Trady(...)Trend [27]** |  **2.037 ms** | **0.7081 ms** | **0.0388 ms** |  **1.00** |    **0.02** |  **264.53 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Trend [27] |  2.035 ms | 2.1401 ms | 0.1173 ms |  1.00 |    0.05 |  579.91 KB |        2.19 |
|            |       |                      |           |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)Trend [27]** | **11.126 ms** | **4.4756 ms** | **0.2453 ms** |  **1.00** |    **0.03** | **3713.55 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Trend [27] |  5.122 ms | 1.1650 ms | 0.0639 ms |  0.46 |    0.01 | 5040.47 KB |        1.36 |
