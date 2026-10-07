```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev     | Median      | Ratio | RatioSD | Gen0       | Allocated    | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|-----------:|------------:|------:|--------:|-----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Fwma** | **28,292.2 μs** | **47,557.7 μs** | **2,606.8 μs** | **28,612.5 μs** |  **1.01** |    **0.11** |  **1000.0000** |  **11720.91 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Fwma |    787.1 μs |  4,243.6 μs |   232.6 μs |    715.4 μs |  0.03 |    0.01 |          - |    260.77 KB |        0.02 |
|            |       |                |             |             |            |             |       |         |            |              |             |
| **Ooples**     | **10000** | **QuanTAlib.Fwma** | **83,192.2 μs** | **72,113.3 μs** | **3,952.8 μs** | **84,666.9 μs** |  **1.00** |    **0.06** | **14000.0000** | **118231.53 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Fwma |  2,179.0 μs | 15,479.5 μs |   848.5 μs |  1,733.7 μs |  0.03 |    0.01 |          - |   2569.55 KB |        0.02 |
