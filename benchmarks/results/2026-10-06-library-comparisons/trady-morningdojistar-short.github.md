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
| **Ooples**     | **1000**  | **Trady(...)iStar [33]** | **3.372 ms** |  **0.6687 ms** | **0.0367 ms** |  **1.00** |    **0.01** |  **270.33 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)iStar [33] | 3.206 ms |  1.2361 ms | 0.0678 ms |  0.95 |    0.02 |  1117.5 KB |        4.13 |
|            |       |                      |          |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)iStar [33]** | **5.265 ms** |  **4.6233 ms** | **0.2534 ms** |  **1.00** |    **0.06** | **3720.05 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)iStar [33] | 9.941 ms | 22.8018 ms | 1.2498 ms |  1.89 |    0.22 | 9060.19 KB |        2.44 |
