```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Trend [25]** | **2.066 ms** |  **0.5569 ms** | **0.0305 ms** |  **1.00** |    **0.02** |  **264.53 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Trend [25] | 1.971 ms |  2.2203 ms | 0.1217 ms |  0.95 |    0.05 |  579.63 KB |        2.19 |
|            |       |                      |          |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)Trend [25]** | **6.686 ms** | **76.6665 ms** | **4.2023 ms** |  **1.52** |    **1.51** | **3713.88 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Trend [25] | 5.074 ms |  2.8320 ms | 0.1552 ms |  1.15 |    0.85 | 5040.47 KB |        1.36 |
