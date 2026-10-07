```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |---------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Dwma** | **2.948 ms** |  **3.318 ms** | **0.1819 ms** |  **1.00** |    **0.08** |  **274.07 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Dwma | 1.003 ms |  1.914 ms | 0.1049 ms |  0.34 |    0.04 |  545.11 KB |        1.99 |
|            |       |                |          |           |           |       |         |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Dwma** | **4.978 ms** |  **1.334 ms** | **0.0731 ms** |  **1.00** |    **0.02** | **3788.85 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Dwma | 2.669 ms | 17.819 ms | 0.9767 ms |  0.54 |    0.17 | 5446.83 KB |        1.44 |
