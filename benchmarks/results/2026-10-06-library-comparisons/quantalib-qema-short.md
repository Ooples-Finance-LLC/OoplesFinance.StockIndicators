```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean         | Error          | StdDev       | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |--------------- |-------------:|---------------:|-------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Qema** |  **81,941.8 μs** |    **25,817.5 μs** |  **1,415.14 μs** | **1.000** |    **0.02** |  **2000.0000** |         **-** |  **17676.62 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Qema |     671.0 μs |       968.1 μs |     53.07 μs | 0.008 |    0.00 |          - |         - |    211.13 KB |        0.01 |
|            |       |                |              |                |              |       |         |            |           |              |             |
| **Ooples**     | **10000** | **QuanTAlib.Qema** | **429,951.5 μs** | **1,246,822.8 μs** | **68,342.57 μs** | **1.016** |    **0.19** | **21000.0000** | **1000.0000** | **175802.48 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Qema |   2,117.4 μs |     3,074.3 μs |    168.52 μs | 0.005 |    0.00 |          - |         - |    2039.3 KB |        0.01 |
