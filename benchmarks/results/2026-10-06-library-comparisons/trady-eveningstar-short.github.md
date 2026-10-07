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
| **Ooples**     | **1000**  | **Trady(...)gStar [29]** |  **4.123 ms** | **2.794 ms** | **0.1532 ms** |  **1.00** |    **0.05** |  **272.14 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)gStar [29] |  3.333 ms | 1.767 ms | 0.0969 ms |  0.81 |    0.03 | 1086.17 KB |        3.99 |
|            |       |                      |           |          |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)gStar [29]** |  **8.886 ms** | **5.622 ms** | **0.3081 ms** |  **1.00** |    **0.04** | **3721.81 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)gStar [29] | 13.115 ms | 5.959 ms | 0.3266 ms |  1.48 |    0.05 | 8748.64 KB |        2.35 |
