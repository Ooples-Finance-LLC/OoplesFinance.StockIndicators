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
| **Ooples**     | **1000**  | **QuanTAlib.Percentile** |  **4.927 ms** |  **5.0974 ms** | **0.2794 ms** |  **1.00** |    **0.07** |   **458.5 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Percentile |  2.218 ms |  0.8853 ms | 0.0485 ms |  0.45 |    0.02 |  396.67 KB |        0.87 |
|            |       |                      |           |            |           |       |         |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Percentile** | **14.614 ms** | **53.0980 ms** | **2.9105 ms** |  **1.03** |    **0.24** | **5663.39 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Percentile |  5.691 ms |  9.1090 ms | 0.4993 ms |  0.40 |    0.07 | 3967.82 KB |        0.70 |
