```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|----------- |------ |--------------- |----------:|-----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Alma** | **25.248 ms** | **14.9359 ms** | **0.8187 ms** |  **1.00** |    **0.04** |         **-** |         **-** |  **6066.15 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Alma |  1.311 ms |  0.2828 ms | 0.0155 ms |  0.05 |    0.00 |         - |         - |    70.02 KB |        0.01 |
|            |       |                |           |            |           |       |         |           |           |             |             |
| **Ooples**     | **10000** | **QuanTAlib.Alma** | **51.132 ms** | **18.7837 ms** | **1.0296 ms** |  **1.00** |    **0.02** | **6000.0000** | **1000.0000** | **55147.61 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Alma |  4.630 ms |  7.1024 ms | 0.3893 ms |  0.09 |    0.01 |         - |         - |    640.6 KB |        0.01 |
