```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean         | Error      | StdDev     | Ratio  | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |-------------:|-----------:|-----------:|-------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)Price [42]** |     **4.903 ms** |   **8.160 ms** |  **0.4473 ms** |   **1.01** |    **0.11** |  **639.71 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)Price [42] |    84.939 ms | 486.815 ms | 26.6840 ms |  17.42 |    4.93 |  910.43 KB |        1.42 |
|            |       |                      |              |            |            |        |         |            |             |
| **Ooples**     | **10000** | **Trady(...)Price [42]** |     **8.730 ms** |  **11.550 ms** |  **0.6331 ms** |   **1.00** |    **0.09** | **8314.12 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)Price [42] | 7,892.442 ms | 315.829 ms | 17.3116 ms | 907.16 |   54.74 | 8886.63 KB |        1.07 |
