```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1  
IterationCount=3  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev     | Median    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)gStar [29]** |  **4.056 ms** |   **2.064 ms** |  **0.1131 ms** |  **4.106 ms** |  **1.00** |    **0.03** |  **272.14 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)gStar [29] |  3.253 ms |   1.754 ms |  0.0961 ms |  3.279 ms |  0.80 |    0.03 | 1086.17 KB |        3.99 |
|            |       |                      |           |            |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)gStar [29]** | **24.971 ms** | **507.429 ms** | **27.8139 ms** |  **8.986 ms** |  **2.01** |    **2.52** | **3720.55 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)gStar [29] | 11.406 ms |  25.304 ms |  1.3870 ms | 11.838 ms |  0.92 |    0.55 | 8750.66 KB |        2.35 |
