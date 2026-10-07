```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId             | Mean         | Error        | StdDev      | Median       | Ratio | RatioSD | Gen0       | Gen1      | Allocated    | Alloc Ratio |
|----------- |------ |------------------- |-------------:|-------------:|------------:|-------------:|------:|--------:|-----------:|----------:|-------------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Realized** | **131,547.9 μs** | **225,055.9 μs** | **12,336.1 μs** | **131,962.5 μs** | **1.006** |    **0.12** |  **4000.0000** |         **-** |  **35279.38 KB** |       **1.000** |
| Competitor | 1000  | QuanTAlib.Realized |     709.8 μs |  10,212.1 μs |    559.8 μs |     397.5 μs | 0.005 |    0.00 |          - |         - |     70.77 KB |       0.002 |
|            |       |                    |              |              |             |              |       |         |            |           |              |             |
| **Ooples**     | **10000** | **QuanTAlib.Realized** | **358,697.5 μs** | **137,916.1 μs** |  **7,559.6 μs** | **358,526.6 μs** | **1.000** |    **0.03** | **43000.0000** | **1000.0000** | **355317.36 KB** |       **1.000** |
| Competitor | 10000 | QuanTAlib.Realized |   1,489.2 μs |   4,073.5 μs |    223.3 μs |   1,363.2 μs | 0.004 |    0.00 |          - |         - |     641.1 KB |       0.002 |
