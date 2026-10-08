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
| **Ooples**     | **1000**  | **Trady(...)tHigh [37]** |  **2.303 ms** |  **2.071 ms** | **0.1135 ms** |  **1.00** |    **0.06** |  **369.42 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)tHigh [37] |  2.463 ms |  2.357 ms | 0.1292 ms |  1.07 |    0.07 |  899.66 KB |        2.44 |
|            |       |                      |           |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)tHigh [37]** | **31.960 ms** |  **9.516 ms** | **0.5216 ms** |  **1.00** |    **0.02** | **4731.84 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)tHigh [37] |  7.677 ms | 34.230 ms | 1.8763 ms |  0.24 |    0.05 | 8173.63 KB |        1.73 |
