```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId              | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |-------------------- |-----------:|-----------:|----------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Curvature** |  **26.734 ms** | **16.8771 ms** | **0.9251 ms** |  **1.00** |    **0.04** |  **1000.0000** |         **-** |  **15759.55 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Curvature |   1.527 ms |  0.9908 ms | 0.0543 ms |  0.06 |    0.00 |          - |         - |    900.35 KB |        0.06 |
|            |       |                     |            |            |           |       |         |            |           |              |             |
| **Ooples**     | **10000** | **QuanTAlib.Curvature** | **149.888 ms** | **90.7615 ms** | **4.9749 ms** |  **1.00** |    **0.04** | **19000.0000** | **1000.0000** | **159200.85 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Curvature |   3.374 ms | 14.4827 ms | 0.7938 ms |  0.02 |    0.00 |  1000.0000 |         - |   9009.14 KB |        0.06 |
