```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean     | Error      | StdDev    | Median   | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|-----------:|----------:|---------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Close [27]** | **1.999 ms** |  **2.2717 ms** | **0.1245 ms** | **1.956 ms** |  **1.00** |    **0.08** |  **369.38 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Close [27] | 2.841 ms |  0.5950 ms | 0.0326 ms | 2.824 ms |  1.42 |    0.08 |   833.8 KB |        2.26 |
|            |       |                      |          |            |           |          |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)Close [27]** | **5.364 ms** | **67.1007 ms** | **3.6780 ms** | **3.267 ms** |  **1.29** |    **0.99** | **4732.13 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Close [27] | 9.236 ms | 28.1484 ms | 1.5429 ms | 8.710 ms |  2.22 |    1.01 | 7544.71 KB |        1.59 |
