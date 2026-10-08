```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1  
IterationCount=3  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean     | Error    | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |---------:|---------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)iStar [34]** | **3.472 ms** | **1.923 ms** | **0.1054 ms** |  **1.00** |    **0.04** |  **270.33 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)iStar [34] | 1.639 ms | 2.339 ms | 0.1282 ms |  0.47 |    0.03 |   803.5 KB |        2.97 |
|            |       |                      |          |          |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)iStar [34]** | **5.189 ms** | **3.593 ms** | **0.1970 ms** |  **1.00** |    **0.05** | **3719.02 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)iStar [34] | 5.849 ms | 8.373 ms | 0.4590 ms |  1.13 |    0.08 | 5891.55 KB |        1.58 |
