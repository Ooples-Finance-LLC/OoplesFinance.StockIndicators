```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |------------:|-------------:|------------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Ltma** | **13,781.5 μs** | **12,516.34 μs** |   **686.06 μs** |  **1.00** |    **0.06** |         **-** |         **-** | **3570.81 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Ltma |    215.1 μs |     83.86 μs |     4.60 μs |  0.02 |    0.00 |         - |         - |   53.23 KB |        0.01 |
|            |       |                |             |              |             |       |         |           |           |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Ltma** | **45,842.2 μs** | **75,906.66 μs** | **4,160.70 μs** |  **1.01** |    **0.11** | **4000.0000** | **1000.0000** | **36951.9 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Ltma |    735.2 μs |  3,526.04 μs |   193.27 μs |  0.02 |    0.00 |         - |         - |  474.73 KB |        0.01 |
