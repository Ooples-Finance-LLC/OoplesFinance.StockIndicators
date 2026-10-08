```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId               | Mean      | Error      | StdDev    | Median    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------------- |----------:|-----------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **Trady(...)llish [25]** |  **2.113 ms** |   **1.328 ms** | **0.0728 ms** |  **2.075 ms** |  **1.00** |    **0.04** |  **264.52 KB** |        **1.00** |
| Competitor | 1000  | Trady(...)llish [25] |  1.444 ms |   1.979 ms | 0.1085 ms |  1.450 ms |  0.68 |    0.05 |  534.16 KB |        2.02 |
|            |       |                      |           |            |           |           |       |         |            |             |
| **Ooples**     | **10000** | **Trady(...)llish [25]** | **11.164 ms** | **106.178 ms** | **5.8200 ms** | **13.856 ms** |  **1.34** |    **1.11** | **3711.62 KB** |        **1.00** |
| Competitor | 10000 | Trady(...)llish [25] |  3.606 ms |   2.666 ms | 0.1461 ms |  3.560 ms |  0.43 |    0.28 | 4573.08 KB |        1.23 |
