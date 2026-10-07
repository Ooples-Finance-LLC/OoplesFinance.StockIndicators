```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9587)
AMD Ryzen 9 3950X 3.70GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=1
IterationCount=3  LaunchCount=1  UnrollFactor=1
WarmupCount=3

```
| Method     | Bars  | PairId         | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|----------- |------ |--------------- |------------:|------------:|----------:|------:|--------:|----------:|----------:|-----------:|------------:|
| **Ooples**     | **1000**  | **QuanTAlib.Epma** |  **8,628.0 μs** | **13,196.1 μs** | **723.32 μs** |  **1.00** |    **0.10** |         **-** |         **-** | **2877.35 KB** |        **1.00** |
| Competitor | 1000  | QuanTAlib.Epma |    576.6 μs |    887.1 μs |  48.62 μs |  0.07 |    0.01 |         - |         - |  262.52 KB |        0.09 |
|            |       |                |             |             |           |       |         |           |           |            |             |
| **Ooples**     | **10000** | **QuanTAlib.Epma** | **17,387.3 μs** | **15,639.6 μs** | **857.26 μs** |  **1.00** |    **0.06** | **3000.0000** | **1000.0000** | **29948.6 KB** |        **1.00** |
| Competitor | 10000 | QuanTAlib.Epma |  1,217.3 μs |  4,325.4 μs | 237.09 μs |  0.07 |    0.01 |         - |         - | 2571.02 KB |        0.09 |
