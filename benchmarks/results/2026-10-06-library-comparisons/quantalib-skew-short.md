```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------- |-----------:|----------:|----------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Skew** |  **64.787 ms** | **33.027 ms** | **1.8103 ms** |  **1.00** |    **0.03** |  **2000.0000** |         **-** |  **23505.22 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Skew |   1.610 ms |  1.079 ms | 0.0592 ms |  0.02 |    0.00 |          - |         - |    399.15 KB |        0.02 |
|            |       |                |            |           |           |       |         |            |           |              |             |
| **Ooples**     | **10000** | **QuanTAlib.Skew** | **383.096 ms** | **68.976 ms** | **3.7808 ms** |  **1.00** |    **0.01** | **28000.0000** | **1000.0000** | **236957.12 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Skew |   9.300 ms |  8.801 ms | 0.4824 ms |  0.02 |    0.00 |          - |         - |   3974.56 KB |        0.02 |
