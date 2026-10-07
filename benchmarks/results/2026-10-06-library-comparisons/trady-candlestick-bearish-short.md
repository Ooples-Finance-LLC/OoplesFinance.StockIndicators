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
| **Ooples**     | **1000**  | **Trady(...)arish [25]** | **2.093 ms** |  **0.9711 ms** | **0.0532 ms** |  **1.00** |    **0.03** |  **264.52 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)arish [25] | 1.386 ms |  1.7942 ms | 0.0983 ms |  0.66 |    0.04 |  533.13 KB |        2.02 |
|            |       |                      |          |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)arish [25]** | **9.359 ms** | **33.1412 ms** | **1.8166 ms** |  **1.03** |    **0.26** |  **3712.6 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)arish [25] | 3.749 ms |  2.5039 ms | 0.1372 ms |  0.41 |    0.08 | 4572.75 KB |        1.23 |
