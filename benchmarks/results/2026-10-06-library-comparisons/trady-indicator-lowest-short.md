```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)owest [22]** |  **2.008 ms** |  **3.6355 ms** | **0.1993 ms** |  **1.01** |    **0.12** |  **369.38 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)owest [22] |  2.855 ms |  0.6824 ms | 0.0374 ms |  1.43 |    0.12 |  834.13 KB |        2.26 |
|            |       |                      |           |            |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)owest [22]** | **10.437 ms** |  **6.7206 ms** | **0.3684 ms** |  **1.00** |    **0.04** | **4726.59 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)owest [22] | 10.023 ms | 21.7400 ms | 1.1916 ms |  0.96 |    0.10 | 7541.38 KB |        1.60 |
