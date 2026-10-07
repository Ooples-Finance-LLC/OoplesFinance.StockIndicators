```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean     | Error     | StdDev   | Ratio | RatioSD | Gen0      | Allocated | Alloc Ratio |
|----------- |------ |--------------- |---------:|----------:|---------:|------:|--------:|----------:|----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Maaf** | **16.34 ms** |  **19.32 ms** | **1.059 ms** |  **1.00** |    **0.08** |         **-** |   **6.24 MB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Maaf | 18.14 ms |  13.19 ms | 0.723 ms |  1.11 |    0.07 |         - |   4.42 MB |        0.71 |
|            |       |                |          |           |          |       |         |           |           |             |
| **Ooples**     | **10000** | **QuanTAlib.Maaf** | **86.43 ms** |  **16.41 ms** | **0.899 ms** |  **1.00** |    **0.01** | **6000.0000** |  **54.16 MB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Maaf | 33.39 ms | 104.12 ms | 5.707 ms |  0.39 |    0.06 | 4000.0000 |   39.1 MB |        0.72 |
