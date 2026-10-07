```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error    | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|---------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)ghest [23]** |  **1.996 ms** | **1.259 ms** | **0.0690 ms** |  **1.00** |    **0.04** |  **369.38 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)ghest [23] |  2.931 ms | 2.255 ms | 0.1236 ms |  1.47 |    0.07 |  834.18 KB |        2.26 |
|            |       |                      |           |          |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)ghest [23]** | **10.541 ms** | **4.998 ms** | **0.2740 ms** |  **1.00** |    **0.03** | **4732.84 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)ghest [23] |  8.812 ms | 7.124 ms | 0.3905 ms |  0.84 |    0.04 | 7543.68 KB |        1.59 |
