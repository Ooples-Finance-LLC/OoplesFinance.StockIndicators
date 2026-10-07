```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|----------- |------ |--------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)rence [46]** |  **2.314 ms** |  **2.791 ms** | **0.1530 ms** |  **1.00** |    **0.08** |         **-** |   **374.17 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)rence [46] |  5.106 ms |  1.464 ms | 0.0803 ms |  2.21 |    0.13 |         - |   1699.1 KB |        4.54 |
|            |       |                      |           |           |           |       |         |           |             |             |
| **Ooples**     | **10000** | **Trady(...)rence [46]** | **13.802 ms** | **15.214 ms** | **0.8339 ms** |  **1.00** |    **0.07** |         **-** |  **4807.94 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)rence [46] | 18.350 ms | 16.413 ms | 0.8997 ms |  1.33 |    0.09 | 1000.0000 | 13479.51 KB |        2.80 |
