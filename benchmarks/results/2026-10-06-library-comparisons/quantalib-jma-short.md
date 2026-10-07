```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId        | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |-------------- |-------------:|-------------:|-------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Jma** |  **80,350.8 μs** |  **31,140.2 μs** |  **1,706.90 μs** |  **1.00** |    **0.03** |  **2000.0000** |         **-** |  **19117.35 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Jma |     974.9 μs |   1,572.6 μs |     86.20 μs |  0.01 |    0.00 |          - |         - |    388.49 KB |        0.02 |
|            |       |               |              |              |              |       |         |            |           |              |             |
| **Ooples**     | **10000** | **QuanTAlib.Jma** | **372,342.6 μs** | **618,407.8 μs** | **33,897.02 μs** | **1.006** |    **0.11** | **23000.0000** | **1000.0000** | **191102.45 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Jma |   2,904.8 μs |   2,023.0 μs |    110.89 μs | 0.008 |    0.00 |          - |         - |   3879.46 KB |        0.02 |
