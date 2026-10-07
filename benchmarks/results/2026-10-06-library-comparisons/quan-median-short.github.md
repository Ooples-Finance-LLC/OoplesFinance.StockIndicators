```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId           | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |----------------- |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Median** | **4.651 ms** |  **2.540 ms** | **0.1392 ms** |  **1.00** |    **0.04** |   **458.5 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Median | 2.172 ms |  1.833 ms | 0.1005 ms |  0.47 |    0.02 |  395.98 KB |        0.86 |
|            |       |                  |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Median** | **9.266 ms** | **13.987 ms** | **0.7667 ms** |  **1.00** |    **0.10** | **5662.94 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Median | 4.126 ms |  2.472 ms | 0.1355 ms |  0.45 |    0.03 | 3971.06 KB |        0.70 |
