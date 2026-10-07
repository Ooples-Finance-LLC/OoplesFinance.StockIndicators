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
| **Ooples**     | **1000**  | **Trady(...)iStar [33]** | **3.431 ms** |  **2.1381 ms** | **0.1172 ms** |  **1.00** |    **0.04** |  **270.33 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)iStar [33] | 3.387 ms |  2.8389 ms | 0.1556 ms |  0.99 |    0.05 | 1149.36 KB |        4.25 |
|            |       |                      |          |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)iStar [33]** | **5.258 ms** |  **0.3801 ms** | **0.0208 ms** |  **1.00** |    **0.00** | **3720.05 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)iStar [33] | 9.167 ms | 10.6836 ms | 0.5856 ms |  1.74 |    0.10 |  9371.7 KB |        2.52 |
